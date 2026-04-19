namespace ComfyBlazorApp.Data;

public sealed class PromptHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The positive prompt text that was used.</summary>
    public string Prompt { get; set; } = string.Empty;

    public string NegativePrompt { get; set; } = string.Empty;

    /// <summary>Generate | Edit</summary>
    public string Mode { get; set; } = "Generate";

    /// <summary>Absolute path to source image (Edit mode only).</summary>
    public string? SourceImagePath { get; set; }

    public int BatchSize { get; set; } = 1;

    /// <summary>ComfyUI prompt_id returned at queue time – for reference only.</summary>
    public string? ComfyPromptId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Derived helpers ────────────────────────────────────────────────

    public string ModeLabel => Mode;
    public bool IsEdit => Mode == "Edit";

    public string PromptExcerpt(int max = 120) =>
        Prompt.Length <= max ? Prompt : Prompt[..max] + "…";
}
