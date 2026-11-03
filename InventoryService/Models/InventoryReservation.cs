namespace InventoryService.Models;

public class InventoryReservation
{
    public Guid ReservationId { get; set; }

    public long OrderId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public string Status { get; set; } = "Reserved";

    public DateTime CreatedAt { get; set; }
}