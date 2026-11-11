using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SagaCommerce.Contracts.Requests;
using SagaCommerce.Contracts.Responses;
using SagaOrchestrator.Data;
using SagaOrchestrator.Models;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SagaCommerce.Contracts.Services;

public class SagaManager(SagaDbContext context, IHttpClientFactory httpClientFactory, ILogger<SagaManager> logger)
{
    private readonly SagaDbContext _context = context;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<SagaManager> _logger = logger;

    public async Task<(int StatusCode, SagaStartResponse Response)> StartAsync(StartSagaRequest request, CancellationToken cancellationToken)
    {
        if (request.OrderId <= 0 ||
            string.IsNullOrWhiteSpace(request.IdempotencyKey) ||
            request.Items == null ||
            request.Items.Count == 0 ||
            request.TotalAmount <= 0 ||
            request.Items.Any(x => x.ProductId <= 0 || x.Quantity <= 0 || x.UnitPrice <= 0) ||
            request.Items.GroupBy(x => x.ProductId).Any(g => g.Count() > 1)
        )
        {
            return (400, new SagaStartResponse
            {
                OrderId = request.OrderId,
                Status = "Invalid",
                Message = "Invalid Saga request or duplicate ProductIds."
            });
        }

        decimal calculatedAmount = request.Items.Sum(x => x.Quantity * x.UnitPrice);

        if (calculatedAmount != request.TotalAmount)
        {
            return (400, new SagaStartResponse
            {
                OrderId = request.OrderId,
                Status = "Invalid",
                Message = "TotalAmount does not match the order items."
            });
        }

        string requestHash = GenerateRequestHash(request);

        var existing = await _context.SagaInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken);

        if (existing != null)
        {
            if (existing.RequestHash != requestHash)
                return (409, ToResponse(existing, "Idempotency key was already used for a different request."));

            return (200, ToResponse(existing, "Saga already exists."));
        }

