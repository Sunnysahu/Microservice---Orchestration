namespace SagaOrchestrator.Models;

public class ServiceUrlsOptions
{
    public const string SectionName = "Services";

    public required string Inventory { get; set; }

    public required string Payment { get; set; }
}