namespace PaymentService.Models;

public class Payment
{
    public Guid PaymentId { get; set; }

    public Guid? SagaId { get; set; }

    public long OrderId { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = "Pending";

    public string IdempotencyKey { get; set; } = string.Empty;

    public string? RequestHash { get; set; }

    public DateTime CreatedAt { get; set; }
}