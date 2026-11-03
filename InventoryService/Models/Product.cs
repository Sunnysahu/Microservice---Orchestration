namespace InventoryService.Models;

public class Product
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int AvailableQuantity { get; set; }
}