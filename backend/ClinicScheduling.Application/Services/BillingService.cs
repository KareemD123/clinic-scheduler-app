using ClinicScheduling.Application.DTOs.Billing;
using ClinicScheduling.Application.DTOs.Common;
using ClinicScheduling.Domain.Entities;
using ClinicScheduling.Domain.Interfaces;

namespace ClinicScheduling.Application.Services;

public class BillingService : IBillingService
{
    private readonly IUnitOfWork _unitOfWork;
    private const decimal TaxRate = 0.08m; // 8% tax

    public BillingService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<InvoiceResponse>> GenerateInvoiceAsync(CreateInvoiceRequest request)
    {
        try
        {
            var appointment = await _unitOfWork.Appointments.GetByIdAsync(request.AppointmentId);
            
            if (appointment == null)
            {
                return new ApiResponse<InvoiceResponse>
                {
                    Success = false,
                    Message = "Appointment not found",
                    ErrorCode = "APPOINTMENT_NOT_FOUND"
                };
            }

            var doctor = await _unitOfWork.Doctors.GetByIdAsync(appointment.DoctorId);
            var patient = await _unitOfWork.Patients.GetByIdAsync(appointment.PatientId);

            if (doctor == null || patient == null)
            {
                return new ApiResponse<InvoiceResponse>
                {
                    Success = false,
                    Message = "Doctor or patient not found",
                    ErrorCode = "ENTITY_NOT_FOUND"
                };
            }

            var amount = doctor.ConsultationFee;
            var tax = amount * TaxRate;
            var totalAmount = amount + tax;

            var invoice = new Invoice
            {
                AppointmentId = appointment.Id,
                PatientId = appointment.PatientId,
                Amount = amount,
                Tax = tax,
                TotalAmount = totalAmount,
                Status = InvoiceStatus.Pending,
                Items = new List<InvoiceItem>
                {
                    new InvoiceItem
                    {
                        Description = $"Consultation Fee - Dr. {doctor.FirstName} {doctor.LastName}",
                        Amount = amount
                    }
                },
                GeneratedAt = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(30)
            };

            var created = await _unitOfWork.Invoices.AddAsync(invoice);
            await _unitOfWork.SaveChangesAsync();

            return new ApiResponse<InvoiceResponse>
            {
                Success = true,
                Data = MapToInvoiceResponse(created, patient),
                Message = "Invoice generated successfully"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<InvoiceResponse>
            {
                Success = false,
                Message = $"Error generating invoice: {ex.Message}",
                ErrorCode = "INVOICE_GENERATE_ERROR"
            };
        }
    }

    public async Task<ApiResponse<InvoiceResponse>> GetInvoiceByIdAsync(Guid id)
    {
        try
        {
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(id);
            
            if (invoice == null)
            {
                return new ApiResponse<InvoiceResponse>
                {
                    Success = false,
                    Message = "Invoice not found",
                    ErrorCode = "INVOICE_NOT_FOUND"
                };
            }

            var patient = await _unitOfWork.Patients.GetByIdAsync(invoice.PatientId);

            return new ApiResponse<InvoiceResponse>
            {
                Success = true,
                Data = MapToInvoiceResponse(invoice, patient)
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<InvoiceResponse>
            {
                Success = false,
                Message = $"Error retrieving invoice: {ex.Message}",
                ErrorCode = "INVOICE_RETRIEVE_ERROR"
            };
        }
    }

    public async Task<ApiResponse<PaymentResponse>> ProcessPaymentAsync(ProcessPaymentRequest request)
    {
        try
        {
            var invoice = await _unitOfWork.Invoices.GetByIdAsync(request.InvoiceId);
            
            if (invoice == null)
            {
                return new ApiResponse<PaymentResponse>
                {
                    Success = false,
                    Message = "Invoice not found",
                    ErrorCode = "INVOICE_NOT_FOUND"
                };
            }

            if (invoice.Status == InvoiceStatus.Paid)
            {
                return new ApiResponse<PaymentResponse>
                {
                    Success = false,
                    Message = "Invoice already paid",
                    ErrorCode = "INVOICE_ALREADY_PAID"
                };
            }

            var patient = await _unitOfWork.Patients.GetByIdAsync(invoice.PatientId);

            var payment = new Payment
            {
                InvoiceId = invoice.Id,
                Amount = request.Amount,
                PaymentMethod = MapPaymentMethod(request.PaymentMethod),
                TransactionId = $"txn_{Guid.NewGuid().ToString("N")[..12]}",
                Status = PaymentStatus.Completed,
                ProcessedAt = DateTime.UtcNow
            };

            var created = await _unitOfWork.Payments.AddAsync(payment);

            // Update invoice status
            invoice.Status = InvoiceStatus.Paid;
            await _unitOfWork.Invoices.UpdateAsync(invoice);
            await _unitOfWork.SaveChangesAsync();

            var response = new PaymentResponse
            {
                Id = created.Id,
                InvoiceId = created.InvoiceId,
                Amount = created.Amount,
                PaymentMethod = created.PaymentMethod.ToString().ToLower(),
                TransactionId = created.TransactionId,
                Status = created.Status.ToString().ToLower(),
                ProcessedAt = created.ProcessedAt,
                Receipt = new PaymentReceiptDto
                {
                    ReceiptNumber = $"RCP-{DateTime.UtcNow:yyyyMMdd}-{created.Id.ToString("N")[..3].ToUpper()}",
                    PatientName = patient != null ? $"{patient.FirstName} {patient.LastName}" : "Unknown",
                    Amount = created.Amount,
                    PaymentMethod = request.CardDetails != null 
                        ? $"{request.CardDetails.Brand} ****{request.CardDetails.Last4}" 
                        : created.PaymentMethod.ToString(),
                    Date = created.ProcessedAt
                }
            };

            return new ApiResponse<PaymentResponse>
            {
                Success = true,
                Data = response,
                Message = "Payment processed successfully"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PaymentResponse>
            {
                Success = false,
                Message = $"Error processing payment: {ex.Message}",
                ErrorCode = "PAYMENT_PROCESS_ERROR"
            };
        }
    }

    private PaymentMethod MapPaymentMethod(string paymentMethodString)
    {
        return paymentMethodString.ToLower() switch
        {
            "credit_card" => PaymentMethod.CreditCard,
            "debit_card" => PaymentMethod.DebitCard,
            "cash" => PaymentMethod.Cash,
            "insurance" => PaymentMethod.Insurance,
            _ => PaymentMethod.CreditCard // Default fallback
        };
    }

    private InvoiceResponse MapToInvoiceResponse(Invoice invoice, Patient? patient)
    {
        return new InvoiceResponse
        {
            Id = invoice.Id,
            AppointmentId = invoice.AppointmentId,
            PatientId = invoice.PatientId,
            PatientName = patient != null ? $"{patient.FirstName} {patient.LastName}" : "Unknown",
            Amount = invoice.Amount,
            Tax = invoice.Tax,
            TotalAmount = invoice.TotalAmount,
            Status = invoice.Status.ToString().ToLower(),
            Items = invoice.Items.Select(i => new InvoiceItemDto
            {
                Description = i.Description,
                Amount = i.Amount
            }).ToList(),
            GeneratedAt = invoice.GeneratedAt,
            DueDate = invoice.DueDate
        };
    }
}
