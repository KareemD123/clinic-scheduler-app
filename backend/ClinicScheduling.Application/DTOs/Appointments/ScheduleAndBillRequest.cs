using ClinicScheduling.Application.DTOs.Billing;

namespace ClinicScheduling.Application.DTOs.Appointments;

public class ScheduleAndBillRequest
{
    public Guid PatientId { get; set; }
    public Guid DoctorId { get; set; }
    public DateTime AppointmentDateTime { get; set; }
    public int Duration { get; set; } = 30;
    public string Reason { get; set; } = string.Empty;
    public bool GenerateInvoice { get; set; } = true;
    public bool ProcessPayment { get; set; } = false;
}

public class ScheduleAndBillResponse
{
    public AppointmentResponse Appointment { get; set; } = new();
    public InvoiceResponse? Invoice { get; set; }
    public PaymentResponse? Payment { get; set; }
}
