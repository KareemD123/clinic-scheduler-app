using ClinicScheduling.Domain.Entities;
using ClinicScheduling.Domain.Interfaces;
using ClinicScheduling.Infrastructure.Data;
using ClinicScheduling.Infrastructure.Repositories;

namespace ClinicScheduling.Infrastructure.UnitOfWork;

public class JsonUnitOfWork : IUnitOfWork
{
    private readonly JsonDatabase _database;
    private readonly DatabaseModel _originalState;
    private bool _inTransaction;

    public IRepository<Patient> Patients { get; }
    public IRepository<Doctor> Doctors { get; }
    public IRepository<Appointment> Appointments { get; }
    public IRepository<Invoice> Invoices { get; }
    public IRepository<Payment> Payments { get; }

    public JsonUnitOfWork(JsonDatabase database)
    {
        _database = database;
        var data = _database.GetData();
        
        Patients = new JsonRepository<Patient>(_database, data.Patients);
        Doctors = new JsonRepository<Doctor>(_database, data.Doctors);
        Appointments = new JsonRepository<Appointment>(_database, data.Appointments);
        Invoices = new JsonRepository<Invoice>(_database, data.Invoices);
        Payments = new JsonRepository<Payment>(_database, data.Payments);
        
        _originalState = CloneData(data);
    }

    public Task BeginTransactionAsync()
    {
        _inTransaction = true;
        return Task.CompletedTask;
    }

    public async Task CommitTransactionAsync()
    {
        if (!_inTransaction) return;
        await _database.SaveData();
        _inTransaction = false;
    }

    public Task RollbackTransactionAsync()
    {
        if (!_inTransaction) return Task.CompletedTask;
        
        var currentData = _database.GetData();
        currentData.Patients.Clear();
        currentData.Patients.AddRange(_originalState.Patients);
        
        currentData.Doctors.Clear();
        currentData.Doctors.AddRange(_originalState.Doctors);
        
        currentData.Appointments.Clear();
        currentData.Appointments.AddRange(_originalState.Appointments);
        
        currentData.Invoices.Clear();
        currentData.Invoices.AddRange(_originalState.Invoices);
        
        currentData.Payments.Clear();
        currentData.Payments.AddRange(_originalState.Payments);
        
        _inTransaction = false;
        return Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync()
    {
        await _database.SaveData();
        return 1;
    }

    private DatabaseModel CloneData(DatabaseModel data)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        return System.Text.Json.JsonSerializer.Deserialize<DatabaseModel>(json) ?? new DatabaseModel();
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
