namespace ClinicScheduling.Domain.Entities;

public class Appointment
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public DateTime AppointmentDateTime { get; set; }
    public int Duration { get; set; } // in minutes
    public AppointmentStatus Status { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum AppointmentStatus
{
    Scheduled,
    Completed,
    Cancelled,
    NoShow
}
