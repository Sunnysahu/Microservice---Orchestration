using Microsoft.AspNetCore.Mvc;
using SagaCommerce.Contracts.Requests;
using SagaCommerce.Contracts.Services;

namespace SagaOrchestrator.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SagaController(SagaManager sagaManager) : ControllerBase
{
    private readonly SagaManager _sagaManager = sagaManager;

    [HttpPost("start")]
    public async Task<IActionResult> StartSaga([FromBody] StartSagaRequest request, CancellationToken cancellationToken)
    {
        var result = await _sagaManager.StartAsync(request, cancellationToken);

        return StatusCode(result.StatusCode, result.Response);
    }

    [HttpGet("{sagaId:guid}")]
    public async Task<IActionResult> GetStatus(Guid sagaId, CancellationToken cancellationToken)
    {
        var result = await _sagaManager.GetStatusAsync(sagaId, cancellationToken);

        if (result.Response == null)
            return StatusCode(result.StatusCode);

        return StatusCode(result.StatusCode, result.Response);
    }
}