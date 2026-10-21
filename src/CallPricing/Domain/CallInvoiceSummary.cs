using System.Collections.ObjectModel;

namespace CallPricing.Domain;

/// <summary>An immutable summary snapshot produced after all pure stages complete.</summary>
public sealed record CallInvoiceSummary
{
    public string PolicyName { get; }
    public int BillableCallCount { get; }
    public decimal TotalCost { get; }
    public ReadOnlyCollection<CallQuote> Quotes { get; }

    public CallInvoiceSummary(
        string policyName,
        IEnumerable<CallQuote> quotes,
        decimal totalCost)
    {
        if (string.IsNullOrWhiteSpace(policyName))
        {
            throw new ArgumentException("Policy name is required.", nameof(policyName));
        }

        ArgumentNullException.ThrowIfNull(quotes);

        CallQuote[] snapshot = quotes.ToArray();
        if (snapshot.Any(quote => quote is null))
        {
            throw new ArgumentException("Quotes cannot contain null entries.", nameof(quotes));
        }

        if (totalCost < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCost), "Total cost cannot be negative.");
        }

        PolicyName = policyName.Trim();
        Quotes = Array.AsReadOnly(snapshot);
        BillableCallCount = snapshot.Length;
        TotalCost = totalCost;
    }
}
