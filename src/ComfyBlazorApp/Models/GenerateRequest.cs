using System.ComponentModel.DataAnnotations;

namespace ComfyBlazorApp.Models;

public sealed class GenerateRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 2)]
    public string Prompt { get; set; } = string.Empty;

    [StringLength(2000)]
    public string NegativePrompt { get; set; } = string.Empty;

    [Range(1, 8)]
    public int BatchSize { get; set; } = 1;

    public GenerateMode Mode { get; set; } = GenerateMode.Generate;
}
