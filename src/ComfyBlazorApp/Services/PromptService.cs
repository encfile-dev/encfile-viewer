using ComfyBlazorApp.Data;
using Microsoft.EntityFrameworkCore;

namespace ComfyBlazorApp.Services;

public sealed class PromptService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public PromptService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<PromptPreset>> GetAllAsync(string? tagFilter = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.PromptPresets.OrderByDescending(p => p.LastUsedAt ?? p.CreatedAt);

        if (!string.IsNullOrWhiteSpace(tagFilter))
        {
            var tag = tagFilter.Trim().ToLowerInvariant();
            query = (IOrderedQueryable<PromptPreset>)query.Where(p =>
                p.Tags != null && EF.Functions.Like(p.Tags.ToLower(), $"%{tag}%"));
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<PromptPreset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.PromptPresets.FindAsync([id], cancellationToken);
    }

    public async Task<PromptPreset> AddAsync(PromptPreset preset, CancellationToken cancellationToken = default)
    {
        preset.Id = Guid.NewGuid();
        preset.CreatedAt = DateTime.UtcNow;

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.PromptPresets.Add(preset);
        await db.SaveChangesAsync(cancellationToken);
        return preset;
    }

    public async Task UpdateAsync(PromptPreset preset, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        db.PromptPresets.Update(preset);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var preset = await db.PromptPresets.FindAsync([id], cancellationToken);
        if (preset is not null)
        {
            db.PromptPresets.Remove(preset);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task TouchLastUsedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var preset = await db.PromptPresets.FindAsync([id], cancellationToken);
        if (preset is not null)
        {
            preset.LastUsedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<string> SavePresetImageAsync(
        Stream stream,
        string fileName,
        string contentRootPath,
        CancellationToken cancellationToken = default)
    {
        var dir = Path.Combine(contentRootPath, "data", "prompt-images");
        Directory.CreateDirectory(dir);

        var ext = Path.GetExtension(fileName);
        var safeName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(dir, safeName);

        await using var file = File.Create(fullPath);
        await stream.CopyToAsync(file, cancellationToken);
        return fullPath;
    }
}
