using ComfyBlazorApp.Models;
using ComfyBlazorApp.Options;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;

namespace ComfyBlazorApp.Services;

public sealed class ImageService
{
    private static readonly string[] SupportedExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    private readonly ComfyUiOptions _options;
    private readonly IWebHostEnvironment _environment;

    public ImageService(IOptions<ComfyUiOptions> options, IWebHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public string OutputDirectoryPath => ResolvePath(_options.OutputDirectory);

    public string? TryGetImagePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var safeFileName = Path.GetFileName(fileName);
        var fullPath = Path.GetFullPath(Path.Combine(OutputDirectoryPath, safeFileName));
        var outputRoot = Path.GetFullPath(OutputDirectoryPath);

        if (!fullPath.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return File.Exists(fullPath) ? fullPath : null;
    }

    public Task<IReadOnlyList<GeneratedImage>> GetImagesAsync(int take = 60, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(OutputDirectoryPath))
        {
            return Task.FromResult<IReadOnlyList<GeneratedImage>>([]);
        }

        var outputRoot = new DirectoryInfo(OutputDirectoryPath);
        var images = outputRoot
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(file => SupportedExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(file => file.CreationTimeUtc)
            .Take(take)
            .Select(CreateGeneratedImage)
            .ToList();

        return Task.FromResult<IReadOnlyList<GeneratedImage>>(images);
    }

    public Task<IReadOnlyList<string>> GetImagesCreatedAfterAsync(DateTimeOffset timestamp, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(OutputDirectoryPath))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var files = new DirectoryInfo(OutputDirectoryPath)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(file => SupportedExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
            .Where(file => file.CreationTimeUtc >= timestamp.UtcDateTime.AddSeconds(-1))
            .OrderBy(file => file.CreationTimeUtc)
            .Select(file => file.Name)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    private string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, path));
    }

    private static GeneratedImage CreateGeneratedImage(FileInfo file)
    {
        var (width, height) = ReadDimensions(file.FullName);

        return new GeneratedImage
        {
            FileName = file.Name,
            Url = $"/images/{Uri.EscapeDataString(file.Name)}",
            Width = width,
            Height = height,
            CreatedAt = new DateTimeOffset(file.CreationTimeUtc, TimeSpan.Zero)
        };
    }

    private static (int Width, int Height) ReadDimensions(string path)
    {
        try
        {
            var image = Image.Identify(path);
            if (image is null)
            {
                return (1600, 1200);
            }

            return (image.Width, image.Height);
        }
        catch
        {
            return (1600, 1200);
        }
    }
}
