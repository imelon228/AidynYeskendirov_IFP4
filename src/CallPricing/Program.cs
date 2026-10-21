using System.Globalization;
using CallPricing.Input;
using CallPricing.Pipeline;
using CallPricing.Pricing;

namespace CallPricing;

internal static class Program
{
    private static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        string inputPath = args.Length > 0 ? args[0] : Path.Combine("data", "sample-calls.csv");

        try
        {
            string[] lines = File.ReadAllLines(inputPath);
            CallParseResult parsed = CallCsvParser.ParseLines(lines);

            Console.WriteLine("CALL COST QUOTE PIPELINE | Assignment 4");
            Console.WriteLine($"Input file: {inputPath}");
            Console.WriteLine($"Valid input records: {parsed.Records.Count}");
            Console.WriteLine($"Rejected input rows: {parsed.RejectedRows.Count}");

            foreach (var rejected in parsed.RejectedRows)
            {
                Console.WriteLine($"  REJECT line {rejected.LineNumber}: {rejected.Reason} | {rejected.RawLine}");
            }

            Console.WriteLine();
            DisplaySummary(CallPricingPipeline.Run(
                parsed.Records,
                "Standard",
                PricingPolicies.StandardRatePerMinute,
                CallPricingPipeline.DefaultMinimumBillableMinutes,
                CallPricingPipeline.DefaultConnectionFee));

            Console.WriteLine();
            DisplaySummary(CallPricingPipeline.Run(
                parsed.Records,
                "Saver",
                PricingPolicies.SaverRatePerMinute,
                CallPricingPipeline.DefaultMinimumBillableMinutes,
                CallPricingPipeline.DefaultConnectionFee));

            if (parsed.Records.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine("No valid calls were available; both policies produced empty summaries.");
            }

            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            Console.Error.WriteLine($"Input error: {exception.Message}");
            Console.Error.WriteLine("Usage: dotnet run --project src/CallPricing -- [path-to-csv]");
            return 2;
        }
    }

    private static void DisplaySummary(CallPricing.Domain.CallInvoiceSummary summary)
    {
        Console.WriteLine($"Policy: {summary.PolicyName}");
        Console.WriteLine("Call ID | Type | Minutes | Rate/min | Fee | Total");
        Console.WriteLine(new string('-', 62));

        if (summary.Quotes.Count == 0)
        {
            Console.WriteLine("(no billable calls)");
        }
        else
        {
            foreach (var quote in summary.Quotes)
            {
                Console.WriteLine(
                    $"{quote.CallId,-7} | {quote.Type,-13} | {quote.DurationMinutes,7} | " +
                    $"{quote.RatePerMinute,8:0.00} | {quote.ConnectionFee,4:0.00} | {quote.TotalCost,5:0.00}");
            }
        }

        Console.WriteLine($"Billable calls: {summary.BillableCallCount}");
        Console.WriteLine($"Total: {summary.TotalCost:0.00}");
    }
}
