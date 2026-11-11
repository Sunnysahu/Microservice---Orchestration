using System.ComponentModel.DataAnnotations;

namespace SagaCommerce.Contracts.Requests;

public class ProcessPaymentRequest
{
    public Guid SagaId { get; set; }

    [Range(1, long.MaxValue)]
    public long OrderId { get; set; }

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;

    public bool SimulateFailure { get; set; }
}