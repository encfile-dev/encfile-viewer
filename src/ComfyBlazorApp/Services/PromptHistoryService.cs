using ComfyBlazorApp.Data;
using Microsoft.EntityFrameworkCore;

namespace ComfyBlazorApp.Services;

public sealed class PromptHistoryService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public PromptHistoryService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<PromptHistory>> GetRecentAsync(int take = 100, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.PromptHistories
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<PromptHistory?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.PromptHistories.FindAsync([id], ct);
    }

    public async Task<PromptHistory> AddAsync(
        string prompt,
        string negativePrompt,
        string mode,
        int batchSize,
        string? sourceImagePath = null,
        string? comfyPromptId = null,
        CancellationToken ct = default)
    {
        var entry = new PromptHistory
        {
            Prompt = prompt,
            NegativePrompt = negativePrompt,
            Mode = mode,
            BatchSize = batchSize,
            SourceImagePath = sourceImagePath,
            ComfyPromptId = comfyPromptId,
            CreatedAt = DateTime.UtcNow
        };

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        db.PromptHistories.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entry = await db.PromptHistories.FindAsync([id], ct);
        if (entry is null) return;
        db.PromptHistories.Remove(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task ClearAllAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.PromptHistories.ExecuteDeleteAsync(ct);
    }
}
