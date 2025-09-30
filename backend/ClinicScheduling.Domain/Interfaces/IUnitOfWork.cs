using ClinicScheduling.Domain.Entities;

namespace ClinicScheduling.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IRepository<Patient> Patients { get; }
    IRepository<Doctor> Doctors { get; }
    IRepository<Appointment> Appointments { get; }
    IRepository<Invoice> Invoices { get; }
    IRepository<Payment> Payments { get; }
    
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
    Task<int> SaveChangesAsync();
}
