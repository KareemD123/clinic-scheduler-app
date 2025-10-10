using Microsoft.AspNetCore.Mvc;
using ClinicScheduling.Domain.Entities;
using ClinicScheduling.Infrastructure.Services;

namespace ClinicScheduling.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly IBackgroundJobService _jobService;
    private readonly ILogger<JobsController> _logger;

    public JobsController(IBackgroundJobService jobService, ILogger<JobsController> logger)
    {
        _jobService = jobService;
        _logger = logger;
    }

    /// <summary>
    /// Get status of a specific job
    /// </summary>
    [HttpGet("{jobId}/status")]
    public async Task<ActionResult<JobStatus>> GetJobStatus(string jobId)
    {
        var status = await _jobService.GetJobStatusAsync(jobId);
        if (status == JobStatus.NotFound)
        {
            return NotFound($"Job {jobId} not found");
        }
        return Ok(status);
    }

    /// <summary>
    /// Get all active jobs
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<JobInfo>>> GetActiveJobs()
    {
        var jobs = await _jobService.GetActiveJobsAsync();
        return Ok(jobs);
    }

    /// <summary>
    /// Bulk update appointment status
    /// </summary>
    [HttpPost("bulk-update-appointments")]
    public async Task<ActionResult<string>> BulkUpdateAppointmentStatus(
        [FromBody] BulkUpdateAppointmentRequest request)
    {
        try
        {
            var jobId = Guid.NewGuid().ToString();
            
            await _jobService.EnqueueAsync<BulkOperationResult>(
                (serviceProvider, cancellationToken) => 
                    BulkOperationJobs.BulkUpdateAppointmentStatusAsync(
                        serviceProvider, 
                        cancellationToken, 
                        request.AppointmentIds, 
                        request.NewStatus, 
                        request.Notes),
                $"BulkUpdateAppointments-{request.AppointmentIds.Count()}-{DateTime.UtcNow:yyyyMMdd-HHmmss}"
            );

            _logger.LogInformation("Bulk appointment update job queued for {Count} appointments", 
                request.AppointmentIds.Count());

            return Ok(new { JobId = jobId, Message = "Bulk update job queued successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue bulk appointment update job");
            return StatusCode(500, "Failed to queue bulk update job");
        }
    }

    /// <summary>
    /// Bulk generate invoices
    /// </summary>
    [HttpPost("bulk-generate-invoices")]
    public async Task<ActionResult<string>> BulkGenerateInvoices(
        [FromBody] BulkGenerateInvoicesRequest request)
    {
        try
        {
            var jobId = Guid.NewGuid().ToString();
            
            await _jobService.EnqueueAsync<BulkOperationResult>(
                (serviceProvider, cancellationToken) => 
                    BulkOperationJobs.BulkGenerateInvoicesAsync(
                        serviceProvider, 
                        cancellationToken, 
                        request.AppointmentIds, 
                        request.DefaultAmount),
                $"BulkGenerateInvoices-{request.AppointmentIds.Count()}-{DateTime.UtcNow:yyyyMMdd-HHmmss}"
            );

            _logger.LogInformation("Bulk invoice generation job queued for {Count} appointments", 
                request.AppointmentIds.Count());

            return Ok(new { JobId = jobId, Message = "Bulk invoice generation job queued successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue bulk invoice generation job");
            return StatusCode(500, "Failed to queue bulk invoice generation job");
        }
    }

    /// <summary>
    /// Bulk process payments
    /// </summary>
    [HttpPost("bulk-process-payments")]
    public async Task<ActionResult<string>> BulkProcessPayments(
        [FromBody] BulkProcessPaymentsRequest request)
    {
        try
        {
            var jobId = Guid.NewGuid().ToString();
            
            await _jobService.EnqueueAsync<BulkOperationResult>(
                (serviceProvider, cancellationToken) => 
                    BulkOperationJobs.BulkProcessPaymentsAsync(
                        serviceProvider, 
                        cancellationToken, 
                        request.InvoiceIds, 
                        request.PaymentMethod),
                $"BulkProcessPayments-{request.InvoiceIds.Count()}-{DateTime.UtcNow:yyyyMMdd-HHmmss}"
            );

            _logger.LogInformation("Bulk payment processing job queued for {Count} invoices", 
                request.InvoiceIds.Count());

            return Ok(new { JobId = jobId, Message = "Bulk payment processing job queued successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue bulk payment processing job");
            return StatusCode(500, "Failed to queue bulk payment processing job");
        }
    }

    /// <summary>
    /// Schedule data cleanup job
    /// </summary>
    [HttpPost("schedule-cleanup")]
    public async Task<ActionResult<string>> ScheduleDataCleanup(
        [FromBody] DataCleanupRequest request)
    {
        try
        {
            var jobId = Guid.NewGuid().ToString();
            
            await _jobService.EnqueueAsync<BulkOperationResult>(
                (serviceProvider, cancellationToken) => 
                    BulkOperationJobs.DataCleanupJobAsync(
                        serviceProvider, 
                        cancellationToken, 
                        request.CutoffDate),
                $"DataCleanup-{request.CutoffDate:yyyyMMdd}-{DateTime.UtcNow:yyyyMMdd-HHmmss}"
            );

            _logger.LogInformation("Data cleanup job queued for cutoff date {CutoffDate}", 
                request.CutoffDate);

            return Ok(new { JobId = jobId, Message = "Data cleanup job queued successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue data cleanup job");
            return StatusCode(500, "Failed to queue data cleanup job");
        }
    }

    /// <summary>
    /// Test job endpoint for demonstration
    /// </summary>
    [HttpPost("test-job")]
    public async Task<ActionResult<string>> TestJob()
    {
        try
        {
            var jobId = Guid.NewGuid().ToString();
            
            await _jobService.EnqueueAsync(
                async (serviceProvider, cancellationToken) =>
                {
                    var logger = serviceProvider.GetService<ILogger<JobsController>>();
                    logger?.LogInformation("Test job is running...");
                    
                    // Simulate some work
                    await Task.Delay(5000, cancellationToken);
                    
                    logger?.LogInformation("Test job completed!");
                },
                "TestJob"
            );

            return Ok(new { JobId = jobId, Message = "Test job queued successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue test job");
            return StatusCode(500, "Failed to queue test job");
        }
    }
}

// Request DTOs
public class BulkUpdateAppointmentRequest
{
    public IEnumerable<Guid> AppointmentIds { get; set; } = new List<Guid>();
    public AppointmentStatus NewStatus { get; set; }
    public string? Notes { get; set; }
}

public class BulkGenerateInvoicesRequest
{
    public IEnumerable<Guid> AppointmentIds { get; set; } = new List<Guid>();
    public decimal DefaultAmount { get; set; } = 150.00m;
}

public class BulkProcessPaymentsRequest
{
    public IEnumerable<Guid> InvoiceIds { get; set; } = new List<Guid>();
    public string PaymentMethod { get; set; } = "BulkPayment";
}

public class DataCleanupRequest
{
    public DateTime CutoffDate { get; set; }
}
