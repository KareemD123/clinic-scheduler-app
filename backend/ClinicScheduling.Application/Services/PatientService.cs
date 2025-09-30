using ClinicScheduling.Application.DTOs.Common;
using ClinicScheduling.Application.DTOs.Patients;
using ClinicScheduling.Domain.Entities;
using ClinicScheduling.Domain.Interfaces;

namespace ClinicScheduling.Application.Services;

public class PatientService : IPatientService
{
    private readonly IUnitOfWork _unitOfWork;

    public PatientService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<PatientResponse>> CreatePatientAsync(CreatePatientRequest request)
    {
        try
        {
            var patient = new Patient
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                DateOfBirth = request.DateOfBirth,
                Email = request.Email,
                Phone = request.Phone,
                Address = new Address
                {
                    Street = request.Address.Street,
                    City = request.Address.City,
                    State = request.Address.State,
                    ZipCode = request.Address.ZipCode
                }
            };

            var created = await _unitOfWork.Patients.AddAsync(patient);
            await _unitOfWork.SaveChangesAsync();

            return new ApiResponse<PatientResponse>
            {
                Success = true,
                Data = MapToPatientResponse(created),
                Message = "Patient created successfully"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PatientResponse>
            {
                Success = false,
                Message = $"Error creating patient: {ex.Message}",
                ErrorCode = "PATIENT_CREATE_ERROR"
            };
        }
    }

    public async Task<ApiResponse<List<PatientListResponse>>> GetAllPatientsAsync()
    {
        try
        {
            var patients = await _unitOfWork.Patients.GetAllAsync();
            var patientList = patients.Select(p => new PatientListResponse
            {
                Id = p.Id,
                FirstName = p.FirstName,
                LastName = p.LastName,
                Email = p.Email,
                Phone = p.Phone
            }).ToList();

            return new ApiResponse<List<PatientListResponse>>
            {
                Success = true,
                Data = patientList,
                Count = patientList.Count
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<PatientListResponse>>
            {
                Success = false,
                Message = $"Error retrieving patients: {ex.Message}",
                ErrorCode = "PATIENTS_RETRIEVE_ERROR"
            };
        }
    }

    public async Task<ApiResponse<PatientResponse>> GetPatientByIdAsync(Guid id)
    {
        try
        {
            var patient = await _unitOfWork.Patients.GetByIdAsync(id);
            
            if (patient == null)
            {
                return new ApiResponse<PatientResponse>
                {
                    Success = false,
                    Message = "Patient not found",
                    ErrorCode = "PATIENT_NOT_FOUND"
                };
            }

            return new ApiResponse<PatientResponse>
            {
                Success = true,
                Data = MapToPatientResponse(patient)
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PatientResponse>
            {
                Success = false,
                Message = $"Error retrieving patient: {ex.Message}",
                ErrorCode = "PATIENT_RETRIEVE_ERROR"
            };
        }
    }

    private PatientResponse MapToPatientResponse(Patient patient)
    {
        return new PatientResponse
        {
            Id = patient.Id,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            DateOfBirth = patient.DateOfBirth,
            Email = patient.Email,
            Phone = patient.Phone,
            Address = new AddressDto
            {
                Street = patient.Address.Street,
                City = patient.Address.City,
                State = patient.Address.State,
                ZipCode = patient.Address.ZipCode
            },
            CreatedAt = patient.CreatedAt,
            UpdatedAt = patient.UpdatedAt
        };
    }
}
