namespace ComfyBlazorApp.Data;

public sealed class BatchItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BatchJobId { get; set; }

    public Guid PromptPresetId { get; set; }

    public int Order { get; set; }

    /// <summary>Pending | Running | Completed | Failed</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>Generate | Edit</summary>
    public string Mode { get; set; } = "Generate";

    /// <summary>Absolute path to the source image used when Mode == Edit.</summary>
    public string? SourceImagePath { get; set; }

    /// <summary>The ComfyUI prompt_id assigned when this item was queued.</summary>
    public string? PromptId { get; set; }

    public string? OutputFolder { get; set; }

    /// <summary>Comma-separated output file names produced by this item.</summary>
    public string? OutputFileNames { get; set; }

    public string? Error { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public BatchJob BatchJob { get; set; } = null!;
    public PromptPreset PromptPreset { get; set; } = null!;

    // Helper
    public IReadOnlyList<string> OutputFiles =>
        string.IsNullOrWhiteSpace(OutputFileNames)
            ? []
            : OutputFileNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
