using InventoryService.Services;
using Microsoft.AspNetCore.Mvc;
using SagaCommerce.Contracts.Requests;

namespace InventoryService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class InventoryController(InventoryManager inventoryManager) : ControllerBase
{
    private readonly InventoryManager _inventoryManager = inventoryManager;

    [HttpPost("reserve")]
    public async Task<IActionResult> Reserve([FromBody] ReserveInventoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryManager.ReserveAsync(request, cancellationToken);

        if (result.Success)
            return Ok(result);

        return result.Code switch
        {
            "INVALID_REQUEST" => BadRequest(result),

            "INSUFFICIENT_STOCK" => Conflict(result),

            "IDEMPOTENCY_CONFLICT" => Conflict(result),

            "CONCURRENT_OPERATION" => Conflict(result),

            _ => StatusCode(StatusCodes.Status500InternalServerError, result)
        };
    }

    [HttpPost("release")]
    public async Task<IActionResult> Release([FromBody] ReleaseInventoryRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryManager.ReleaseAsync(request, cancellationToken);

        if (result.Success)
        {
            return Ok(result);
        }

        return result.Code switch
        {
            "INVALID_REQUEST" => BadRequest(result),

            _ => StatusCode(StatusCodes.Status500InternalServerError, result)
        };
    }
}