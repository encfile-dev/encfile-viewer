namespace ComfyBlazorApp.Models;

public sealed class GeneratedImage
{
    public required string FileName { get; init; }

    public required string Url { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
