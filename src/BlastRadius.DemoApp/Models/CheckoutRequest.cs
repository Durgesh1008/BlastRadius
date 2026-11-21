using System.ComponentModel.DataAnnotations;

namespace BlastRadius.DemoApp.Models;

/// <summary>
/// Payload submitted by customers when initiating an e-commerce checkout.
/// Fully dynamic: accepts custom user identification, amounts, items, and targeted feature flags.
/// </summary>
public sealed class CheckoutRequest
{
    [Required]
    public string UserId { get; init; } = string.Empty;

    public string OrderId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    [Range(0.01, 1000000)]
    public decimal Amount { get; init; } = 2499.00m;

    public string ItemName { get; init; } = "High-Performance Cloud Node (4 vCPU, 16GB RAM)";

    public string UserTier { get; init; } = "Standard"; // Free, Standard, VIP, Enterprise, BetaTester

    public string City { get; init; } = "Bangalore";

    public string? FlagKey { get; init; } = "new-upi-checkout";

    public string? PaymentMethodChosen { get; init; } // e.g. "UPI_INTENT", "LEGACY_CARD", "ONE_CLICK"

    public string? PaymentDetails { get; init; } // e.g. "user@okhdfcbank" or "4111-XXXX-XXXX-1111"
}
