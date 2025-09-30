namespace ClinicScheduling.Domain.Entities;

public class Doctor
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public decimal ConsultationFee { get; set; }
    public List<DoctorAvailability> Availability { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class DoctorAvailability
{
    public int DayOfWeek { get; set; } // 0=Sunday, 1=Monday, etc.
    public string StartTime { get; set; } = string.Empty; // "09:00"
    public string EndTime { get; set; } = string.Empty;   // "17:00"
}