        var existingOrder = await _context.SagaInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OrderId == request.OrderId, cancellationToken);

        if (existingOrder != null)
        {
            return (409, ToResponse(
                existingOrder,
                "A Saga already exists for this OrderId."));
        }

        var saga = new SagaInstance
        {
            SagaId = Guid.NewGuid(),
            OrderId = request.OrderId,
            Status = "Running",
            CurrentStep = "ReserveInventory",
            IdempotencyKey = request.IdempotencyKey,
            RequestHash = requestHash,
            RequestPayload = JsonSerializer.Serialize(request),
            RetryCount = 0,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _context.SagaInstances.Add(saga);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            _context.Entry(saga).State = EntityState.Detached;

            var duplicate = await _context.SagaInstances
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.IdempotencyKey == request.IdempotencyKey || x.OrderId == request.OrderId,
                    cancellationToken
                );

            if (duplicate == null)
                throw;

            if (duplicate.IdempotencyKey == request.IdempotencyKey && duplicate.RequestHash != requestHash)
                return (409, ToResponse(duplicate, "Idempotency key was already used for a different request."));


            return (200, ToResponse(duplicate, "Saga already exists."));
        }

        // Claim the Saga so duplicate requests cannot execute it concurrently.
        Guid leaseId = Guid.NewGuid();

        int claimed = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE dbo.SagaInstances
            SET LeaseId = {leaseId},
                LeaseUntil = DATEADD(SECOND, 60, SYSDATETIME()),
                UpdatedAt = SYSDATETIME()
            WHERE SagaId = {saga.SagaId}
              AND LeaseId IS NULL
            """,
            cancellationToken
        );

        if (claimed == 0)
        {
            return (202, ToResponse(saga, "Saga is already being processed."));
        }

        try
        {
            return await ExecuteSagaAsync(saga.SagaId, leaseId, request, cancellationToken);
        }
        finally
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE dbo.SagaInstances
                SET LeaseId = NULL,
                    LeaseUntil = NULL,
                    UpdatedAt = SYSDATETIME()
                WHERE SagaId = {saga.SagaId}
                  AND LeaseId = {leaseId}
                """, CancellationToken.None
            );
        }
    }

    private async Task<(int StatusCode, SagaStartResponse Response)> ExecuteSagaAsync
    (
        Guid sagaId,
        Guid leaseId,
        StartSagaRequest request,
        CancellationToken cancellationToken
    )
    {
        var saga = await _context.SagaInstances.FirstAsync(x => x.SagaId == sagaId, cancellationToken);
        await _context.Entry(saga).ReloadAsync(cancellationToken);

        // STEP 1: Reserve inventory.
        saga.CurrentStep = "ReserveInventory";
        await SaveStepAsync(saga, leaseId, cancellationToken);

        var inventoryRequest = new ReserveInventoryRequest
        {
            SagaId = saga.SagaId,
            OrderId = request.OrderId,
            IdempotencyKey = request.IdempotencyKey,
            Items = request.Items
        };

        OperationResponse? inventoryResult;

        try
        {
            var client = _httpClientFactory.CreateClient("Inventory");

            using var response = await client.PostAsJsonAsync(
                "api/inventory/reserve",
                inventoryRequest,
                cancellationToken
            );

            inventoryResult = await response.Content.ReadFromJsonAsync<OperationResponse>(cancellationToken);

            if (inventoryResult == null)
                throw new HttpRequestException("Inventory returned an empty response.");
        }
        catch (Exception exception) when (
            exception is HttpRequestException ||
            exception is TaskCanceledException)
        {
            return await MarkUncertainAsync(saga, leaseId, "Inventory outcome is uncertain: " + exception.Message);
        }

        if (!inventoryResult.Success)
        {
            saga.Status = "Failed";
            saga.LastError = inventoryResult.Message;
            saga.CurrentStep = "InventoryFailed";

            await SaveStepAsync(saga, leaseId, cancellationToken);

            return (409, ToResponse(saga, inventoryResult.Message));
        }

        // STEP 2: Process payment.
        saga.CurrentStep = "ProcessPayment";
        await SaveStepAsync(saga, leaseId, cancellationToken);

        var paymentRequest = new ProcessPaymentRequest
        {
            SagaId = saga.SagaId,
            OrderId = request.OrderId,
            Amount = request.TotalAmount,
            IdempotencyKey = request.IdempotencyKey,
            SimulateFailure = request.SimulatePaymentFailure
        };

        OperationResponse? paymentResult;
        HttpStatusCode paymentHttpStatus;

        try
        {
            var client = _httpClientFactory.CreateClient("Payment");

            using var response = await client.PostAsJsonAsync(
                "api/payment/process",
                paymentRequest,
                cancellationToken);

            paymentHttpStatus = response.StatusCode;

            paymentResult = await response.Content.ReadFromJsonAsync<OperationResponse>(cancellationToken);

            if (paymentResult == null)
                throw new HttpRequestException("Payment returned an empty response.");
        }
        catch (Exception exception)
        when (exception is HttpRequestException || exception is TaskCanceledException)
        {
            return await MarkUncertainAsync(saga, leaseId, "Payment outcome is uncertain: " + exception.Message);
        }

        if (paymentResult.Success)
        {
            saga.Status = "Completed";
            saga.CurrentStep = "Completed";
            saga.LastError = null;

            await SaveStepAsync(saga, leaseId, cancellationToken);

            return (200, ToResponse(saga, "Inventory reserved and payment completed."));
        }

        // A payment decline or conflict is a known failure.
        // Release the inventory reservation.
        if (paymentHttpStatus == HttpStatusCode.UnprocessableEntity ||
            paymentHttpStatus == HttpStatusCode.Conflict)
        {
            saga.CurrentStep = "CompensateInventory";
            saga.LastError = paymentResult.Message;

            await SaveStepAsync(saga, leaseId, cancellationToken);

            return await CompensateInventoryAsync(
                saga,
                leaseId,
                request,
                cancellationToken
            );
        }

        // A server error may have happened after payment was committed.
        // Do not release inventory until the payment outcome is reconciled.
        return await MarkUncertainAsync(saga, leaseId, "Payment outcome requires reconciliation: " + paymentResult.Message);
    }

    private async Task<(int StatusCode, SagaStartResponse Response)> CompensateInventoryAsync
    (
        SagaInstance saga,
        Guid leaseId,
        StartSagaRequest request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var client = _httpClientFactory.CreateClient("Inventory");

            var releaseRequest = new ReleaseInventoryRequest
            {
                SagaId = saga.SagaId,
                OrderId = saga.OrderId,
                IdempotencyKey = request.IdempotencyKey
            };

            using var response = await client.PostAsJsonAsync(
                "api/inventory/release",
                releaseRequest,
                cancellationToken
            );

            var result = await response.Content
                .ReadFromJsonAsync<OperationResponse>(cancellationToken: cancellationToken);

            if (response.IsSuccessStatusCode && result?.Success == true)
            {
                saga.Status = "Failed";
                saga.CurrentStep = "CompensationCompleted";
                saga.LastError = "Payment failed. Inventory was released.";

                await SaveStepAsync(saga, leaseId, cancellationToken);

                return (422, ToResponse(saga, "Payment failed and inventory was released."));
            }

            saga.Status = "CompensationPending";
            saga.LastError = result?.Message ?? "Inventory release failed.";

            await SaveStepAsync(saga, leaseId, cancellationToken);

            return (202, ToResponse(saga, "Payment failed. Inventory compensation is pending."));
        }
        catch (Exception exception) when (
            exception is HttpRequestException ||
            exception is TaskCanceledException)
        {
            saga.Status = "CompensationPending";
            saga.LastError = exception.Message;

            await SaveStepAsync(saga, leaseId, CancellationToken.None);

            return (202, ToResponse(saga, "Payment failed. Inventory compensation is pending."));
        }
    }

    private async Task<(int StatusCode, SagaStartResponse Response)> MarkUncertainAsync(SagaInstance saga, Guid leaseId, string error)
    {
        saga.Status = "RequiresReconciliation";
        saga.LastError = error;

        await SaveStepAsync(saga, leaseId, CancellationToken.None);

        _logger.LogWarning(
            "Saga {SagaId} requires reconciliation at step {CurrentStep}: {Error}",
            saga.SagaId,
            saga.CurrentStep,
            error
        );

        return (202, ToResponse(saga, "The operation outcome is uncertain and requires reconciliation."));
    }

    private async Task SaveStepAsync(SagaInstance saga, Guid leaseId, CancellationToken cancellationToken)
    {
        saga.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string GenerateRequestHash(StartSagaRequest request)
    {
        var canonicalRequest = new
        {
            request.OrderId,
            request.IdempotencyKey,
            request.TotalAmount,
            request.SimulatePaymentFailure,
            Items = request.Items
                .OrderBy(x => x.ProductId)
                .Select(x => new
                {
                    x.ProductId,
                    x.Quantity,
                    x.UnitPrice
                })
                .ToArray()
        };

        string json = JsonSerializer.Serialize(canonicalRequest);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));

        return Convert.ToHexString(hash);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException && (sqlException.Number == 2601 || sqlException.Number == 2627);

    private static SagaStartResponse ToResponse(SagaInstance saga, string message)
    {
        return new SagaStartResponse
        {
            SagaId = saga.SagaId,
            OrderId = saga.OrderId,
            Status = saga.Status,
            CurrentStep = saga.CurrentStep,
            Message = message
        };
    }
}