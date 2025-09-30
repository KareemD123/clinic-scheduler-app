using ClinicScheduling.Application.DTOs.Appointments;
using ClinicScheduling.Application.DTOs.Billing;
using ClinicScheduling.Application.DTOs.Common;
using ClinicScheduling.Domain.Entities;
using ClinicScheduling.Domain.Interfaces;

namespace ClinicScheduling.Application.Services;

public class AppointmentService : IAppointmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBillingService _billingService;

    public AppointmentService(IUnitOfWork unitOfWork, IBillingService billingService)
    {
        _unitOfWork = unitOfWork;
        _billingService = billingService;
    }

    public async Task<ApiResponse<AppointmentResponse>> CreateAppointmentAsync(CreateAppointmentRequest request)
    {
        try
        {
            var appointment = new Appointment
            {
                PatientId = request.PatientId,
                DoctorId = request.DoctorId,
                AppointmentDateTime = request.AppointmentDateTime,
                Duration = request.Duration,
                Status = AppointmentStatus.Scheduled,
                Reason = request.Reason,
                Notes = request.Notes
            };

            var created = await _unitOfWork.Appointments.AddAsync(appointment);
            await _unitOfWork.SaveChangesAsync();

            var response = await MapToAppointmentResponse(created);

            return new ApiResponse<AppointmentResponse>
            {
                Success = true,
                Data = response,
                Message = "Appointment scheduled successfully"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<AppointmentResponse>
            {
                Success = false,
                Message = $"Error creating appointment: {ex.Message}",
                ErrorCode = "APPOINTMENT_CREATE_ERROR"
            };
        }
    }

    public async Task<ApiResponse<List<AppointmentResponse>>> GetAllAppointmentsAsync()
    {
        try
        {
            var appointments = await _unitOfWork.Appointments.GetAllAsync();
            var appointmentList = new List<AppointmentResponse>();

            foreach (var appointment in appointments)
            {
                appointmentList.Add(await MapToAppointmentResponse(appointment));
            }

            return new ApiResponse<List<AppointmentResponse>>
            {
                Success = true,
                Data = appointmentList,
                Count = appointmentList.Count
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<AppointmentResponse>>
            {
                Success = false,
                Message = $"Error retrieving appointments: {ex.Message}",
                ErrorCode = "APPOINTMENTS_RETRIEVE_ERROR"
            };
        }
    }

    public async Task<ApiResponse<AppointmentResponse>> GetAppointmentByIdAsync(Guid id)
    {
        try
        {
            var appointment = await _unitOfWork.Appointments.GetByIdAsync(id);
            
            if (appointment == null)
            {
                return new ApiResponse<AppointmentResponse>
                {
                    Success = false,
                    Message = "Appointment not found",
                    ErrorCode = "APPOINTMENT_NOT_FOUND"
                };
            }

            return new ApiResponse<AppointmentResponse>
            {
                Success = true,
                Data = await MapToAppointmentResponse(appointment)
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<AppointmentResponse>
            {
                Success = false,
                Message = $"Error retrieving appointment: {ex.Message}",
                ErrorCode = "APPOINTMENT_RETRIEVE_ERROR"
            };
        }
    }

    public async Task<ApiResponse<ScheduleAndBillResponse>> ScheduleAndBillAsync(ScheduleAndBillRequest request)
    {
        await _unitOfWork.BeginTransactionAsync();
        
        try
        {
            // Step 1: Create appointment
            var appointmentRequest = new CreateAppointmentRequest
            {
                PatientId = request.PatientId,
                DoctorId = request.DoctorId,
                AppointmentDateTime = request.AppointmentDateTime,
                Duration = request.Duration,
                Reason = request.Reason
            };

            var appointmentResult = await CreateAppointmentAsync(appointmentRequest);
            
            if (!appointmentResult.Success || appointmentResult.Data == null)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ApiResponse<ScheduleAndBillResponse>
                {
                    Success = false,
                    Message = "Failed to create appointment",
                    ErrorCode = "TRANSACTION_ROLLBACK"
                };
            }

            var response = new ScheduleAndBillResponse
            {
                Appointment = appointmentResult.Data
            };

            // Step 2: Generate invoice if requested
            if (request.GenerateInvoice)
            {
                var invoiceRequest = new CreateInvoiceRequest
                {
                    AppointmentId = appointmentResult.Data.Id
                };

                var invoiceResult = await _billingService.GenerateInvoiceAsync(invoiceRequest);
                
                if (!invoiceResult.Success || invoiceResult.Data == null)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return new ApiResponse<ScheduleAndBillResponse>
                    {
                        Success = false,
                        Message = "Transaction failed: Invoice generation error. All changes have been rolled back.",
                        ErrorCode = "TRANSACTION_ROLLBACK",
                        Errors = invoiceResult.Errors
                    };
                }

                response.Invoice = invoiceResult.Data;
            }

            await _unitOfWork.CommitTransactionAsync();

            return new ApiResponse<ScheduleAndBillResponse>
            {
                Success = true,
                Data = response,
                Message = "Appointment scheduled and invoice generated successfully"
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            return new ApiResponse<ScheduleAndBillResponse>
            {
                Success = false,
                Message = $"Transaction failed: {ex.Message}. All changes have been rolled back.",
                ErrorCode = "TRANSACTION_ROLLBACK"
            };
        }
    }

    private async Task<AppointmentResponse> MapToAppointmentResponse(Appointment appointment)
    {
        var patient = await _unitOfWork.Patients.GetByIdAsync(appointment.PatientId);
        var doctor = await _unitOfWork.Doctors.GetByIdAsync(appointment.DoctorId);

        return new AppointmentResponse
        {
            Id = appointment.Id,
            PatientId = appointment.PatientId,
            PatientName = patient != null ? $"{patient.FirstName} {patient.LastName}" : "Unknown",
            DoctorId = appointment.DoctorId,
            DoctorName = doctor != null ? $"Dr. {doctor.FirstName} {doctor.LastName}" : "Unknown",
            AppointmentDateTime = appointment.AppointmentDateTime,
            Duration = appointment.Duration,
            Status = appointment.Status.ToString().ToLower(),
            Reason = appointment.Reason,
            Notes = appointment.Notes,
            CreatedAt = appointment.CreatedAt
        };
    }
}
