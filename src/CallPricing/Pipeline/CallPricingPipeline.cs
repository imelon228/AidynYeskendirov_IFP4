using CallPricing.Domain;

namespace CallPricing.Pipeline;

/// <summary>
/// The functional core. Each stage returns a new read-only snapshot and has no I/O or mutable global state.
/// </summary>
public static class CallPricingPipeline
{
    public const int DefaultMinimumBillableMinutes = 1;
    public const decimal DefaultConnectionFee = 0.05m;

    /// <summary>
    /// Stage 1: selects calls whose duration is at least the explicit billable threshold.
    /// Input: IEnumerable&lt;CallRecord&gt;; output: read-only CallRecord list.
    /// </summary>
    public static IReadOnlyList<CallRecord> SelectBillableCalls(
        IEnumerable<CallRecord>? records,
        int minimumBillableMinutes)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (minimumBillableMinutes < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumBillableMinutes), minimumBillableMinutes,
                "The minimum billable duration must be at least one minute.");
        }

        CallRecord[] snapshot = records.ToArray();
        if (snapshot.Any(record => record is null))
        {
            throw new ArgumentException("Input records cannot contain null entries.", nameof(records));
        }

        return Array.AsReadOnly(snapshot
            .Where(record => record.DurationMinutes >= minimumBillableMinutes)
            .ToArray());
    }

    /// <summary>
    /// Stage 2: applies the supplied rate delegate and computes a quote for each selected call.
    /// The delegate is invoked once per billable call.
    /// Input: read-only CallRecord list plus Func&lt;CallRecord, decimal&gt;; output: read-only CallQuote list.
    /// </summary>
    public static IReadOnlyList<CallQuote> PriceCalls(
        IEnumerable<CallRecord>? billableCalls,
        Func<CallRecord, decimal>? ratePerMinutePolicy,
        decimal connectionFee)
    {
        ArgumentNullException.ThrowIfNull(billableCalls);
        ArgumentNullException.ThrowIfNull(ratePerMinutePolicy);
        if (connectionFee < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(connectionFee), "Connection fee cannot be negative.");
        }

        CallRecord[] snapshot = billableCalls.ToArray();
        if (snapshot.Any(call => call is null))
        {
            throw new ArgumentException("Billable calls cannot contain null entries.", nameof(billableCalls));
        }

        CallQuote[] quotes = snapshot.Select(call =>
        {
            decimal rate = ratePerMinutePolicy(call);
            if (rate < 0m)
            {
                throw new InvalidOperationException("A pricing policy cannot return a negative rate.");
            }

            decimal total = decimal.Round(
                (rate * call.DurationMinutes) + connectionFee,
                2,
                MidpointRounding.AwayFromZero);

            return new CallQuote(
                call.CallId,
                call.DurationMinutes,
                call.Type,
                rate,
                connectionFee,
                total);
        }).ToArray();

        return Array.AsReadOnly(quotes);
    }

    /// <summary>
    /// Stage 3: sorts quotes by descending cost (then CallId), and creates an aggregate summary.
    /// Input: quotes plus a policy label; output: immutable CallInvoiceSummary.
    /// </summary>
    public static CallInvoiceSummary BuildInvoiceSummary(
        IEnumerable<CallQuote>? quotes,
        string policyName)
    {
        ArgumentNullException.ThrowIfNull(quotes);
        if (string.IsNullOrWhiteSpace(policyName))
        {
            throw new ArgumentException("Policy name is required.", nameof(policyName));
        }

        CallQuote[] quoteSnapshot = quotes.ToArray();
        if (quoteSnapshot.Any(quote => quote is null))
        {
            throw new ArgumentException("Quotes cannot contain null entries.", nameof(quotes));
        }

        CallQuote[] ordered = quoteSnapshot
            .OrderByDescending(quote => quote.TotalCost)
            .ThenBy(quote => quote.CallId, StringComparer.Ordinal)
            .ToArray();

        decimal total = decimal.Round(ordered.Sum(quote => quote.TotalCost), 2, MidpointRounding.AwayFromZero);
        return new CallInvoiceSummary(policyName, ordered, total);
    }

    /// <summary>Visible composition of the three transformations, with all rules passed explicitly.</summary>
    public static CallInvoiceSummary Run(
        IEnumerable<CallRecord>? records,
        string policyName,
        Func<CallRecord, decimal>? ratePerMinutePolicy,
        int minimumBillableMinutes,
        decimal connectionFee)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(ratePerMinutePolicy);
        if (string.IsNullOrWhiteSpace(policyName))
        {
            throw new ArgumentException("Policy name is required.", nameof(policyName));
        }

        // Snapshot once so all stages process one stable set even if the caller owns a mutable collection.
        CallRecord[] stableInput = records.ToArray();
        if (stableInput.Any(record => record is null))
        {
            throw new ArgumentException("Input records cannot contain null entries.", nameof(records));
        }

        IReadOnlyList<CallRecord> selected = SelectBillableCalls(stableInput, minimumBillableMinutes);
        IReadOnlyList<CallQuote> priced = PriceCalls(selected, ratePerMinutePolicy, connectionFee);
        return BuildInvoiceSummary(priced, policyName);
    }
}
