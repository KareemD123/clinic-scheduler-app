namespace ClinicScheduling.Domain.Entities;

public class Payment
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public DateTime ProcessedAt { get; set; }
}

public enum PaymentMethod
{
    CreditCard,
    DebitCard,
    Cash,
    Insurance
}

public enum PaymentStatus
{
    Processing,
    Completed,
    Failed,
    Refunded
}
