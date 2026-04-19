using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ComfyBlazorApp.Models;
using ComfyBlazorApp.Options;
using Microsoft.Extensions.Options;

namespace ComfyBlazorApp.Services;

public sealed class ComfyUiService
{
    private readonly HttpClient _httpClient;
    private readonly ComfyUiOptions _options;
    private readonly IWebHostEnvironment _environment;

    public ComfyUiService(HttpClient httpClient, IOptions<ComfyUiOptions> options, IWebHostEnvironment environment)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _environment = environment;
    }

    public Uri GetWebSocketUri()
    {
        var baseUri = new Uri(_options.BaseUrl);
        var builder = new UriBuilder(baseUri)
        {
            Scheme = baseUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws",
            Path = _options.WebSocketEndpoint.TrimStart('/'),
            Query = $"clientId={Uri.EscapeDataString(_options.ClientId)}"
        };

        return builder.Uri;
    }

    public async Task<string> QueuePromptAsync(
        GenerateRequest request,
        UploadedImageReference? uploadedImage = null,
        CancellationToken cancellationToken = default)
    {
        var payload = await BuildPromptPayloadAsync(request, uploadedImage, cancellationToken);

        using var response = await _httpClient.PostAsJsonAsync(
            _options.PromptEndpoint.TrimStart('/'),
            payload,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"ComfyUI request failed ({(int)response.StatusCode}): {body}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("ComfyUI returned an empty response.");

        var promptId = json["prompt_id"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(promptId))
        {
            throw new InvalidOperationException("ComfyUI response did not contain a prompt_id.");
        }

        return promptId;
    }

    public async Task<UploadedImageReference> UploadImageAsync(
        Stream stream,
        string fileName,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        var normalizedContentType = NormalizeImageContentType(fileName, contentType);

        using var multipart = new MultipartFormDataContent();
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(normalizedContentType);

        multipart.Add(fileContent, "image", Path.GetFileName(fileName));
        multipart.Add(new StringContent(_options.UploadImageType), "type");
        multipart.Add(new StringContent("true"), "overwrite");

        using var response = await _httpClient.PostAsync(
            _options.UploadImageEndpoint.TrimStart('/'),
            multipart,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Image upload failed ({(int)response.StatusCode}): {body}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("ComfyUI upload returned an empty response.");

        var name = json["name"]?.GetValue<string>() ?? Path.GetFileName(fileName);

        return new UploadedImageReference
        {
            Name = name,
            Type = json["type"]?.GetValue<string>() ?? _options.UploadImageType,
            Subfolder = json["subfolder"]?.GetValue<string>()
        };
    }

    public async Task<IReadOnlyList<string>> GetOutputFilesAsync(string promptId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<JsonObject>(
            $"{_options.HistoryEndpoint.TrimStart('/')}/{Uri.EscapeDataString(promptId)}",
            cancellationToken);

        if (response is null || response[promptId] is not JsonObject promptHistory)
        {
            return [];
        }

        if (promptHistory["outputs"] is not JsonObject outputs)
        {
            return [];
        }

        var files = new List<string>();

        foreach (var node in outputs)
        {
            if (node.Value is not JsonObject outputNode)
            {
                continue;
            }

            if (outputNode["images"] is not JsonArray images)
            {
                continue;
            }

            foreach (var imageNode in images.OfType<JsonObject>())
            {
                var filename = imageNode["filename"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(filename) &&
                    !files.Contains(filename, StringComparer.OrdinalIgnoreCase))
                {
                    files.Add(filename);
                }
            }
        }

        return files;
    }

    private async Task<JsonObject> BuildPromptPayloadAsync(
        GenerateRequest request,
        UploadedImageReference? uploadedImage,
        CancellationToken cancellationToken)
    {
        var templatePath = ResolvePath(
            request.Mode == GenerateMode.Edit
                ? _options.EditWorkflowTemplatePath
                : _options.GenerateWorkflowTemplatePath);

        if (!File.Exists(templatePath))
        {
            throw new InvalidOperationException(
                $"Workflow template not found at '{templatePath}'. Update the ComfyUI workflow template path before generating.");
        }

        if (request.Mode == GenerateMode.Edit && uploadedImage is null)
        {
            throw new InvalidOperationException("Edit mode requires an uploaded source image.");
        }

        await using var stream = File.OpenRead(templatePath);
        var root = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken) as JsonObject
            ?? throw new InvalidOperationException("Workflow template must be a JSON object.");

        ReplaceTokens(root, request, uploadedImage);

        if (root["prompt"] is JsonObject)
        {
            root["client_id"] = _options.ClientId;
            return root;
        }

        return new JsonObject
        {
            ["client_id"] = _options.ClientId,
            ["prompt"] = root
        };
    }

    private static void ReplaceTokens(JsonNode? node, GenerateRequest request, UploadedImageReference? uploadedImage)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj)
                {
                    if (property.Value is JsonObject or JsonArray)
                    {
                        ReplaceTokens(property.Value, request, uploadedImage);
                        continue;
                    }

                    var replaced = ReplaceScalarValue(property.Value, request, uploadedImage);
                    if (!ReferenceEquals(replaced, property.Value))
                    {
                        obj[property.Key] = replaced;
                    }
                }
                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    if (array[i] is JsonObject or JsonArray)
                    {
                        ReplaceTokens(array[i], request, uploadedImage);
                        continue;
                    }

                    var replaced = ReplaceScalarValue(array[i], request, uploadedImage);
                    if (!ReferenceEquals(replaced, array[i]))
                    {
                        array[i] = replaced;
                    }
                }
                break;
        }
    }

    private static JsonNode? ReplaceScalarValue(JsonNode? node, GenerateRequest request, UploadedImageReference? uploadedImage)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var stringValue))
        {
            if (stringValue == "{{batch_size}}")
            {
                return JsonValue.Create(request.BatchSize);
            }

            return JsonValue.Create(
                stringValue
                    .Replace("{{prompt}}", request.Prompt, StringComparison.Ordinal)
                    .Replace("{{negative_prompt}}", request.NegativePrompt, StringComparison.Ordinal)
                    .Replace("{{input_image}}", uploadedImage?.Name ?? string.Empty, StringComparison.Ordinal));
        }

        return node;
    }

    private string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, path));
    }

    private static string NormalizeImageContentType(string fileName, string? contentType)
    {
        if (contentType is "image/png" or "image/jpeg" or "image/webp")
        {
            return contentType;
        }

        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => throw new InvalidOperationException("Only PNG, JPEG, and WebP uploads are supported.")
        };
    }
}
