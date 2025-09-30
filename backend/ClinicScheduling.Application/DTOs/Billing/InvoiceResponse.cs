namespace ClinicScheduling.Application.DTOs.Billing;

public class InvoiceResponse
{
    public Guid Id { get; set; }
    public Guid AppointmentId { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Tax { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<InvoiceItemDto> Items { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public DateTime DueDate { get; set; }
}

public class InvoiceItemDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class CreateInvoiceRequest
{
    public Guid AppointmentId { get; set; }
}
