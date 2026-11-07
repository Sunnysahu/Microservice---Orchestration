namespace SagaOrchestrator.Models;

public class ServiceUrlsOptions
{
    public const string SectionName = "Services";

    public string Inventory { get; set; } = string.Empty;

    public string Payment { get; set; } = string.Empty;
}