namespace OrderService.Models;

public class Order
{
    public long OrderId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; }

    public List<OrderItem> Items { get; set; } = [];
}