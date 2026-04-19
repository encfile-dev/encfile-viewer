namespace ComfyBlazorApp.Options;

public sealed class ComfyUiOptions
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:8188";

    public string PromptEndpoint { get; set; } = "/prompt";

    public string UploadImageEndpoint { get; set; } = "/upload/image";

    public string HistoryEndpoint { get; set; } = "/history";

    public string WebSocketEndpoint { get; set; } = "/ws";

    public string OutputDirectory { get; set; } = "ComfyUI-output";

    public string GenerateWorkflowTemplatePath { get; set; } = "Data/workflow-generate-template.json";

    public string EditWorkflowTemplatePath { get; set; } = "Data/workflow-edit-template.json";

    public string ClientId { get; set; } = "comfy-blazor-app";

    public string UploadImageType { get; set; } = "input";
}
