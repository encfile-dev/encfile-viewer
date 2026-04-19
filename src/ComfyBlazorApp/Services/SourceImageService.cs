using ComfyBlazorApp.Data;
using Microsoft.EntityFrameworkCore;

namespace ComfyBlazorApp.Services;

public sealed class SourceImageService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public SourceImageService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<SourceImage>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.SourceImages
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<SourceImage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.SourceImages.FindAsync([id], cancellationToken);
    }

    public async Task<SourceImage> SaveAsync(
        Stream stream,
        string originalFileName,
        string contentRootPath,
        CancellationToken cancellationToken = default)
    {
        var dir = Path.Combine(contentRootPath, "data", "source-images");
        Directory.CreateDirectory(dir);

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(dir, storedName);

        await using var file = File.Create(fullPath);
        await stream.CopyToAsync(file, cancellationToken);
        await file.FlushAsync(cancellationToken);

        var entity = new SourceImage
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileNameWithoutExtension(originalFileName),
            FilePath = fullPath,
            StoredFileName = storedName,
            OriginalFileName = originalFileName,
            FileSizeBytes = new FileInfo(fullPath).Length,
            CreatedAt = DateTime.UtcNow
        };

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.SourceImages.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task RenameAsync(Guid id, string newName, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var img = await db.SourceImages.FindAsync([id], cancellationToken);
        if (img is null) return;

        img.Name = newName.Trim();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var img = await db.SourceImages.FindAsync([id], cancellationToken);
        if (img is null) return;

        // Delete file from disk
        if (File.Exists(img.FilePath))
        {
            try { File.Delete(img.FilePath); } catch { /* best-effort */ }
        }

        db.SourceImages.Remove(img);
        await db.SaveChangesAsync(cancellationToken);
    }
}
