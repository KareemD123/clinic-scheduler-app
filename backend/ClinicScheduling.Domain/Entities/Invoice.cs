namespace ClinicScheduling.Domain.Entities;

public class Invoice
{
    public Guid Id { get; set; }
    public Guid AppointmentId { get; set; }
    public Guid PatientId { get; set; }
    public decimal Amount { get; set; }
    public decimal Tax { get; set; }
    public decimal TotalAmount { get; set; }
    public InvoiceStatus Status { get; set; }
    public List<InvoiceItem> Items { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public DateTime DueDate { get; set; }
}

public class InvoiceItem
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public enum InvoiceStatus
{
    Pending,
    Paid,
    Overdue,
    Cancelled
}
