using ComfyBlazorApp.Data;
using ComfyBlazorApp.Models;

namespace ComfyBlazorApp.Services;

/// <summary>
/// Background service that processes BatchJobs sequentially.
/// One job at a time, one item at a time, to prevent GPU overload.
/// </summary>
public sealed class BatchWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BatchWorker> _logger;

    // Signalled whenever a new batch is enqueued so we don't wait the full poll interval.
    private readonly SemaphoreSlim _wakeSignal = new(0, 1);

    public BatchWorker(IServiceScopeFactory scopeFactory, ILogger<BatchWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Call this from UI code after CreateBatchAsync to wake the worker immediately.</summary>
    public void SignalNewBatch()
    {
        _wakeSignal.Release();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BatchWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextJobIfAvailableAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in BatchWorker loop.");
            }

            // Wait up to 5 seconds, or until a new batch signals us
            await _wakeSignal.WaitAsync(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            // Drain any extra signals
            while (_wakeSignal.CurrentCount > 0) _wakeSignal.Wait(0);
        }

        _logger.LogInformation("BatchWorker stopped.");
    }

    private async Task ProcessNextJobIfAvailableAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var batchService = scope.ServiceProvider.GetRequiredService<BatchService>();
        var comfyUiService = scope.ServiceProvider.GetRequiredService<ComfyUiService>();
        var jobService = scope.ServiceProvider.GetRequiredService<JobService>();

        var batchJob = await batchService.GetNextPendingJobAsync(stoppingToken);
        if (batchJob is null)
        {
            return;
        }

        _logger.LogInformation("BatchWorker: starting job {JobId} ({Name})", batchJob.Id, batchJob.Name ?? "(unnamed)");
        await batchService.MarkJobRunningAsync(batchJob.Id, stoppingToken);

        var failed = false;

        foreach (var item in batchJob.Items.OrderBy(i => i.Order))
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            var preset = item.PromptPreset;
            _logger.LogInformation("BatchWorker: running item {ItemId} (preset '{Name}')", item.Id, preset.Name);

            try
            {
                var request = new GenerateRequest
                {
                    Prompt = preset.Prompt,
                    NegativePrompt = preset.NegativePrompt,
                    BatchSize = 1,
                    Mode = item.Mode == "Edit" ? GenerateMode.Edit : GenerateMode.Generate
                };

                UploadedImageReference? uploadedImage = null;

                if (item.Mode == "Edit")
                {
                    if (string.IsNullOrWhiteSpace(item.SourceImagePath) || !File.Exists(item.SourceImagePath))
                    {
                        await batchService.MarkItemFailedAsync(item.Id, "Source image not found on disk.", stoppingToken);
                        _logger.LogWarning("BatchWorker: item {ItemId} failed — source image missing: {Path}", item.Id, item.SourceImagePath);
                        failed = true;
                        continue;
                    }

                    await using var imgStream = File.OpenRead(item.SourceImagePath);
                    uploadedImage = await comfyUiService.UploadImageAsync(
                        imgStream,
                        Path.GetFileName(item.SourceImagePath),
                        contentType: null,
                        stoppingToken);

                    _logger.LogInformation("BatchWorker: uploaded source image {Name} for item {ItemId}", uploadedImage.Name, item.Id);
                }

                var promptId = await comfyUiService.QueuePromptAsync(request, uploadedImage, stoppingToken);
                jobService.RegisterQueuedJob(promptId, 1, request.Mode);

                await batchService.MarkItemRunningAsync(item.Id, promptId, stoppingToken);
                await jobService.WaitForCompletionAsync(promptId, stoppingToken);

                var job = jobService.GetJob(promptId);
                if (job is null || job.State == "Failed")
                {
                    var err = job?.Error ?? "Unknown error";
                    await batchService.MarkItemFailedAsync(item.Id, err, stoppingToken);
                    _logger.LogWarning("BatchWorker: item {ItemId} failed: {Error}", item.Id, err);
                    failed = true;
                }
                else
                {
                    var outputFiles = await comfyUiService.GetOutputFilesAsync(promptId, stoppingToken);
                    await batchService.MarkItemCompletedAsync(item.Id, outputFiles, stoppingToken);
                    _logger.LogInformation("BatchWorker: item {ItemId} completed with {Count} file(s)", item.Id, outputFiles.Count);
                }
            }

            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                await batchService.MarkItemFailedAsync(item.Id, ex.Message, stoppingToken);
                _logger.LogError(ex, "BatchWorker: error processing item {ItemId}", item.Id);
                failed = true;
            }
        }

        if (failed)
        {
            await batchService.MarkJobFailedAsync(batchJob.Id, stoppingToken);
            _logger.LogWarning("BatchWorker: job {JobId} completed with failures", batchJob.Id);
        }
        else
        {
            await batchService.MarkJobCompletedAsync(batchJob.Id, stoppingToken);
            _logger.LogInformation("BatchWorker: job {JobId} completed successfully", batchJob.Id);
        }
    }
}
