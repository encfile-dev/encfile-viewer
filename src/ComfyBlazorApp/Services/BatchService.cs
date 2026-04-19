using ComfyBlazorApp.Data;
using Microsoft.EntityFrameworkCore;

namespace ComfyBlazorApp.Services;

public sealed class BatchService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public BatchService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<BatchJob>> GetRecentJobsAsync(int take = 20, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.BatchJobs
            .Include(j => j.Items)
            .OrderByDescending(j => j.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<BatchJob?> GetJobWithItemsAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.BatchJobs
            .Include(j => j.Items)
                .ThenInclude(i => i.PromptPreset)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
    }

    /// <summary>Creates a new batch job from an ordered list of preset IDs.</summary>
    public async Task<BatchJob> CreateBatchAsync(
        IList<Guid> presetIds,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var job = new BatchJob
        {
            Name = name,
            TotalItems = presetIds.Count,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        db.BatchJobs.Add(job);

        for (var i = 0; i < presetIds.Count; i++)
        {
            db.BatchItems.Add(new BatchItem
            {
                BatchJobId = job.Id,
                PromptPresetId = presetIds[i],
                Order = i,
                Status = "Pending",
                Mode = "Generate"
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return job;
    }

    /// <summary>
    /// Creates an Edit batch: one job, N items — each applies a different preset to the SAME source image.
    /// </summary>
    public async Task<BatchJob> CreateEditBatchAsync(
        string sourceImagePath,
        IList<Guid> presetIds,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceImagePath))
            throw new InvalidOperationException($"Source image not found: {sourceImagePath}");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var job = new BatchJob
        {
            Name = name,
            TotalItems = presetIds.Count,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        db.BatchJobs.Add(job);

        for (var i = 0; i < presetIds.Count; i++)
        {
            db.BatchItems.Add(new BatchItem
            {
                BatchJobId = job.Id,
                PromptPresetId = presetIds[i],
                Order = i,
                Status = "Pending",
                Mode = "Edit",
                SourceImagePath = sourceImagePath
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return job;
    }


    /// <summary>Returns the oldest pending job (for the worker to pick up).</summary>
    public async Task<BatchJob?> GetNextPendingJobAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.BatchJobs
            .Include(j => j.Items.OrderBy(i => i.Order))
                .ThenInclude(i => i.PromptPreset)
            .Where(j => j.Status == "Pending")
            .OrderBy(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task MarkJobRunningAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.BatchJobs.FindAsync([jobId], cancellationToken);
        if (job is not null)
        {
            job.Status = "Running";
            job.StartedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkJobCompletedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.BatchJobs.FindAsync([jobId], cancellationToken);
        if (job is not null)
        {
            job.Status = "Completed";
            job.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkJobFailedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.BatchJobs.FindAsync([jobId], cancellationToken);
        if (job is not null)
        {
            job.Status = "Failed";
            job.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkItemRunningAsync(Guid itemId, string promptId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.BatchItems.FindAsync([itemId], cancellationToken);
        if (item is not null)
        {
            item.Status = "Running";
            item.PromptId = promptId;
            item.StartedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkItemCompletedAsync(Guid itemId, IReadOnlyList<string> outputFiles, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.BatchItems.FindAsync([itemId], cancellationToken);
        if (item is not null)
        {
            item.Status = "Completed";
            item.OutputFileNames = string.Join(',', outputFiles);
            item.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkItemFailedAsync(Guid itemId, string error, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.BatchItems.FindAsync([itemId], cancellationToken);
        if (item is not null)
        {
            item.Status = "Failed";
            item.Error = error;
            item.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var job = await db.BatchJobs.FindAsync([jobId], cancellationToken);
        if (job is not null)
        {
            db.BatchJobs.Remove(job);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
