using System.Collections.Concurrent;
using ComfyBlazorApp.Models;

namespace ComfyBlazorApp.Services;

public sealed class JobService
{
    // Fired whenever a job transitions to a terminal state (Completed / Failed).
    // Subscribers receive the promptId.
    public event Action<string>? JobCompleted;
    private readonly ConcurrentDictionary<string, JobStatus> _jobs = new();

    public JobStatus RegisterQueuedJob(
        string promptId,
        int expectedImageCount,
        GenerateMode mode,
        string? sourceImageFileName = null)
    {
        var job = new JobStatus
        {
            PromptId = promptId,
            ExpectedImageCount = expectedImageCount,
            Mode = mode,
            SourceImageFileName = sourceImageFileName,
            State = "Queued"
        };

        _jobs[promptId] = job;
        return job;
    }

    public IReadOnlyList<JobStatus> GetJobs()
    {
        return _jobs.Values
            .OrderByDescending(job => job.StartedAt)
            .ToList();
    }

    public JobStatus? GetJob(string promptId)
    {
        _jobs.TryGetValue(promptId, out var job);
        return job;
    }

    public string? GetLatestActivePromptId()
    {
        return _jobs.Values
            .Where(job => !job.IsComplete)
            .OrderByDescending(job => job.StartedAt)
            .Select(job => job.PromptId)
            .FirstOrDefault();
    }

    public void MarkRunning(string promptId, string? currentNode = null)
    {
        if (_jobs.TryGetValue(promptId, out var job))
        {
            job.State = "Generating";
            job.CurrentNode = currentNode;
        }
    }

    public void UpdateProgress(string promptId, int? value, int? max)
    {
        if (_jobs.TryGetValue(promptId, out var job))
        {
            job.State = "Generating";
            job.ProgressValue = value;
            job.ProgressMax = max;
        }
    }

    public void MarkCompleted(string promptId, IReadOnlyList<string> outputFiles)
    {
        if (_jobs.TryGetValue(promptId, out var job))
        {
            job.State = "Completed";
            job.IsComplete = true;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.CurrentNode = null;
            job.ProgressValue = job.ProgressMax;

            foreach (var file in outputFiles)
            {
                if (!job.OutputFiles.Contains(file, StringComparer.OrdinalIgnoreCase))
                {
                    job.OutputFiles.Add(file);
                }
            }
        }

        JobCompleted?.Invoke(promptId);
    }

    public void MarkFailed(string promptId, string error)
    {
        if (_jobs.TryGetValue(promptId, out var job))
        {
            job.State = "Failed";
            job.Error = error;
            job.IsComplete = true;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.CurrentNode = null;
        }

        JobCompleted?.Invoke(promptId);
    }

    /// <summary>
    /// Awaits until the given prompt reaches a terminal state (Completed or Failed).
    /// Falls back to polling every 500 ms if the event fires before we subscribe.
    /// </summary>
    public async Task WaitForCompletionAsync(string promptId, CancellationToken cancellationToken = default)
    {
        // Fast path: already complete
        if (_jobs.TryGetValue(promptId, out var job) && job.IsComplete)
        {
            return;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnCompleted(string id)
        {
            if (id == promptId)
            {
                tcs.TrySetResult();
            }
        }

        JobCompleted += OnCompleted;
        await using var reg = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

        try
        {
            // Re-check after subscribing to avoid race condition
            if (_jobs.TryGetValue(promptId, out var latestJob) && latestJob.IsComplete)
            {
                tcs.TrySetResult();
            }

            await tcs.Task;
        }
        finally
        {
            JobCompleted -= OnCompleted;
        }
    }
}
