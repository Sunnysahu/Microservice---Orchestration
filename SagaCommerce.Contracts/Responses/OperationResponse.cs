namespace SagaCommerce.Contracts.Responses;

public class OperationResponse
{
    public bool Success { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public Guid? OperationId { get; set; }
}