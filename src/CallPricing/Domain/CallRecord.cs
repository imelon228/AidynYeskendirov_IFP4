namespace CallPricing.Domain;

/// <summary>A validated, immutable call record used by the functional core.</summary>
public sealed record CallRecord
{
    public string CallId { get; }
    public int DurationMinutes { get; }
    public CallType Type { get; }

    public CallRecord(string callId, int durationMinutes, CallType type)
    {
        if (string.IsNullOrWhiteSpace(callId))
        {
            throw new ArgumentException("CallId is required.", nameof(callId));
        }

        if (durationMinutes is < 0 or > 300)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationMinutes), durationMinutes,
                "DurationMinutes must be between 0 and 300, inclusive.");
        }

        if (!Enum.IsDefined(typeof(CallType), type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Call type is not supported.");
        }

        CallId = callId.Trim();
        DurationMinutes = durationMinutes;
        Type = type;
    }
}
