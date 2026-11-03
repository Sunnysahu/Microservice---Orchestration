using System.ComponentModel.DataAnnotations;

namespace SagaOrchestrator.Models;

public class SagaInstance
{
    [Key]
    public Guid SagaId { get; set; }

    public long OrderId { get; set; }

    public string Status { get; set; } = "Running";

    public string CurrentStep { get; set; } = "ReserveInventory";

    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];
}
