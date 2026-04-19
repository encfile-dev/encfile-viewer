namespace ComfyBlazorApp.Data;

public sealed class BatchJob
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? Name { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>Pending | Running | Completed | Failed</summary>
    public string Status { get; set; } = "Pending";

    public int TotalItems { get; set; }

    // Navigation
    public ICollection<BatchItem> Items { get; set; } = [];
}
