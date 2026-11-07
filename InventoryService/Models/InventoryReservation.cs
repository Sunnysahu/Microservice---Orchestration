using System.ComponentModel.DataAnnotations;

namespace InventoryService.Models;

public class InventoryReservation
{
    [Key]
    public Guid ReservationId { get; set; }

    public Guid? SagaId { get; set; }

    public long OrderId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    public string Status { get; set; } = "Reserved";

    public string? IdempotencyKey { get; set; }

    public DateTime CreatedAt { get; set; }
}