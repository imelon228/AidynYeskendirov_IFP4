namespace CallPricing.Domain;

/// <summary>The calculated quote for one billable call.</summary>
public sealed record CallQuote(
    string CallId,
    int DurationMinutes,
    CallType Type,
    decimal RatePerMinute,
    decimal ConnectionFee,
    decimal TotalCost);
