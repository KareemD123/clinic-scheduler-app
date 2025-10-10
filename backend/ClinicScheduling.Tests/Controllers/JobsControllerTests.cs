using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ClinicScheduling.API.Controllers;
using ClinicScheduling.Domain.Entities;
using ClinicScheduling.Infrastructure.Services;

namespace ClinicScheduling.Tests.Controllers;

public class JobsControllerTests
{
    private readonly Mock<IBackgroundJobService> _mockJobService;
    private readonly Mock<ILogger<JobsController>> _mockLogger;
    private readonly JobsController _controller;

    public JobsControllerTests()
    {
        _mockJobService = new Mock<IBackgroundJobService>();
        _mockLogger = new Mock<ILogger<JobsController>>();
        _controller = new JobsController(_mockJobService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetJobStatus_ExistingJob_ShouldReturnStatus()
    {
        // Arrange
        var jobId = Guid.NewGuid().ToString();
        var expectedStatus = JobStatus.Running;
        
        _mockJobService.Setup(s => s.GetJobStatusAsync(jobId))
            .ReturnsAsync(expectedStatus);

        // Act
        var result = await _controller.GetJobStatus(jobId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(expectedStatus);
    }

    [Fact]
    public async Task GetJobStatus_NonExistentJob_ShouldReturnNotFound()
    {
        // Arrange
        var jobId = Guid.NewGuid().ToString();
        
        _mockJobService.Setup(s => s.GetJobStatusAsync(jobId))
            .ReturnsAsync(JobStatus.NotFound);

        // Act
        var result = await _controller.GetJobStatus(jobId);

        // Assert
        result.Should().NotBeNull();
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().Be($"Job {jobId} not found");
    }

    [Fact]
    public async Task GetActiveJobs_HasActiveJobs_ShouldReturnJobs()
    {
        // Arrange
        var activeJobs = new List<JobInfo>
        {
            new JobInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = "TestJob1",
                Status = JobStatus.Running,
                EnqueuedAt = DateTime.UtcNow.AddMinutes(-5)
            },
            new JobInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = "TestJob2",
                Status = JobStatus.Queued,
                EnqueuedAt = DateTime.UtcNow.AddMinutes(-2)
            }
        };

        _mockJobService.Setup(s => s.GetActiveJobsAsync())
            .ReturnsAsync(activeJobs);

        // Act
        var result = await _controller.GetActiveJobs();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedJobs = okResult.Value.Should().BeAssignableTo<IEnumerable<JobInfo>>().Subject;
        returnedJobs.Should().HaveCount(2);
    }

    [Fact]
    public async Task BulkUpdateAppointmentStatus_ValidRequest_ShouldEnqueueJob()
    {
        // Arrange
        var request = new BulkUpdateAppointmentRequest
        {
            AppointmentIds = new[] { Guid.NewGuid(), Guid.NewGuid() },
            NewStatus = AppointmentStatus.Confirmed,
            Notes = "Bulk update test"
        };

        _mockJobService.Setup(s => s.EnqueueAsync<BulkOperationResult>(
            It.IsAny<Func<IServiceProvider, CancellationToken, Task<BulkOperationResult>>>(),
            It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.BulkUpdateAppointmentStatus(request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<object>().Subject;
        
        // Verify job was enqueued
        _mockJobService.Verify(s => s.EnqueueAsync<BulkOperationResult>(
            It.IsAny<Func<IServiceProvider, CancellationToken, Task<BulkOperationResult>>>(),
            It.Is<string>(name => name.Contains("BulkUpdateAppointments"))), 
            Times.Once);
    }

    [Fact]
    public async Task BulkUpdateAppointmentStatus_EmptyAppointmentIds_ShouldStillEnqueueJob()
    {
        // Arrange
        var request = new BulkUpdateAppointmentRequest
        {
            AppointmentIds = new List<Guid>(),
            NewStatus = AppointmentStatus.Confirmed
        };

        _mockJobService.Setup(s => s.EnqueueAsync<BulkOperationResult>(
            It.IsAny<Func<IServiceProvider, CancellationToken, Task<BulkOperationResult>>>(),
            It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.BulkUpdateAppointmentStatus(request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        
        _mockJobService.Verify(s => s.EnqueueAsync<BulkOperationResult>(
            It.IsAny<Func<IServiceProvider, CancellationToken, Task<BulkOperationResult>>>(),
            It.IsAny<string>()), 
            Times.Once);
    }

    [Fact]
    public async Task BulkGenerateInvoices_ValidRequest_ShouldEnqueueJob()
    {
        // Arrange
        var request = new BulkGenerateInvoicesRequest
        {
            AppointmentIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() },
            DefaultAmount = 200.00m
        };

        _mockJobService.Setup(s => s.EnqueueAsync<BulkOperationResult>(
            It.IsAny<Func<IServiceProvider, CancellationToken, Task<BulkOperationResult>>>(),
            It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.BulkGenerateInvoices(request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        
        _mockJobService.Verify(s => s.EnqueueAsync<BulkOperationResult>(
            It.IsAny<Func<IServiceProvider, CancellationToken, Task<BulkOperationResult>>>(),
            It.Is<string>(name => name.Contains("BulkGenerateInvoices"))), 
            Times.Once);
    }

    [Fact]
    public async Task BulkProcessPayments_ValidRequest_ShouldEnqueueJob()
    {
        // Arrange
        var request = new BulkProcessPaymentsRequest
        {
            InvoiceIds = new[] { Guid.NewGuid(), Guid.NewGuid() },
            PaymentMethod = "CreditCard"
        };

        _mockJobService.Setup(s => s.EnqueueAsync<BulkOperationResult>(
            It.IsAny<Func<IServiceProvider, CancellationToken, Task<BulkOperationResult>>>(),
            It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.BulkProcessPayments(request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        
        _mockJobService.Verify(s => s.EnqueueAsync<BulkOperationResult>(
            It.IsAny<Func<IServiceProvider, CancellationToken, Task<BulkOperationResult>>>(),
            It.Is<string>(name => name.Contains("BulkProcessPayments"))), 
            Times.Once);
    }

    [Fact]
    public async Task ScheduleDataCleanup_ValidRequest_ShouldEnqueueJob()
    {
        // Arrange
        var request = new DataCleanupRequest
        {
            CutoffDate = DateTime.UtcNow.AddMonths(-6)
        };

        _mockJobService.Setup(s => s.EnqueueAsync<BulkOperationResult>(
            It.IsAny<Func<IServiceProvider, CancellationToken, Task<BulkOperationResult>>>(),
            It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ScheduleDataCleanup(request);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        
        _mockJobService.Verify(s => s.EnqueueAsync<BulkOperationResult>(
            It.IsAny<Func<IServiceProvider, CancellationToken, Task<BulkOperationResult>>>(),
            It.Is<string>(name => name.Contains("DataCleanup"))), 
            Times.Once);
    }

    [Fact]
    public async Task TestJob_ShouldEnqueueSuccessfully()
    {
        // Arrange
        _mockJobService.Setup(s => s.EnqueueAsync(
            It.IsAny<Func<IServiceProvider, CancellationToken, Task>>(),
            It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.TestJob();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        
        _mockJobService.Verify(s => s.EnqueueAsync(
            It.IsAny<Func<IServiceProvider, CancellationToken, Task>>(),
            "TestJob"), 
            Times.Once);
    }

    [Fact]
    public async Task BulkUpdateAppointmentStatus_JobServiceThrows_ShouldReturnInternalServerError()
    {
        // Arrange
        var request = new BulkUpdateAppointmentRequest
        {
            AppointmentIds = new[] { Guid.NewGuid() },
            NewStatus = AppointmentStatus.Confirmed
        };

        _mockJobService.Setup(s => s.EnqueueAsync<BulkOperationResult>(
            It.IsAny<Func<IServiceProvider, CancellationToken, Task<BulkOperationResult>>>(),
            It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Job service error"));

        // Act
        var result = await _controller.BulkUpdateAppointmentStatus(request);

        // Assert
        result.Should().NotBeNull();
        var statusCodeResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);
        statusCodeResult.Value.Should().Be("Failed to queue bulk update job");
    }

    [Fact]
    public async Task GetActiveJobs_NoActiveJobs_ShouldReturnEmptyList()
    {
        // Arrange
        _mockJobService.Setup(s => s.GetActiveJobsAsync())
            .ReturnsAsync(new List<JobInfo>());

        // Act
        var result = await _controller.GetActiveJobs();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedJobs = okResult.Value.Should().BeAssignableTo<IEnumerable<JobInfo>>().Subject;
        returnedJobs.Should().BeEmpty();
    }
}
