namespace OrderService.Models;

public class OrderItem
{
    public long OrderItemId { get; set; }

    public long OrderId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public Order Order { get; set; } = null!;
}