using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace ComfyBlazorApp.Services;

public sealed class ComfyUiWebSocketService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ComfyUiWebSocketService> _logger;
    private string? _currentPromptId;

    public ComfyUiWebSocketService(IServiceScopeFactory scopeFactory, ILogger<ComfyUiWebSocketService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var comfyUiService = scope.ServiceProvider.GetRequiredService<ComfyUiService>();
                var jobService = scope.ServiceProvider.GetRequiredService<JobService>();

                using var socket = new ClientWebSocket();
                await socket.ConnectAsync(comfyUiService.GetWebSocketUri(), stoppingToken);
                _logger.LogInformation("Connected to ComfyUI WebSocket.");

                await ReceiveLoopAsync(socket, jobService, comfyUiService, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ComfyUI WebSocket connection failed. Retrying.");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }

    private async Task ReceiveLoopAsync(
        ClientWebSocket socket,
        JobService jobService,
        ComfyUiService comfyUiService,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];

        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var message = await ReceiveTextMessageAsync(socket, buffer, cancellationToken);
            if (message is null)
            {
                continue;
            }

            await HandleMessageAsync(message, jobService, comfyUiService, cancellationToken);
        }
    }

    private async Task HandleMessageAsync(
        string message,
        JobService jobService,
        ComfyUiService comfyUiService,
        CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(message);
        var root = document.RootElement;

        if (!root.TryGetProperty("type", out var typeProperty))
        {
            return;
        }

        var messageType = typeProperty.GetString();
        var data = root.TryGetProperty("data", out var dataElement) ? dataElement : default;

        switch (messageType)
        {
            case "execution_start":
                if (TryGetPromptId(data, out var startPromptId))
                {
                    _currentPromptId = startPromptId!;
                    jobService.MarkRunning(startPromptId!);
                }

                break;
            case "executing":
                if (TryGetPromptId(data, out var executingPromptId))
                {
                    _currentPromptId = executingPromptId;
                }

                if (_currentPromptId is null)
                {
                    _currentPromptId = jobService.GetLatestActivePromptId();
                }

                if (_currentPromptId is not null)
                {
                    var node = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("node", out var nodeProperty)
                        ? nodeProperty.GetString()
                        : null;

                    if (node is null)
                    {
                        var outputFiles = await comfyUiService.GetOutputFilesAsync(_currentPromptId, cancellationToken);
                        jobService.MarkCompleted(_currentPromptId, outputFiles);
                        _currentPromptId = null;
                    }
                    else
                    {
                        jobService.MarkRunning(_currentPromptId, node);
                    }
                }

                break;
            case "progress":
                var progressPromptId = TryGetPromptId(data, out var explicitPromptId)
                    ? explicitPromptId
                    : _currentPromptId ?? jobService.GetLatestActivePromptId();

                if (progressPromptId is not null)
                {
                    var value = data.TryGetProperty("value", out var valueProperty) ? valueProperty.GetInt32() : (int?)null;
                    var max = data.TryGetProperty("max", out var maxProperty) ? maxProperty.GetInt32() : (int?)null;
                    jobService.UpdateProgress(progressPromptId, value, max);
                }

                break;
            case "execution_error":
                var failedPromptId = TryGetPromptId(data, out var errorPromptId)
                    ? errorPromptId
                    : _currentPromptId ?? jobService.GetLatestActivePromptId();

                if (failedPromptId is not null)
                {
                    var errorMessage = data.TryGetProperty("exception_message", out var errorProperty)
                        ? errorProperty.GetString()
                        : "ComfyUI reported an execution error.";

                    jobService.MarkFailed(failedPromptId, errorMessage ?? "ComfyUI reported an execution error.");
                }

                break;
        }
    }

    private static bool TryGetPromptId(JsonElement data, out string? promptId)
    {
        promptId = null;

        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("prompt_id", out var property))
        {
            return false;
        }

        promptId = property.GetString();
        return !string.IsNullOrWhiteSpace(promptId);
    }

    private static async Task<string?> ReceiveTextMessageAsync(
        ClientWebSocket socket,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var segment = new ArraySegment<byte>(buffer);
        using var memoryStream = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(segment, cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (result.MessageType == WebSocketMessageType.Binary)
            {
                if (result.EndOfMessage)
                {
                    return null;
                }

                continue;
            }

            memoryStream.Write(buffer, 0, result.Count);

            if (result.EndOfMessage)
            {
                break;
            }
        }

        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }
}
