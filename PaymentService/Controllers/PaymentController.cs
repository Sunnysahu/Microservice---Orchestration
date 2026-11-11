using Microsoft.AspNetCore.Mvc;
using PaymentService.Services;
using SagaCommerce.Contracts.Requests;

namespace PaymentService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentController(PaymentManager paymentManager) : ControllerBase
{
    private readonly PaymentManager _paymentManager = paymentManager;

    [HttpPost("process")]
    public async Task<IActionResult> ProcessPayment([FromBody] ProcessPaymentRequest request, CancellationToken cancellationToken)
    {
        var response = await _paymentManager.ProcessPaymentAsync(request, cancellationToken);

        return response.Code switch
        {
            "INVALID_REQUEST" => BadRequest(response),

            "IDEMPOTENCY_CONFLICT" => Conflict(response),

            "ORDER_ALREADY_PROCESSED" => Conflict(response),

            "PAYMENT_DECLINED" => UnprocessableEntity(response),

            _ => Ok(response)
        };
    }
}

