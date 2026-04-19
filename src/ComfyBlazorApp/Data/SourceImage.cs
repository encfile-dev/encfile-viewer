namespace ComfyBlazorApp.Data;

public sealed class SourceImage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>User-friendly display name (derived from original filename by default).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Absolute path on disk: data/source-images/{StoredFileName}.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Stored file name on disk (guid.ext) – used for serving via /source-images/{StoredFileName}.</summary>
    public string StoredFileName { get; set; } = string.Empty;

    /// <summary>Original file name uploaded by the user (for display only).</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Derived
    public string PublicUrl => $"/source-images/{Uri.EscapeDataString(StoredFileName)}";

    public string HumanSize => FileSizeBytes switch
    {
        >= 1_048_576 => $"{FileSizeBytes / 1_048_576.0:F1} MB",
        >= 1_024 => $"{FileSizeBytes / 1_024.0:F0} KB",
        _ => $"{FileSizeBytes} B"
    };
}
