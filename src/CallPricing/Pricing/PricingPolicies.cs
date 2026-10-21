using CallPricing.Domain;

namespace CallPricing.Pricing;

/// <summary>Policy functions are selected by the caller and passed into the same pipeline.</summary>
public static class PricingPolicies
{
    public static decimal StandardRatePerMinute(CallRecord call) => call.Type switch
    {
        CallType.Local => 0.10m,
        CallType.International => 0.35m,
        _ => throw new ArgumentOutOfRangeException(nameof(call), "Unsupported call type.")
    };

    public static decimal SaverRatePerMinute(CallRecord call) => call.Type switch
    {
        CallType.Local => 0.08m,
        CallType.International => 0.30m,
        _ => throw new ArgumentOutOfRangeException(nameof(call), "Unsupported call type.")
    };
}
