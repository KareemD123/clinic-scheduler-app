namespace ClinicScheduling.Application.DTOs.Appointments;

public class CreateAppointmentRequest
{
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public DateTime AppointmentDateTime { get; set; }
    public int Duration { get; set; } = 30;
    public string Reason { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
