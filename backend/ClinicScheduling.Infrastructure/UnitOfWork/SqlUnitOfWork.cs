using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ClinicScheduling.Domain.Entities;
using ClinicScheduling.Domain.Interfaces;
using ClinicScheduling.Infrastructure.Data;
using ClinicScheduling.Infrastructure.Repositories;

namespace ClinicScheduling.Infrastructure.UnitOfWork;

public class SqlUnitOfWork : IUnitOfWork, IDisposable
{
    private readonly SqlDbContext _context;
    private IDbContextTransaction? _transaction;
    private bool _disposed = false;

    // Repository instances
    private IRepository<Patient>? _patients;
    private IRepository<Doctor>? _doctors;
    private IRepository<Appointment>? _appointments;
    private IRepository<Invoice>? _invoices;
    private IRepository<Payment>? _payments;

    public SqlUnitOfWork(SqlDbContext context)
    {
        _context = context;
    }

    public IRepository<Patient> Patients => 
        _patients ??= new SqlRepository<Patient>(_context);

    public IRepository<Doctor> Doctors => 
        _doctors ??= new SqlRepository<Doctor>(_context);

    public IRepository<Appointment> Appointments => 
        _appointments ??= new SqlRepository<Appointment>(_context);

    public IRepository<Invoice> Invoices => 
        _invoices ??= new SqlRepository<Invoice>(_context);

    public IRepository<Payment> Payments => 
        _payments ??= new SqlRepository<Payment>(_context);

    public async Task<int> SaveChangesAsync()
    {
        try
        {
            return await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Log the exception
            throw new InvalidOperationException("Failed to save changes to database", ex);
        }
    }

    public async Task BeginTransactionAsync()
    {
        if (_transaction != null)
        {
            throw new InvalidOperationException("Transaction already started");
        }

        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No transaction to commit");
        }

        try
        {
            await SaveChangesAsync();
            await _transaction.CommitAsync();
        }
        catch
        {
            await RollbackTransactionAsync();
            throw;
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("No transaction to rollback");
        }

        try
        {
            await _transaction.RollbackAsync();
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    // Advanced bulk operations
    public async Task<int> BulkUpdateAppointmentsAsync(
        DateTime fromDate, 
        DateTime toDate, 
        AppointmentStatus newStatus, 
        string? notes = null)
    {
        var query = _context.Appointments
            .Where(a => a.AppointmentDate >= fromDate && a.AppointmentDate <= toDate);

        var appointments = await query.ToListAsync();
        
        foreach (var appointment in appointments)
        {
            appointment.Status = newStatus;
            appointment.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(notes))
            {
                appointment.Notes = notes;
            }
        }

        return appointments.Count;
    }

    public async Task<int> BulkGenerateInvoicesAsync(IEnumerable<Guid> appointmentIds, decimal defaultAmount)
    {
        var appointments = await _context.Appointments
            .Where(a => appointmentIds.Contains(a.Id))
            .ToListAsync();

        var invoices = appointments.Select(appointment => new Invoice
        {
            Id = Guid.NewGuid(),
            PatientId = appointment.PatientId,
            AppointmentId = appointment.Id,
            Amount = defaultAmount,
            Status = InvoiceStatus.Pending,
            DueDate = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _context.Invoices.AddRangeAsync(invoices);
        return invoices.Count();
    }

    public async Task<int> ProcessBulkPaymentsAsync(IEnumerable<Guid> invoiceIds, string paymentMethod)
    {
        var invoices = await _context.Invoices
            .Where(i => invoiceIds.Contains(i.Id) && i.Status == InvoiceStatus.Pending)
            .ToListAsync();

        var payments = new List<Payment>();
        
        foreach (var invoice in invoices)
        {
            payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                Amount = invoice.Amount,
                PaymentDate = DateTime.UtcNow,
                PaymentMethod = paymentMethod,
                CreatedAt = DateTime.UtcNow
            });

            invoice.Status = InvoiceStatus.Paid;
            invoice.UpdatedAt = DateTime.UtcNow;
        }

        await _context.Payments.AddRangeAsync(payments);
        return payments.Count;
    }

    // Complex reporting queries
    public async Task<IEnumerable<PatientBillingSummary>> GetPatientBillingSummaryAsync(
        DateTime fromDate, 
        DateTime toDate)
    {
        var query = from patient in _context.Patients
                   join appointment in _context.Appointments on patient.Id equals appointment.PatientId
                   join invoice in _context.Invoices on appointment.Id equals invoice.AppointmentId into invoiceGroup
                   from invoice in invoiceGroup.DefaultIfEmpty()
                   join payment in _context.Payments on invoice.Id equals payment.InvoiceId into paymentGroup
                   from payment in paymentGroup.DefaultIfEmpty()
                   where appointment.AppointmentDate >= fromDate && appointment.AppointmentDate <= toDate
                   group new { patient, appointment, invoice, payment } by new 
                   { 
                       patient.Id, 
                       patient.FirstName, 
                       patient.LastName, 
                       patient.Email 
                   } into grouped
                   select new PatientBillingSummary
                   {
                       PatientId = grouped.Key.Id,
                       PatientName = $"{grouped.Key.FirstName} {grouped.Key.LastName}",
                       Email = grouped.Key.Email,
                       TotalAppointments = grouped.Count(g => g.appointment != null),
                       TotalBilled = grouped.Sum(g => g.invoice != null ? g.invoice.Amount : 0),
                       TotalPaid = grouped.Sum(g => g.payment != null ? g.payment.Amount : 0),
                       OutstandingBalance = grouped.Sum(g => g.invoice != null ? g.invoice.Amount : 0) - 
                                         grouped.Sum(g => g.payment != null ? g.payment.Amount : 0)
                   };

        return await query.ToListAsync();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _transaction?.Dispose();
                _context.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

// DTO for complex reporting
public class PatientBillingSummary
{
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int TotalAppointments { get; set; }
    public decimal TotalBilled { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal OutstandingBalance { get; set; }
}
