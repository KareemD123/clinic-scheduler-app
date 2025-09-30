using System.Text.Json;
using ClinicScheduling.Domain.Entities;

namespace ClinicScheduling.Infrastructure.Data;

public class JsonDatabase
{
    private readonly string _filePath;
    private DatabaseModel _data;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public JsonDatabase(string filePath = "database.json")
    {
        _filePath = filePath;
        _data = LoadData();
    }

    private DatabaseModel LoadData()
    {
        if (!File.Exists(_filePath))
        {
            var newData = new DatabaseModel();
            // Write the initial empty database file synchronously
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(newData, options);
            File.WriteAllText(_filePath, json);
            return newData;
        }

        var existingJson = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<DatabaseModel>(existingJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new DatabaseModel();
    }

    public async Task SaveData()
    {
        await _semaphore.WaitAsync();
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var json = JsonSerializer.Serialize(_data, options);
            await File.WriteAllTextAsync(_filePath, json);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public DatabaseModel GetData() => _data;
}

public class DatabaseModel
{
    public List<Patient> Patients { get; set; } = new();
    public List<Doctor> Doctors { get; set; } = new();
    public List<Appointment> Appointments { get; set; } = new();
    public List<Invoice> Invoices { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
}
