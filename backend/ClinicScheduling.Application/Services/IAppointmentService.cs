using ClinicScheduling.Application.DTOs.Appointments;
using ClinicScheduling.Application.DTOs.Common;

namespace ClinicScheduling.Application.Services;

public interface IAppointmentService
{
    Task<ApiResponse<AppointmentResponse>> CreateAppointmentAsync(CreateAppointmentRequest request);
    Task<ApiResponse<List<AppointmentResponse>>> GetAllAppointmentsAsync();
    Task<ApiResponse<AppointmentResponse>> GetAppointmentByIdAsync(Guid id);
    Task<ApiResponse<ScheduleAndBillResponse>> ScheduleAndBillAsync(ScheduleAndBillRequest request);
}
