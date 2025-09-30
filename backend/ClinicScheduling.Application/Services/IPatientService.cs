using ClinicScheduling.Application.DTOs.Common;
using ClinicScheduling.Application.DTOs.Patients;

namespace ClinicScheduling.Application.Services;

public interface IPatientService
{
    Task<ApiResponse<PatientResponse>> CreatePatientAsync(CreatePatientRequest request);
    Task<ApiResponse<List<PatientListResponse>>> GetAllPatientsAsync();
    Task<ApiResponse<PatientResponse>> GetPatientByIdAsync(Guid id);
}
