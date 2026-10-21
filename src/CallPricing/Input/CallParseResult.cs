using System.Collections.ObjectModel;
using CallPricing.Domain;

namespace CallPricing.Input;

/// <summary>Accepted domain records and rejected raw rows, copied into read-only snapshots.</summary>
public sealed record CallParseResult
{
    public ReadOnlyCollection<CallRecord> Records { get; }
    public ReadOnlyCollection<RejectedCall> RejectedRows { get; }

    public CallParseResult(IEnumerable<CallRecord> records, IEnumerable<RejectedCall> rejectedRows)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(rejectedRows);

        CallRecord[] recordSnapshot = records.ToArray();
        RejectedCall[] rejectedSnapshot = rejectedRows.ToArray();

        if (recordSnapshot.Any(record => record is null))
        {
            throw new ArgumentException("Accepted records cannot contain null entries.", nameof(records));
        }

        if (rejectedSnapshot.Any(row => row is null))
        {
            throw new ArgumentException("Rejected rows cannot contain null entries.", nameof(rejectedRows));
        }

        Records = Array.AsReadOnly(recordSnapshot);
        RejectedRows = Array.AsReadOnly(rejectedSnapshot);
    }
}
