using ClinicScheduling.Application.DTOs.Billing;
using ClinicScheduling.Application.DTOs.Common;

namespace ClinicScheduling.Application.Services;

public interface IBillingService
{
    Task<ApiResponse<InvoiceResponse>> GenerateInvoiceAsync(CreateInvoiceRequest request);
    Task<ApiResponse<InvoiceResponse>> GetInvoiceByIdAsync(Guid id);
    Task<ApiResponse<PaymentResponse>> ProcessPaymentAsync(ProcessPaymentRequest request);
}
