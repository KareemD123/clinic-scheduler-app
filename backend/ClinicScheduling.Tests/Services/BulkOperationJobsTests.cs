using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ClinicScheduling.Domain.Entities;
using ClinicScheduling.Domain.Interfaces;
using ClinicScheduling.Infrastructure.Services;

namespace ClinicScheduling.Tests.Services;

public class BulkOperationJobsTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IRepository<Appointment>> _mockAppointmentRepo;
    private readonly Mock<IRepository<Invoice>> _mockInvoiceRepo;
    private readonly Mock<IRepository<Payment>> _mockPaymentRepo;
    private readonly Mock<ILogger<BulkOperationJobs>> _mockLogger;
    private readonly ServiceProvider _serviceProvider;

    public BulkOperationJobsTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockAppointmentRepo = new Mock<IRepository<Appointment>>();
        _mockInvoiceRepo = new Mock<IRepository<Invoice>>();
        _mockPaymentRepo = new Mock<IRepository<Payment>>();
        _mockLogger = new Mock<ILogger<BulkOperationJobs>>();

        _mockUnitOfWork.Setup(u => u.Appointments).Returns(_mockAppointmentRepo.Object);
        _mockUnitOfWork.Setup(u => u.Invoices).Returns(_mockInvoiceRepo.Object);
        _mockUnitOfWork.Setup(u => u.Payments).Returns(_mockPaymentRepo.Object);

        var services = new ServiceCollection();
        services.AddSingleton(_mockUnitOfWork.Object);
        services.AddSingleton(_mockLogger.Object);
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task BulkUpdateAppointmentStatusAsync_ValidAppointments_ShouldUpdateSuccessfully()
    {
        // Arrange
        var appointmentIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var newStatus = AppointmentStatus.Confirmed;
        var notes = "Bulk update test";

        var appointments = appointmentIds.Select(id => new Appointment
        {
            Id = id,
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            AppointmentDate = DateTime.UtcNow.AddDays(1),
            Duration = 30,
            Status = AppointmentStatus.Scheduled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ToList();

        foreach (var appointment in appointments)
        {
            _mockAppointmentRepo.Setup(r => r.GetByIdAsync(appointment.Id))
                .ReturnsAsync(appointment);
        }

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(appointments.Count);

        // Act
        var result = await BulkOperationJobs.BulkUpdateAppointmentStatusAsync(
            _serviceProvider,
            CancellationToken.None,
            appointmentIds,
            newStatus,
            notes);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.ProcessedCount.Should().Be(3);
        result.SuccessCount.Should().Be(3);
        result.OperationType.Should().Be("BulkUpdateAppointmentStatus");
        result.Errors.Should().BeEmpty();

        // Verify all appointments were updated
        foreach (var appointment in appointments)
        {
            appointment.Status.Should().Be(newStatus);
            appointment.Notes.Should().Be(notes);
        }

        _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
        _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(), Times.Once);
        _mockAppointmentRepo.Verify(r => r.Update(It.IsAny<Appointment>()), Times.Exactly(3));
    }

    [Fact]
    public async Task BulkUpdateAppointmentStatusAsync_PastAppointments_ShouldSkipInvalidOnes()
    {
        // Arrange
        var appointmentIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var newStatus = AppointmentStatus.Scheduled;

        var appointments = new List<Appointment>
        {
            new Appointment
            {
                Id = appointmentIds[0],
                PatientId = Guid.NewGuid(),
                DoctorId = Guid.NewGuid(),
                AppointmentDate = DateTime.UtcNow.AddDays(-1), // Past appointment
                Duration = 30,
                Status = AppointmentStatus.Completed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Appointment
            {
                Id = appointmentIds[1],
                PatientId = Guid.NewGuid(),
                DoctorId = Guid.NewGuid(),
                AppointmentDate = DateTime.UtcNow.AddDays(1), // Future appointment
                Duration = 30,
                Status = AppointmentStatus.Confirmed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _mockAppointmentRepo.Setup(r => r.GetByIdAsync(appointmentIds[0]))
            .ReturnsAsync(appointments[0]);
        _mockAppointmentRepo.Setup(r => r.GetByIdAsync(appointmentIds[1]))
            .ReturnsAsync(appointments[1]);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await BulkOperationJobs.BulkUpdateAppointmentStatusAsync(
            _serviceProvider,
            CancellationToken.None,
            appointmentIds,
            newStatus);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.ProcessedCount.Should().Be(2);
        result.SuccessCount.Should().Be(1); // Only one valid update
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Contain("Cannot reschedule past appointment");
    }

    [Fact]
    public async Task BulkGenerateInvoicesAsync_CompletedAppointments_ShouldGenerateInvoices()
    {
        // Arrange
        var appointmentIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var defaultAmount = 200.00m;

        var appointments = appointmentIds.Select(id => new Appointment
        {
            Id = id,
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            AppointmentDate = DateTime.UtcNow.AddDays(-1),
            Duration = 30,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ToList();

        foreach (var appointment in appointments)
        {
            _mockAppointmentRepo.Setup(r => r.GetByIdAsync(appointment.Id))
                .ReturnsAsync(appointment);
        }

        // No existing invoices
        _mockInvoiceRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Invoice, bool>>>()))
            .ReturnsAsync((Invoice?)null);

        _mockInvoiceRepo.Setup(r => r.AddAsync(It.IsAny<Invoice>()))
            .ReturnsAsync((Invoice invoice) => invoice);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        var result = await BulkOperationJobs.BulkGenerateInvoicesAsync(
            _serviceProvider,
            CancellationToken.None,
            appointmentIds,
            defaultAmount);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.ProcessedCount.Should().Be(2);
        result.SuccessCount.Should().Be(2);
        result.OperationType.Should().Be("BulkGenerateInvoices");
        result.Errors.Should().BeEmpty();

        _mockInvoiceRepo.Verify(r => r.AddAsync(It.Is<Invoice>(i => i.Amount == defaultAmount)), Times.Exactly(2));
        _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
        _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(), Times.Once);
    }

    [Fact]
    public async Task BulkGenerateInvoicesAsync_NonCompletedAppointments_ShouldSkipThem()
    {
        // Arrange
        var appointmentIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var defaultAmount = 150.00m;

        var appointments = new List<Appointment>
        {
            new Appointment
            {
                Id = appointmentIds[0],
                PatientId = Guid.NewGuid(),
                DoctorId = Guid.NewGuid(),
                AppointmentDate = DateTime.UtcNow.AddDays(-1),
                Duration = 30,
                Status = AppointmentStatus.Scheduled, // Not completed
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Appointment
            {
                Id = appointmentIds[1],
                PatientId = Guid.NewGuid(),
                DoctorId = Guid.NewGuid(),
                AppointmentDate = DateTime.UtcNow.AddDays(-1),
                Duration = 30,
                Status = AppointmentStatus.Completed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _mockAppointmentRepo.Setup(r => r.GetByIdAsync(appointmentIds[0]))
            .ReturnsAsync(appointments[0]);
        _mockAppointmentRepo.Setup(r => r.GetByIdAsync(appointmentIds[1]))
            .ReturnsAsync(appointments[1]);

        _mockInvoiceRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Invoice, bool>>>()))
            .ReturnsAsync((Invoice?)null);

        _mockInvoiceRepo.Setup(r => r.AddAsync(It.IsAny<Invoice>()))
            .ReturnsAsync((Invoice invoice) => invoice);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await BulkOperationJobs.BulkGenerateInvoicesAsync(
            _serviceProvider,
            CancellationToken.None,
            appointmentIds,
            defaultAmount);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.ProcessedCount.Should().Be(2);
        result.SuccessCount.Should().Be(1); // Only one completed appointment
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Contain("is not completed");

        _mockInvoiceRepo.Verify(r => r.AddAsync(It.IsAny<Invoice>()), Times.Once);
    }

    [Fact]
    public async Task BulkProcessPaymentsAsync_PendingInvoices_ShouldProcessPayments()
    {
        // Arrange
        var invoiceIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var paymentMethod = "CreditCard";

        var invoices = invoiceIds.Select(id => new Invoice
        {
            Id = id,
            PatientId = Guid.NewGuid(),
            AppointmentId = Guid.NewGuid(),
            Amount = 150.00m,
            Status = InvoiceStatus.Pending,
            DueDate = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ToList();

        foreach (var invoice in invoices)
        {
            _mockInvoiceRepo.Setup(r => r.GetByIdAsync(invoice.Id))
                .ReturnsAsync(invoice);
        }

        // No existing payments
        _mockPaymentRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Payment, bool>>>()))
            .ReturnsAsync((Payment?)null);

        _mockPaymentRepo.Setup(r => r.AddAsync(It.IsAny<Payment>()))
            .ReturnsAsync((Payment payment) => payment);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        var result = await BulkOperationJobs.BulkProcessPaymentsAsync(
            _serviceProvider,
            CancellationToken.None,
            invoiceIds,
            paymentMethod);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.ProcessedCount.Should().Be(2);
        result.SuccessCount.Should().Be(2);
        result.OperationType.Should().Be("BulkProcessPayments");
        result.Errors.Should().BeEmpty();

        // Verify payments were created
        _mockPaymentRepo.Verify(r => r.AddAsync(It.Is<Payment>(p => p.PaymentMethod == paymentMethod)), Times.Exactly(2));
        
        // Verify invoices were updated to paid status
        foreach (var invoice in invoices)
        {
            invoice.Status.Should().Be(InvoiceStatus.Paid);
        }

        _mockInvoiceRepo.Verify(r => r.Update(It.IsAny<Invoice>()), Times.Exactly(2));
    }

    [Fact]
    public async Task BulkOperationJob_TransactionFailure_ShouldRollback()
    {
        // Arrange
        var appointmentIds = new[] { Guid.NewGuid() };
        var newStatus = AppointmentStatus.Confirmed;

        var appointment = new Appointment
        {
            Id = appointmentIds[0],
            PatientId = Guid.NewGuid(),
            DoctorId = Guid.NewGuid(),
            AppointmentDate = DateTime.UtcNow.AddDays(1),
            Duration = 30,
            Status = AppointmentStatus.Scheduled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockAppointmentRepo.Setup(r => r.GetByIdAsync(appointment.Id))
            .ReturnsAsync(appointment);

        // Simulate save failure
        _mockUnitOfWork.Setup(u => u.SaveChangesAsync())
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await BulkOperationJobs.BulkUpdateAppointmentStatusAsync(
            _serviceProvider,
            CancellationToken.None,
            appointmentIds,
            newStatus);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Database error");

        _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
        _mockUnitOfWork.Verify(u => u.RollbackTransactionAsync(), Times.Once);
        _mockUnitOfWork.Verify(u => u.CommitTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task DataCleanupJobAsync_OldRecords_ShouldIdentifyForCleanup()
    {
        // Arrange
        var cutoffDate = DateTime.UtcNow.AddMonths(-6);
        
        var oldAppointments = new List<Appointment>
        {
            new Appointment
            {
                Id = Guid.NewGuid(),
                PatientId = Guid.NewGuid(),
                DoctorId = Guid.NewGuid(),
                AppointmentDate = cutoffDate.AddDays(-30),
                Duration = 30,
                Status = AppointmentStatus.Completed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Appointment
            {
                Id = Guid.NewGuid(),
                PatientId = Guid.NewGuid(),
                DoctorId = Guid.NewGuid(),
                AppointmentDate = cutoffDate.AddDays(-60),
                Duration = 30,
                Status = AppointmentStatus.Completed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        var oldInvoices = new List<Invoice>
        {
            new Invoice
            {
                Id = Guid.NewGuid(),
                PatientId = Guid.NewGuid(),
                AppointmentId = oldAppointments[0].Id,
                Amount = 150.00m,
                Status = InvoiceStatus.Paid,
                DueDate = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _mockAppointmentRepo.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Appointment, bool>>>()))
            .ReturnsAsync(oldAppointments);

        _mockInvoiceRepo.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Invoice, bool>>>()))
            .ReturnsAsync(oldInvoices);

        _mockUnitOfWork.Setup(u => u.SaveChangesAsync()).ReturnsAsync(0);

        // Act
        var result = await BulkOperationJobs.DataCleanupJobAsync(
            _serviceProvider,
            CancellationToken.None,
            cutoffDate);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.ProcessedCount.Should().Be(3); // 2 appointments + 1 invoice
        result.SuccessCount.Should().Be(3);
        result.OperationType.Should().Be("DataCleanup");
    }
}
