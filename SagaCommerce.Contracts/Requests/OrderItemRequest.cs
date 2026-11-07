using System.ComponentModel.DataAnnotations;

namespace SagaCommerce.Contracts.Requests;

public class OrderItemRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, 10000)]
    public int Quantity { get; set; }

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal UnitPrice { get; set; }
}