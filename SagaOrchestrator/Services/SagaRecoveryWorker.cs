using SagaCommerce.Contracts.Services;

namespace SagaOrchestrator.Services;

public class SagaRecoveryWorker(IServiceScopeFactory scopeFactory, ILogger<SagaRecoveryWorker> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<SagaRecoveryWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Saga recovery worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var sagaManager = scope.ServiceProvider.GetRequiredService<SagaManager>();

                bool recovered = await sagaManager.RecoverNextAsync(stoppingToken);

                if (recovered)
                    _logger.LogInformation("Recovery worker processed a Saga.");

            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Unexpected error in Saga recovery worker.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _logger.LogInformation("Saga recovery worker stopped.");
    }
}