namespace ComfyBlazorApp.Models;

public sealed class UploadedImageReference
{
    public required string Name { get; init; }

    public string Type { get; init; } = "input";

    public string? Subfolder { get; init; }
}
