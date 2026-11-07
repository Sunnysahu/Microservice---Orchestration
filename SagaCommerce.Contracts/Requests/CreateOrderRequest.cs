using System.ComponentModel.DataAnnotations;

namespace SagaCommerce.Contracts.Requests;

public class CreateOrderRequest
{
    [Required]
    [StringLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<OrderItemRequest> Items { get; set; } = [];
}