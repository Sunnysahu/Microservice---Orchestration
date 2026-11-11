using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PaymentService.Data;
using PaymentService.Models;
using SagaCommerce.Contracts.Requests;
using SagaCommerce.Contracts.Responses;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PaymentService.Services;

public class PaymentManager
{
    private readonly PaymentDbContext _context;

    public PaymentManager(PaymentDbContext context) => _context = context;

    public async Task<OperationResponse> ProcessPaymentAsync(ProcessPaymentRequest request, CancellationToken cancellationToken)
    {
        if (request.SagaId == Guid.Empty ||
            request.OrderId <= 0 ||
            request.Amount <= 0 ||
            string.IsNullOrWhiteSpace(request.IdempotencyKey)
        )
        {
            return Failure("INVALID_REQUEST", "Invalid payment request.");
        }

        string requestHash = GenerateRequestHash(request);

        try
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

            // First check whether this idempotency key was already used.
            var existingByKey = await _context.Payments
                .FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken);

            if (existingByKey != null)
            {
                if (existingByKey.RequestHash != requestHash)
                    return Failure("IDEMPOTENCY_CONFLICT", "This idempotency key was already used for a different payment.");

                await transaction.CommitAsync(cancellationToken);

                return GetExistingPaymentResponse(existingByKey);
            }

            // Prevent a second payment for the same order.
            var existingByOrder = await _context.Payments
                .FirstOrDefaultAsync(x => x.OrderId == request.OrderId, cancellationToken);

            if (existingByOrder != null)
                return Failure("ORDER_ALREADY_PROCESSED", "A payment record already exists for this order.");


            string paymentStatus = request.SimulateFailure ? "Declined" : "Paid";

            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                SagaId = request.SagaId,
                OrderId = request.OrderId,
                Amount = request.Amount,
                IdempotencyKey = request.IdempotencyKey,
                RequestHash = requestHash,
                Status = paymentStatus,
                CreatedAt = DateTime.Now
            };

            _context.Payments.Add(payment);

            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            if (payment.Status == "Declined")
                return Failure("PAYMENT_DECLINED", "Payment was declined by the simulated payment processor.");


            return new OperationResponse
            {
                Success = true,
                Code = "PAYMENT_SUCCESS",
                Message = "Payment processed successfully.",
                OperationId = payment.PaymentId
            };
        }
        catch (DbUpdateException exception)
            when (IsUniqueConstraintViolation(exception))
        {
            // Another concurrent request may have inserted the payment.
            var existingByKey = await _context.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken);

            if (existingByKey != null)
            {
                if (existingByKey.RequestHash != requestHash)
                    return Failure("IDEMPOTENCY_CONFLICT", "This idempotency key was already used for a different payment.");


                return GetExistingPaymentResponse(existingByKey);
            }

            var existingByOrder = await _context.Payments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OrderId == request.OrderId, cancellationToken);

            if (existingByOrder != null)
                return Failure("ORDER_ALREADY_PROCESSED", "A payment record already exists for this order.");

            throw;
        }
    }

    private static string GenerateRequestHash(ProcessPaymentRequest request)
    {
        string input = string.Join(
            "|",
            request.SagaId.ToString("D"),
            request.OrderId.ToString(CultureInfo.InvariantCulture),
            request.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            request.IdempotencyKey,
            request.SimulateFailure.ToString());

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));

        return Convert.ToHexString(hash);
    }

    private static OperationResponse GetExistingPaymentResponse(Payment payment)
    {
        if (payment.Status == "Declined")
        {
            return Failure("PAYMENT_DECLINED", "This payment was previously declined.");
        }

        return new OperationResponse
        {
            Success = true,
            Code = "ALREADY_PROCESSED",
            Message = "This payment was already processed.",
            OperationId = payment.PaymentId
        };
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        => exception.InnerException is SqlException sqlException && (sqlException.Number == 2601 || sqlException.Number == 2627);


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