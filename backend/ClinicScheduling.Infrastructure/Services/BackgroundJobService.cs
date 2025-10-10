using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ClinicScheduling.Infrastructure.Services;

public interface IBackgroundJobService
{
    Task EnqueueAsync<T>(Func<IServiceProvider, CancellationToken, Task<T>> job, string jobName = "");
    Task EnqueueAsync(Func<IServiceProvider, CancellationToken, Task> job, string jobName = "");
    Task<JobStatus> GetJobStatusAsync(string jobId);
    Task<IEnumerable<JobInfo>> GetActiveJobsAsync();
}

public class BackgroundJobService : BackgroundService, IBackgroundJobService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundJobService> _logger;
    private readonly ConcurrentQueue<JobItem> _jobQueue = new();
    private readonly ConcurrentDictionary<string, JobInfo> _jobStatuses = new();
    private readonly SemaphoreSlim _semaphore;

    public BackgroundJobService(
        IServiceProvider serviceProvider, 
        ILogger<BackgroundJobService> logger,
        int maxConcurrentJobs = 5)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _semaphore = new SemaphoreSlim(maxConcurrentJobs, maxConcurrentJobs);
    }

    public Task EnqueueAsync<T>(Func<IServiceProvider, CancellationToken, Task<T>> job, string jobName = "")
    {
        var jobId = Guid.NewGuid().ToString();
        var jobItem = new JobItem
        {
            Id = jobId,
            Name = string.IsNullOrEmpty(jobName) ? typeof(T).Name : jobName,
            JobFunc = async (sp, ct) => 
            {
                var result = await job(sp, ct);
                return result;
            },
            EnqueuedAt = DateTime.UtcNow
        };

        _jobQueue.Enqueue(jobItem);
        _jobStatuses[jobId] = new JobInfo
        {
            Id = jobId,
            Name = jobItem.Name,
            Status = JobStatus.Queued,
            EnqueuedAt = jobItem.EnqueuedAt
        };

        _logger.LogInformation("Job {JobId} ({JobName}) enqueued", jobId, jobItem.Name);
        return Task.CompletedTask;
    }

    public Task EnqueueAsync(Func<IServiceProvider, CancellationToken, Task> job, string jobName = "")
    {
        return EnqueueAsync<object>(async (sp, ct) =>
        {
            await job(sp, ct);
            return null!;
        }, jobName);
    }

    public Task<JobStatus> GetJobStatusAsync(string jobId)
    {
        if (_jobStatuses.TryGetValue(jobId, out var jobInfo))
        {
            return Task.FromResult(jobInfo.Status);
        }
        return Task.FromResult(JobStatus.NotFound);
    }

    public Task<IEnumerable<JobInfo>> GetActiveJobsAsync()
    {
        var activeJobs = _jobStatuses.Values
            .Where(j => j.Status == JobStatus.Queued || j.Status == JobStatus.Running)
            .ToList();
        return Task.FromResult<IEnumerable<JobInfo>>(activeJobs);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Job Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_jobQueue.TryDequeue(out var jobItem))
                {
                    // Wait for available slot
                    await _semaphore.WaitAsync(stoppingToken);
                    
                    // Process job in background
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessJobAsync(jobItem, stoppingToken);
                        }
                        finally
                        {
                            _semaphore.Release();
                        }
                    }, stoppingToken);
                }
                else
                {
                    // No jobs available, wait a bit
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background job service main loop");
                await Task.Delay(5000, stoppingToken); // Wait before retrying
            }
        }

        _logger.LogInformation("Background Job Service stopped");
    }

    private async Task ProcessJobAsync(JobItem jobItem, CancellationToken cancellationToken)
    {
        var jobId = jobItem.Id;
        
        try
        {
            // Update status to running
            if (_jobStatuses.TryGetValue(jobId, out var jobInfo))
            {
                jobInfo.Status = JobStatus.Running;
                jobInfo.StartedAt = DateTime.UtcNow;
            }

            _logger.LogInformation("Starting job {JobId} ({JobName})", jobId, jobItem.Name);

            using var scope = _serviceProvider.CreateScope();
            var result = await jobItem.JobFunc(scope.ServiceProvider, cancellationToken);

            // Update status to completed
            if (_jobStatuses.TryGetValue(jobId, out jobInfo))
            {
                jobInfo.Status = JobStatus.Completed;
                jobInfo.CompletedAt = DateTime.UtcNow;
                jobInfo.Result = result;
            }

            _logger.LogInformation("Completed job {JobId} ({JobName})", jobId, jobItem.Name);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Job {JobId} ({JobName}) was cancelled", jobId, jobItem.Name);
            
            if (_jobStatuses.TryGetValue(jobId, out var jobInfo))
            {
                jobInfo.Status = JobStatus.Cancelled;
                jobInfo.CompletedAt = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} ({JobName}) failed", jobId, jobItem.Name);
            
            if (_jobStatuses.TryGetValue(jobId, out var jobInfo))
            {
                jobInfo.Status = JobStatus.Failed;
                jobInfo.CompletedAt = DateTime.UtcNow;
                jobInfo.ErrorMessage = ex.Message;
            }
        }
    }

    public override void Dispose()
    {
        _semaphore?.Dispose();
        base.Dispose();
    }
}

// Supporting classes
public class JobItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Func<IServiceProvider, CancellationToken, Task<object?>> JobFunc { get; set; } = null!;
    public DateTime EnqueuedAt { get; set; }
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = 3;
}

public class JobInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public DateTime EnqueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public object? Result { get; set; }
    public string? ErrorMessage { get; set; }
    
    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue 
        ? CompletedAt.Value - StartedAt.Value 
        : null;
}

public enum JobStatus
{
    NotFound,
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}
