using System.Globalization;
using CallPricing.Domain;

namespace CallPricing.Input;

/// <summary>
/// Pure parser for the assignment's simple CSV format: CallId,DurationMinutes,Type.
/// CSV quoting is intentionally not supported because fields in this schema contain no commas.
/// </summary>
public static class CallCsvParser
{
    private static readonly string[] ExpectedHeaders = ["CallId", "DurationMinutes", "Type"];

    public static CallParseResult ParseLines(IEnumerable<string?>? lines)
    {
        if (lines is null)
        {
            throw new ArgumentNullException(nameof(lines), "CSV lines are required; an empty dataset must still include the header.");
        }

        string?[] materialized = lines.ToArray();
        if (materialized.Length == 0 || materialized[0] is null)
        {
            throw new FormatException("CSV input is missing its header row.");
        }

        string[] headers = materialized[0]!.Split(',').Select(part => part.Trim()).ToArray();
        if (headers.Length != ExpectedHeaders.Length ||
            !headers.SequenceEqual(ExpectedHeaders, StringComparer.OrdinalIgnoreCase))
        {
            throw new FormatException("CSV header must be: CallId,DurationMinutes,Type");
        }

        var accepted = new List<CallRecord>();
        var rejected = new List<RejectedCall>();

        for (int index = 1; index < materialized.Length; index++)
        {
            int lineNumber = index + 1;
            string? rawLine = materialized[index];
            if (rawLine is null)
            {
                rejected.Add(new RejectedCall(lineNumber, string.Empty, "Row is missing."));
                continue;
            }

            string[] fields = rawLine.Split(',');
            if (fields.Length != 3)
            {
                rejected.Add(new RejectedCall(lineNumber, rawLine, "Expected exactly three comma-separated fields."));
                continue;
            }

            string callId = fields[0].Trim();
            if (string.IsNullOrWhiteSpace(callId))
            {
                rejected.Add(new RejectedCall(lineNumber, rawLine, "CallId is required."));
                continue;
            }

            if (!int.TryParse(fields[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int duration))
            {
                rejected.Add(new RejectedCall(lineNumber, rawLine, "DurationMinutes must be an integer from 0 through 300."));
                continue;
            }

            if (duration is < 0 or > 300)
            {
                rejected.Add(new RejectedCall(lineNumber, rawLine, "DurationMinutes must be between 0 and 300, inclusive."));
                continue;
            }

            if (!Enum.TryParse(fields[2].Trim(), ignoreCase: true, out CallType type) ||
                !Enum.IsDefined(typeof(CallType), type))
            {
                rejected.Add(new RejectedCall(lineNumber, rawLine, "Type must be Local or International."));
                continue;
            }

            accepted.Add(new CallRecord(callId, duration, type));
        }

        return new CallParseResult(accepted, rejected);
    }
}
