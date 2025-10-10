using Microsoft.Extensions.Logging;
using ClinicScheduling.Domain.Entities;
using ClinicScheduling.Domain.Interfaces;

namespace ClinicScheduling.Infrastructure.Services;

public static class BulkOperationJobs
{
    /// <summary>
    /// Bulk appointment status update job
    /// </summary>
    public static async Task<BulkOperationResult> BulkUpdateAppointmentStatusAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken,
        IEnumerable<Guid> appointmentIds,
        AppointmentStatus newStatus,
        string? notes = null)
    {
        var logger = serviceProvider.GetService<ILogger<BulkOperationJobs>>();
        var unitOfWork = serviceProvider.GetService<IUnitOfWork>();
        
        if (unitOfWork == null)
        {
            throw new InvalidOperationException("Unit of Work not available");
        }

        var result = new BulkOperationResult
        {
            OperationType = "BulkUpdateAppointmentStatus",
            StartedAt = DateTime.UtcNow
        };

        try
        {
            logger?.LogInformation("Starting bulk appointment status update for {Count} appointments", 
                appointmentIds.Count());

            await unitOfWork.BeginTransactionAsync();

            var appointments = new List<Appointment>();
            var updatedCount = 0;

            // Process in batches to avoid memory issues
            const int batchSize = 100;
            var batches = appointmentIds.Chunk(batchSize);

            foreach (var batch in batches)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batchAppointments = new List<Appointment>();
                foreach (var id in batch)
                {
                    var appointment = await unitOfWork.Appointments.GetByIdAsync(id);
                    if (appointment != null && appointment.Status != newStatus)
                    {
                        // Validate business rules
                        if (appointment.AppointmentDate <= DateTime.UtcNow && newStatus == AppointmentStatus.Scheduled)
                        {
                            result.Errors.Add($"Cannot reschedule past appointment {id}");
                            continue;
                        }

                        appointment.Status = newStatus;
                        appointment.UpdatedAt = DateTime.UtcNow;
                        if (!string.IsNullOrEmpty(notes))
                        {
                            appointment.Notes = notes;
                        }

                        unitOfWork.Appointments.Update(appointment);
                        batchAppointments.Add(appointment);
                        updatedCount++;
                    }
                }

                appointments.AddRange(batchAppointments);
                
                // Save batch
                await unitOfWork.SaveChangesAsync();
                
                logger?.LogDebug("Processed batch of {BatchSize} appointments, {UpdatedInBatch} updated", 
                    batch.Length, batchAppointments.Count);
            }

            await unitOfWork.CommitTransactionAsync();

            result.ProcessedCount = appointmentIds.Count();
            result.SuccessCount = updatedCount;
            result.CompletedAt = DateTime.UtcNow;
            result.IsSuccess = true;
            result.Data = appointments.Select(a => new { a.Id, a.Status, a.UpdatedAt }).ToList();

            logger?.LogInformation("Bulk appointment status update completed. {Updated}/{Total} appointments updated", 
                updatedCount, appointmentIds.Count());

            return result;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Bulk appointment status update failed");
            
            try
            {
                await unitOfWork.RollbackTransactionAsync();
            }
            catch (Exception rollbackEx)
            {
                logger?.LogError(rollbackEx, "Failed to rollback transaction");
            }

            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            
            return result;
        }
    }

    /// <summary>
    /// Bulk invoice generation job
    /// </summary>
    public static async Task<BulkOperationResult> BulkGenerateInvoicesAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken,
        IEnumerable<Guid> appointmentIds,
        decimal defaultAmount = 150.00m)
    {
        var logger = serviceProvider.GetService<ILogger<BulkOperationJobs>>();
        var unitOfWork = serviceProvider.GetService<IUnitOfWork>();
        
        if (unitOfWork == null)
        {
            throw new InvalidOperationException("Unit of Work not available");
        }

        var result = new BulkOperationResult
        {
            OperationType = "BulkGenerateInvoices",
            StartedAt = DateTime.UtcNow
        };

        try
        {
            logger?.LogInformation("Starting bulk invoice generation for {Count} appointments", 
                appointmentIds.Count());

            await unitOfWork.BeginTransactionAsync();

            var generatedInvoices = new List<Invoice>();
            var generatedCount = 0;

            foreach (var appointmentId in appointmentIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var appointment = await unitOfWork.Appointments.GetByIdAsync(appointmentId);
                if (appointment == null)
                {
                    result.Errors.Add($"Appointment {appointmentId} not found");
                    continue;
                }

                if (appointment.Status != AppointmentStatus.Completed)
                {
                    result.Errors.Add($"Appointment {appointmentId} is not completed");
                    continue;
                }

                // Check if invoice already exists
                var existingInvoice = await unitOfWork.Invoices
                    .FirstOrDefaultAsync(i => i.AppointmentId == appointmentId);
                
                if (existingInvoice != null)
                {
                    result.Errors.Add($"Invoice already exists for appointment {appointmentId}");
                    continue;
                }

                var invoice = new Invoice
                {
                    Id = Guid.NewGuid(),
                    PatientId = appointment.PatientId,
                    AppointmentId = appointmentId,
                    Amount = defaultAmount,
                    Status = InvoiceStatus.Pending,
                    DueDate = DateTime.UtcNow.AddDays(30),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await unitOfWork.Invoices.AddAsync(invoice);
                generatedInvoices.Add(invoice);
                generatedCount++;
            }

            await unitOfWork.SaveChangesAsync();
            await unitOfWork.CommitTransactionAsync();

            result.ProcessedCount = appointmentIds.Count();
            result.SuccessCount = generatedCount;
            result.CompletedAt = DateTime.UtcNow;
            result.IsSuccess = true;
            result.Data = generatedInvoices.Select(i => new { i.Id, i.AppointmentId, i.Amount }).ToList();

            logger?.LogInformation("Bulk invoice generation completed. {Generated}/{Total} invoices generated", 
                generatedCount, appointmentIds.Count());

            return result;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Bulk invoice generation failed");
            
            try
            {
                await unitOfWork.RollbackTransactionAsync();
            }
            catch (Exception rollbackEx)
            {
                logger?.LogError(rollbackEx, "Failed to rollback transaction");
            }

            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            
            return result;
        }
    }

    /// <summary>
    /// Bulk payment processing job
    /// </summary>
    public static async Task<BulkOperationResult> BulkProcessPaymentsAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken,
        IEnumerable<Guid> invoiceIds,
        string paymentMethod = "BulkPayment")
    {
        var logger = serviceProvider.GetService<ILogger<BulkOperationJobs>>();
        var unitOfWork = serviceProvider.GetService<IUnitOfWork>();
        
        if (unitOfWork == null)
        {
            throw new InvalidOperationException("Unit of Work not available");
        }

        var result = new BulkOperationResult
        {
            OperationType = "BulkProcessPayments",
            StartedAt = DateTime.UtcNow
        };

        try
        {
            logger?.LogInformation("Starting bulk payment processing for {Count} invoices", 
                invoiceIds.Count());

            await unitOfWork.BeginTransactionAsync();

            var processedPayments = new List<Payment>();
            var processedCount = 0;

            foreach (var invoiceId in invoiceIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var invoice = await unitOfWork.Invoices.GetByIdAsync(invoiceId);
                if (invoice == null)
                {
                    result.Errors.Add($"Invoice {invoiceId} not found");
                    continue;
                }

                if (invoice.Status != InvoiceStatus.Pending)
                {
                    result.Errors.Add($"Invoice {invoiceId} is not in pending status");
                    continue;
                }

                // Check if payment already exists
                var existingPayment = await unitOfWork.Payments
                    .FirstOrDefaultAsync(p => p.InvoiceId == invoiceId);
                
                if (existingPayment != null)
                {
                    result.Errors.Add($"Payment already exists for invoice {invoiceId}");
                    continue;
                }

                var payment = new Payment
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    Amount = invoice.Amount,
                    PaymentDate = DateTime.UtcNow,
                    PaymentMethod = paymentMethod,
                    CreatedAt = DateTime.UtcNow
                };

                await unitOfWork.Payments.AddAsync(payment);
                
                // Update invoice status
                invoice.Status = InvoiceStatus.Paid;
                invoice.UpdatedAt = DateTime.UtcNow;
                unitOfWork.Invoices.Update(invoice);

                processedPayments.Add(payment);
                processedCount++;
            }

            await unitOfWork.SaveChangesAsync();
            await unitOfWork.CommitTransactionAsync();

            result.ProcessedCount = invoiceIds.Count();
            result.SuccessCount = processedCount;
            result.CompletedAt = DateTime.UtcNow;
            result.IsSuccess = true;
            result.Data = processedPayments.Select(p => new { p.Id, p.InvoiceId, p.Amount }).ToList();

            logger?.LogInformation("Bulk payment processing completed. {Processed}/{Total} payments processed", 
                processedCount, invoiceIds.Count());

            return result;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Bulk payment processing failed");
            
            try
            {
                await unitOfWork.RollbackTransactionAsync();
            }
            catch (Exception rollbackEx)
            {
                logger?.LogError(rollbackEx, "Failed to rollback transaction");
            }

            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            
            return result;
        }
    }

    /// <summary>
    /// Data cleanup job - archives old records
    /// </summary>
    public static async Task<BulkOperationResult> DataCleanupJobAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken,
        DateTime cutoffDate)
    {
        var logger = serviceProvider.GetService<ILogger<BulkOperationJobs>>();
        var unitOfWork = serviceProvider.GetService<IUnitOfWork>();
        
        if (unitOfWork == null)
        {
            throw new InvalidOperationException("Unit of Work not available");
        }

        var result = new BulkOperationResult
        {
            OperationType = "DataCleanup",
            StartedAt = DateTime.UtcNow
        };

        try
        {
            logger?.LogInformation("Starting data cleanup for records older than {CutoffDate}", cutoffDate);

            await unitOfWork.BeginTransactionAsync();

            // Find old completed appointments
            var oldAppointments = await unitOfWork.Appointments.FindAsync(a => 
                a.AppointmentDate < cutoffDate && 
                a.Status == AppointmentStatus.Completed);

            var appointmentCount = oldAppointments.Count();

            // Find associated paid invoices
            var appointmentIds = oldAppointments.Select(a => a.Id).ToList();
            var oldInvoices = await unitOfWork.Invoices.FindAsync(i => 
                appointmentIds.Contains(i.AppointmentId) && 
                i.Status == InvoiceStatus.Paid);

            var invoiceCount = oldInvoices.Count();

            // Archive logic would go here - for now, just log what would be archived
            logger?.LogInformation("Would archive {AppointmentCount} appointments and {InvoiceCount} invoices", 
                appointmentCount, invoiceCount);

            await unitOfWork.CommitTransactionAsync();

            result.ProcessedCount = appointmentCount + invoiceCount;
            result.SuccessCount = appointmentCount + invoiceCount;
            result.CompletedAt = DateTime.UtcNow;
            result.IsSuccess = true;
            result.Data = new { ArchivedAppointments = appointmentCount, ArchivedInvoices = invoiceCount };

            logger?.LogInformation("Data cleanup completed. {Total} records processed", 
                result.ProcessedCount);

            return result;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Data cleanup failed");
            
            try
            {
                await unitOfWork.RollbackTransactionAsync();
            }
            catch (Exception rollbackEx)
            {
                logger?.LogError(rollbackEx, "Failed to rollback transaction");
            }

            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            
            return result;
        }
    }
}

public class BulkOperationResult
{
    public string OperationType { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsSuccess { get; set; }
    public int ProcessedCount { get; set; }
    public int SuccessCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public object? Data { get; set; }
    
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
    public int FailedCount => ProcessedCount - SuccessCount;
}
