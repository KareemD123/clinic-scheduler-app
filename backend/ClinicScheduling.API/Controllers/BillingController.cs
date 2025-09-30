using ClinicScheduling.Application.DTOs.Billing;
using ClinicScheduling.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClinicScheduling.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BillingController : ControllerBase
{
    private readonly IBillingService _billingService;

    public BillingController(IBillingService billingService)
    {
        _billingService = billingService;
    }

    [HttpPost("invoices")]
    public async Task<IActionResult> GenerateInvoice([FromBody] CreateInvoiceRequest request)
    {
        var result = await _billingService.GenerateInvoiceAsync(request);
        return result.Success ? CreatedAtAction(nameof(GetInvoice), new { id = result.Data?.Id }, result) : BadRequest(result);
    }

    [HttpGet("invoices/{id}")]
    public async Task<IActionResult> GetInvoice(Guid id)
    {
        var result = await _billingService.GetInvoiceByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost("payments")]
    public async Task<IActionResult> ProcessPayment([FromBody] ProcessPaymentRequest request)
    {
        var result = await _billingService.ProcessPaymentAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
