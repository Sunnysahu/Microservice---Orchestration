using System.ComponentModel.DataAnnotations;

namespace SagaCommerce.Contracts.Requests;

public class StartSagaRequest
{
    [Range(1, long.MaxValue)]
    public long OrderId { get; set; }

    [Required]
    [StringLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal TotalAmount { get; set; }

    [Required]
    [MinLength(1)]
    public List<OrderItemRequest> Items { get; set; } = [];
}