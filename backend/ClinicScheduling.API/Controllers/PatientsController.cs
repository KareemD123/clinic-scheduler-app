using ClinicScheduling.Application.DTOs.Patients;
using ClinicScheduling.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClinicScheduling.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly IPatientService _patientService;

    public PatientsController(IPatientService patientService)
    {
        _patientService = patientService;
    }

    [HttpPost]
    public async Task<IActionResult> CreatePatient([FromBody] CreatePatientRequest request)
    {
        var result = await _patientService.CreatePatientAsync(request);
        return result.Success ? CreatedAtAction(nameof(GetPatient), new { id = result.Data?.Id }, result) : BadRequest(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllPatients()
    {
        var result = await _patientService.GetAllPatientsAsync();
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPatient(Guid id)
    {
        var result = await _patientService.GetPatientByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
