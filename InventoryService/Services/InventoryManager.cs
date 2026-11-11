using InventoryService.Data;
using InventoryService.Models;
using Microsoft.EntityFrameworkCore;
using SagaCommerce.Contracts.Requests;
using SagaCommerce.Contracts.Responses;

namespace InventoryService.Services;

public class InventoryManager(InventoryDbContext dbContext, ILogger<InventoryManager> logger)
{
    private readonly InventoryDbContext _dbContext = dbContext;
    private readonly ILogger<InventoryManager> _logger = logger;

    public async Task<OperationResponse> ReserveAsync(ReserveInventoryRequest request, CancellationToken cancellationToken)
    {
        if (request.SagaId == Guid.Empty)
            return Failure("INVALID_REQUEST", "SagaId is required.");

        if (request.Items.Count == 0)
            return Failure("INVALID_REQUEST", "At least one item is required.");


        if (request.Items.Any(x => x.ProductId <= 0 || x.Quantity <= 0))
            return Failure("INVALID_REQUEST", "ProductId and Quantity must be positive.");

        // A product must appear only once in a reservation request.
        if (request.Items.GroupBy(x => x.ProductId).Any(group => group.Count() > 1))
            return Failure("INVALID_REQUEST", "Duplicate ProductId values are not allowed.");


        await using var transaction = await
            _dbContext.Database
            .BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        try
        {
            var existingReservations = await _dbContext.InventoryReservations
                .Where(x => x.SagaId == request.SagaId)
                .ToListAsync(cancellationToken);

            if (existingReservations.Count > 0)
            {
                var sameRequest = existingReservations.All
                    (x => x.OrderId == request.OrderId && x.IdempotencyKey == request.IdempotencyKey)
                    && existingReservations.Count == request.Items.Count
                    && request.Items.All(item =>
                    existingReservations.Any(reservation =>
                    reservation.ProductId == item.ProductId && reservation.Quantity == item.Quantity));

                if (!sameRequest)
                {
                    return new OperationResponse
                    {
                        Success = false,
                        Code = "IDEMPOTENCY_CONFLICT",
                        Message = "This SagaId was already used for a different inventory request."
                    };
                }

                if (existingReservations.Any(x => x.Status != "Reserved"))
                {
                    return new OperationResponse
                    {
                        Success = false,
                        Code = "RESERVATION_ALREADY_RELEASED",
                        Message = "This inventory reservation has already been released."
                    };
                }

                return new OperationResponse
                {
                    Success = true,
                    Code = "ALREADY_RESERVED",
                    Message = "Inventory was already reserved for this Saga."
                };
            }

            foreach (var item in request.Items)
            {
                // Atomic conditional update:
                // deduct stock only if enough quantity is available.
                var affectedRows = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                    UPDATE dbo.Products
                    SET AvailableQuantity = AvailableQuantity - {item.Quantity}
                    WHERE ProductId = {item.ProductId}
                      AND AvailableQuantity >= {item.Quantity}
                    """,
                    cancellationToken
                    );

                if (affectedRows == 0)
                {
                    await transaction.RollbackAsync(cancellationToken);

                    _logger.LogInformation(
                        "Inventory reservation failed. SagaId: {SagaId}, ProductId: {ProductId}",
                        request.SagaId,
                        item.ProductId
                    );

                    return Failure(
                        "INSUFFICIENT_STOCK",
                        $"Insufficient stock for ProductId {item.ProductId}, or product does not exist."
                    );
                }

                _dbContext.InventoryReservations.Add(
                    new InventoryReservation
                    {
                        ReservationId = Guid.NewGuid(),
                        SagaId = request.SagaId,
                        OrderId = request.OrderId,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        Status = "Reserved",
                        IdempotencyKey = request.IdempotencyKey,
                        CreatedAt = DateTime.Now
                    }
                );
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new OperationResponse
            {
                Success = true,
                Code = "SUCCESS",
                Message = "Inventory reserved successfully."
            };
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            _logger.LogWarning(
                ex,
                "Database conflict while reserving inventory for SagaId {SagaId}",
                request.SagaId
            );

            return Failure(
                "CONCURRENT_OPERATION",
                "A concurrent inventory operation occurred. Retry the request."
            );
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<OperationResponse> ReleaseAsync(ReleaseInventoryRequest request, CancellationToken cancellationToken)
    {
        if (request.SagaId == Guid.Empty)
            return Failure("INVALID_REQUEST", "SagaId is required.");


        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        try
        {
            var reservations = await _dbContext.InventoryReservations
                .Where(x => x.SagaId == request.SagaId)
                .ToListAsync(cancellationToken);

            if (reservations.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken);

                return new OperationResponse
                {
                    Success = true,
                    Code = "SUCCESS",
                    Message = "No active reservations were found."
                };
            }

            var activeReservations = reservations
                .Where(x => x.Status == "Reserved")
                .ToList();

            foreach (var reservation in activeReservations)
            {
                var affectedRows = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE dbo.Products
                        SET AvailableQuantity = AvailableQuantity + {reservation.Quantity}
                        WHERE ProductId = {reservation.ProductId}
                        """,
                        cancellationToken
                );

                if (affectedRows == 0)
                    throw new InvalidOperationException($"Product {reservation.ProductId} was not found during compensation.");


                reservation.Status = "Released";
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new OperationResponse
            {
                Success = true,
                Code = "SUCCESS",
                Message = "Inventory reservation released."
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static OperationResponse Failure(string code, string message)
    {
        return new OperationResponse
        {
            Success = false,
            Code = code,
            Message = message
        };
    }
}