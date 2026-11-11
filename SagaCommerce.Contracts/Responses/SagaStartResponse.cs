namespace SagaCommerce.Contracts.Responses;

public class SagaStartResponse
{
    public Guid SagaId { get; set; }
    public long OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CurrentStep { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}