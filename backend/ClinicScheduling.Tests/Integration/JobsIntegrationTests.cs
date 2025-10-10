using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using ClinicScheduling.API.Controllers;
using ClinicScheduling.Domain.Entities;
using ClinicScheduling.Infrastructure.Services;

namespace ClinicScheduling.Tests.Integration;

public class JobsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public JobsIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetActiveJobs_ShouldReturnOkResponse()
    {
        // Act
        var response = await _client.GetAsync("/api/jobs/active");

        // Assert
        response.Should().NotBeNull();
        response.IsSuccessStatusCode.Should().BeTrue();
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNull();
        
        // Should return an array (even if empty)
        var jobs = JsonSerializer.Deserialize<JobInfo[]>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        jobs.Should().NotBeNull();
    }

    [Fact]
    public async Task GetJobStatus_NonExistentJob_ShouldReturnNotFound()
    {
        // Arrange
        var nonExistentJobId = Guid.NewGuid().ToString();

        // Act
        var response = await _client.GetAsync($"/api/jobs/{nonExistentJobId}/status");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TestJob_ShouldEnqueueSuccessfully()
    {
        // Act
        var response = await _client.PostAsync("/api/jobs/test-job", null);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccessStatusCode.Should().BeTrue();
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("JobId");
        content.Should().Contain("Test job queued successfully");
    }

    [Fact]
    public async Task BulkUpdateAppointmentStatus_ValidRequest_ShouldEnqueueJob()
    {
        // Arrange
        var request = new BulkUpdateAppointmentRequest
        {
            AppointmentIds = new[] { Guid.NewGuid(), Guid.NewGuid() },
            NewStatus = AppointmentStatus.Confirmed,
            Notes = "Integration test bulk update"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/jobs/bulk-update-appointments", request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccessStatusCode.Should().BeTrue();
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("JobId");
        content.Should().Contain("Bulk update job queued successfully");
    }

    [Fact]
    public async Task BulkGenerateInvoices_ValidRequest_ShouldEnqueueJob()
    {
        // Arrange
        var request = new BulkGenerateInvoicesRequest
        {
            AppointmentIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() },
            DefaultAmount = 175.00m
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/jobs/bulk-generate-invoices", request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccessStatusCode.Should().BeTrue();
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("JobId");
        content.Should().Contain("Bulk invoice generation job queued successfully");
    }

    [Fact]
    public async Task BulkProcessPayments_ValidRequest_ShouldEnqueueJob()
    {
        // Arrange
        var request = new BulkProcessPaymentsRequest
        {
            InvoiceIds = new[] { Guid.NewGuid(), Guid.NewGuid() },
            PaymentMethod = "IntegrationTestPayment"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/jobs/bulk-process-payments", request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccessStatusCode.Should().BeTrue();
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("JobId");
        content.Should().Contain("Bulk payment processing job queued successfully");
    }

    [Fact]
    public async Task ScheduleDataCleanup_ValidRequest_ShouldEnqueueJob()
    {
        // Arrange
        var request = new DataCleanupRequest
        {
            CutoffDate = DateTime.UtcNow.AddMonths(-12)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/jobs/schedule-cleanup", request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccessStatusCode.Should().BeTrue();
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("JobId");
        content.Should().Contain("Data cleanup job queued successfully");
    }

    [Fact]
    public async Task BulkUpdateAppointmentStatus_EmptyAppointmentIds_ShouldStillSucceed()
    {
        // Arrange
        var request = new BulkUpdateAppointmentRequest
        {
            AppointmentIds = new List<Guid>(),
            NewStatus = AppointmentStatus.Cancelled,
            Notes = "Empty list test"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/jobs/bulk-update-appointments", request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task JobWorkflow_EnqueueAndCheckStatus_ShouldWork()
    {
        // Arrange - Enqueue a test job
        var enqueueResponse = await _client.PostAsync("/api/jobs/test-job", null);
        enqueueResponse.IsSuccessStatusCode.Should().BeTrue();

        var enqueueContent = await enqueueResponse.Content.ReadAsStringAsync();
        var enqueueResult = JsonSerializer.Deserialize<JsonElement>(enqueueContent);
        var jobId = enqueueResult.GetProperty("JobId").GetString();

        jobId.Should().NotBeNullOrEmpty();

        // Act - Check job status (note: the job might complete very quickly)
        var statusResponse = await _client.GetAsync($"/api/jobs/{jobId}/status");

        // Assert - Should get a valid response (job might be completed, running, or queued)
        statusResponse.IsSuccessStatusCode.Should().BeTrue();
        
        var statusContent = await statusResponse.Content.ReadAsStringAsync();
        statusContent.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MultipleJobsEnqueue_ShouldAllSucceed()
    {
        // Arrange
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act - Enqueue multiple jobs concurrently
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(_client.PostAsync("/api/jobs/test-job", null));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        foreach (var response in responses)
        {
            response.IsSuccessStatusCode.Should().BeTrue();
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("JobId");
        }
    }
}

// Custom WebApplicationFactory for testing with in-memory services
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Override services for testing if needed
            // For example, use in-memory database instead of JSON file
            
            // Remove existing background job service registration
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IBackgroundJobService));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add test-specific background job service
            services.AddSingleton<IBackgroundJobService, BackgroundJobService>();
            services.AddHostedService<BackgroundJobService>(provider => 
                (BackgroundJobService)provider.GetRequiredService<IBackgroundJobService>());
        });
    }
}

// Performance test for job processing
public class JobPerformanceTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public JobPerformanceTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task BulkOperations_LargeDataSet_ShouldCompleteWithinReasonableTime()
    {
        // Arrange
        var appointmentIds = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToArray();
        var request = new BulkUpdateAppointmentRequest
        {
            AppointmentIds = appointmentIds,
            NewStatus = AppointmentStatus.Confirmed,
            Notes = "Performance test"
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await _client.PostAsJsonAsync("/api/jobs/bulk-update-appointments", request);

        stopwatch.Stop();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should enqueue within 5 seconds
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("JobId");
    }

    [Fact]
    public async Task ConcurrentJobEnqueuing_ShouldHandleLoad()
    {
        // Arrange
        const int numberOfJobs = 20;
        var tasks = new List<Task<HttpResponseMessage>>();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act - Enqueue many jobs concurrently
        for (int i = 0; i < numberOfJobs; i++)
        {
            var request = new BulkUpdateAppointmentRequest
            {
                AppointmentIds = new[] { Guid.NewGuid() },
                NewStatus = AppointmentStatus.Confirmed,
                Notes = $"Concurrent test job {i}"
            };
            
            tasks.Add(_client.PostAsJsonAsync("/api/jobs/bulk-update-appointments", request));
        }

        var responses = await Task.WhenAll(tasks);
        stopwatch.Stop();

        // Assert
        responses.Should().HaveCount(numberOfJobs);
        responses.Should().OnlyContain(r => r.IsSuccessStatusCode);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000); // Should complete within 10 seconds
    }
}
