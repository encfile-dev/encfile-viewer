namespace ComfyBlazorApp.Data;

public sealed class PromptPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;

    public string NegativePrompt { get; set; } = string.Empty;

    /// <summary>Comma-separated tags for quick filtering.</summary>
    public string? Tags { get; set; }

    /// <summary>Path to an optional reference/thumbnail image stored locally.</summary>
    public string? ImagePath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastUsedAt { get; set; }

    // Navigation
    public ICollection<BatchItem> BatchItems { get; set; } = [];

    // Helpers
    public IReadOnlyList<string> TagList =>
        string.IsNullOrWhiteSpace(Tags)
            ? []
            : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
