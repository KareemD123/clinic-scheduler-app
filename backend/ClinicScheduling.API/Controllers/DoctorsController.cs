using ClinicScheduling.Application.DTOs.Common;
using ClinicScheduling.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClinicScheduling.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DoctorsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;

    public DoctorsController(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllDoctors()
    {
        var doctors = await _unitOfWork.Doctors.GetAllAsync();
        var doctorList = doctors.Select(d => new 
        {
            d.Id,
            d.FirstName,
            d.LastName,
            d.Specialization,
            d.ConsultationFee
        }).ToList();

        var response = new ApiResponse<object>
        {
            Success = true,
            Data = doctorList,
            Count = doctorList.Count
        };

        return Ok(response);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDoctorById(Guid id)
    {
        var doctor = await _unitOfWork.Doctors.GetByIdAsync(id);
        if (doctor == null)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = "Doctor not found", ErrorCode = "DOCTOR_NOT_FOUND" });
        }

        var response = new ApiResponse<object>
        {
            Success = true,
            Data = new 
            {
                doctor.Id,
                doctor.FirstName,
                doctor.LastName,
                doctor.Specialization,
                doctor.Email,
                doctor.Phone,
                doctor.ConsultationFee,
                doctor.Availability,
                doctor.CreatedAt
            }
        };

        return Ok(response);
    }

    [HttpGet("{id}/availability")]
    public async Task<IActionResult> GetDoctorAvailability(Guid id)
    {
        var doctor = await _unitOfWork.Doctors.GetByIdAsync(id);
        if (doctor == null)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = "Doctor not found", ErrorCode = "DOCTOR_NOT_FOUND" });
        }

        var response = new ApiResponse<object>
        {
            Success = true,
            Data = doctor.Availability
        };

        return Ok(response);
    }

    [HttpGet("{id}/schedule")]
    public async Task<IActionResult> GetDoctorSchedule(Guid id, [FromQuery] DateTime date)
    {
        var doctor = await _unitOfWork.Doctors.GetByIdAsync(id);
        if (doctor == null)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = "Doctor not found", ErrorCode = "DOCTOR_NOT_FOUND" });
        }

        var allAppointments = await _unitOfWork.Appointments.GetAllAsync();
        var doctorAppointments = allAppointments
            .Where(a => a.DoctorId == id && a.AppointmentDateTime.Date == date.Date)
            .OrderBy(a => a.AppointmentDateTime)
            .ToList();

        var dayOfWeek = (int)date.DayOfWeek;
        var availability = doctor.Availability.FirstOrDefault(a => a.DayOfWeek == dayOfWeek);

        var availableSlots = new List<object>();
        if (availability != null)
        {
            var slotDuration = 30; // Assuming 30 minute slots
            var startTime = TimeSpan.Parse(availability.StartTime);
            var endTime = TimeSpan.Parse(availability.EndTime);

            var currentTime = startTime;
            while (currentTime.Add(TimeSpan.FromMinutes(slotDuration)) <= endTime)
            {
                var slotStartDateTime = date.Date.Add(currentTime);
                var slotEndDateTime = slotStartDateTime.Add(TimeSpan.FromMinutes(slotDuration));

                bool isBooked = doctorAppointments.Any(a =>
                    a.AppointmentDateTime < slotEndDateTime && a.AppointmentDateTime.AddMinutes(a.Duration) > slotStartDateTime
                );

                if (!isBooked)
                {
                    availableSlots.Add(new { 
                        StartTime = slotStartDateTime.ToString("HH:mm"), 
                        EndTime = slotEndDateTime.ToString("HH:mm") 
                    });
                }
                currentTime = currentTime.Add(TimeSpan.FromMinutes(slotDuration));
            }
        }
        
        var patientIds = doctorAppointments.Select(a => a.PatientId).Distinct();
        var patients = (await _unitOfWork.Patients.GetAllAsync()).Where(p => patientIds.Contains(p.Id)).ToDictionary(p => p.Id);

        var scheduleData = new
        {
            doctorId = doctor.Id,
            doctorName = $"Dr. {doctor.FirstName} {doctor.LastName}",
            date = date.ToString("yyyy-MM-dd"),
            appointments = doctorAppointments.Select(a => new {
                a.Id,
                patientName = patients.TryGetValue(a.PatientId, out var patient) ? $"{patient.FirstName} {patient.LastName}" : "Unknown",
                startTime = a.AppointmentDateTime.ToString("HH:mm"),
                endTime = a.AppointmentDateTime.AddMinutes(a.Duration).ToString("HH:mm"),
                status = a.Status.ToString()
            }),
            availableSlots
        };

        return Ok(new ApiResponse<object> { Success = true, Data = scheduleData });
    }
}
