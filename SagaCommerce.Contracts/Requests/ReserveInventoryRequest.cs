using System.ComponentModel.DataAnnotations;

namespace SagaCommerce.Contracts.Requests;

public class ReserveInventoryRequest
{
    public Guid SagaId { get; set; }

    [Range(1, long.MaxValue)]
    public long OrderId { get; set; }

    [Required]
    [StringLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<OrderItemRequest> Items { get; set; } = [];
}