using ClinicScheduling.Application.DTOs.Appointments;
using ClinicScheduling.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClinicScheduling.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentService;

    public AppointmentsController(IAppointmentService appointmentService)
    {
        _appointmentService = appointmentService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateAppointment([FromBody] CreateAppointmentRequest request)
    {
        var result = await _appointmentService.CreateAppointmentAsync(request);
        return result.Success ? CreatedAtAction(nameof(GetAppointment), new { id = result.Data?.Id }, result) : BadRequest(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllAppointments()
    {
        var result = await _appointmentService.GetAllAppointmentsAsync();
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAppointment(Guid id)
    {
        var result = await _appointmentService.GetAppointmentByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("schedule-and-bill")]
    public async Task<IActionResult> ScheduleAndBill([FromBody] ScheduleAndBillRequest request)
    {
        var result = await _appointmentService.ScheduleAndBillAsync(request);
        return result.Success ? CreatedAtAction(nameof(GetAppointment), new { id = result.Data?.Appointment.Id }, result) : BadRequest(result);
    }
}
