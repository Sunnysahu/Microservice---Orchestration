using System.ComponentModel.DataAnnotations;

namespace SagaCommerce.Contracts.Requests;

public class ReleaseInventoryRequest
{
    public Guid SagaId { get; set; }

    [Range(1, long.MaxValue)]
    public long OrderId { get; set; }

    [Required]
    [StringLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;
}