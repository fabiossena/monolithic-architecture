using Monolith.Domain;

namespace Monolith.Services;

public record PaymentResult(bool Approved, string TransactionId, string? Reason);

// Payments Service — simulated gateway. No external calls: keeps the demo
// self-contained while preserving the approve/decline contract.
public class PaymentsService
{
    public Task<PaymentResult> ChargeAsync(decimal amount, string method, bool simulateFailure)
    {
        // Deterministic simulation rules:
        //  - explicit failure flag -> declined
        //  - non-positive amount   -> declined
        //  - otherwise             -> approved
        var declined = simulateFailure || amount <= 0;

        var result = declined
            ? new PaymentResult(false, "", "Transaction declined by the issuer (simulated).")
            : new PaymentResult(true, $"TXN-{Guid.NewGuid().ToString("N")[..12].ToUpper()}", null);

        return Task.FromResult(result);
    }
}
