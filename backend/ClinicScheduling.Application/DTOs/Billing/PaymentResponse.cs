namespace ClinicScheduling.Application.DTOs.Billing;

public class PaymentResponse
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public PaymentReceiptDto? Receipt { get; set; }
}

public class PaymentReceiptDto
{
    public string ReceiptNumber { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}

public class ProcessPaymentRequest
{
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "credit_card";
    public CardDetailsDto? CardDetails { get; set; }
}

public class CardDetailsDto
{
    public string Last4 { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
}
