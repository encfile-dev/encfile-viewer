namespace ComfyBlazorApp.Models;

public sealed class JobStatus
{
    public required string PromptId { get; init; }

    public required int ExpectedImageCount { get; init; }

    public GenerateMode Mode { get; init; } = GenerateMode.Generate;

    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    public string State { get; set; } = "Queued";

    public bool IsComplete { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? Error { get; set; }

    public string? CurrentNode { get; set; }

    public int? ProgressValue { get; set; }

    public int? ProgressMax { get; set; }

    public string? SourceImageFileName { get; init; }

    public List<string> OutputFiles { get; } = [];

    public int ProgressPercent =>
        ProgressValue.HasValue && ProgressMax.GetValueOrDefault() > 0
            ? (int)Math.Round(100d * ProgressValue.Value / ProgressMax.GetValueOrDefault())
            : 0;
}
