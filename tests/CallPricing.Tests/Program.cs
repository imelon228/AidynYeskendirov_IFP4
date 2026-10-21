using CallPricing.Domain;
using CallPricing.Input;
using CallPricing.Pipeline;
using CallPricing.Pricing;

namespace CallPricing.Tests;

internal static class Program
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        RunTest("normal workflow returns documented Standard total", NormalWorkflowReturnsExpectedStandardTotal);
        RunTest("empty dataset returns an empty summary", EmptyDatasetReturnsEmptySummary);
        RunTest("exact one-minute boundary is included and zero is excluded", BoundaryValueIsHandled);
        RunTest("invalid rows are rejected with reasons", InvalidRowsAreRejected);
        RunTest("missing required input is rejected", MissingRequiredInputIsRejected);
        RunTest("both pricing policies run on the same input and differ", PoliciesDifferForSameInput);
        RunTest("rate policy delegate is invoked once per billable call", HigherOrderFunctionInvokesDelegate);
        RunTest("stages do not change caller-owned input collections", PipelinePreservesCallerInput);
        RunTest("individual stages produce the expected transformation", IndividualStagesWork);
        RunTest("negative policy rate is rejected", NegativeRateIsRejected);

        Console.WriteLine();
        Console.WriteLine($"Test result: {_passed} passed, {_failed} failed.");
        return _failed == 0 ? 0 : 1;
    }

    private static void NormalWorkflowReturnsExpectedStandardTotal()
    {
        CallParseResult parsed = ParseSample();
        CallInvoiceSummary result = RunStandard(parsed.Records);

        AssertEqual(5, parsed.Records.Count, "valid records");
        AssertEqual(3, parsed.RejectedRows.Count, "rejected rows");
        AssertEqual(4, result.BillableCallCount, "billable count");
        AssertEqual(5.40m, result.TotalCost, "Standard total");
        AssertEqual("C-104", result.Quotes[0].CallId, "most expensive first");
        AssertEqual(3.55m, result.Quotes[0].TotalCost, "C-104 Standard quote");
    }

    private static void EmptyDatasetReturnsEmptySummary()
    {
        CallParseResult parsed = CallCsvParser.ParseLines(new[] { "CallId,DurationMinutes,Type" });
        CallInvoiceSummary result = RunStandard(parsed.Records);

        AssertEqual(0, parsed.Records.Count, "parsed record count");
        AssertEqual(0, result.BillableCallCount, "billable count");
        AssertEqual(0m, result.TotalCost, "total cost");
        AssertEqual(0, result.Quotes.Count, "quote count");
    }

    private static void BoundaryValueIsHandled()
    {
        var source = new[]
        {
            new CallRecord("ZERO", 0, CallType.Local),
            new CallRecord("MIN", 1, CallType.Local),
            new CallRecord("OVER", 2, CallType.Local)
        };

        IReadOnlyList<CallRecord> selected = CallPricingPipeline.SelectBillableCalls(source, 1);
        AssertEqual(2, selected.Count, "selected count");
        AssertTrue(selected.All(call => call.DurationMinutes >= 1), "all selected calls meet the threshold");
        AssertTrue(selected.Any(call => call.CallId == "MIN"), "the exact threshold is included");
        AssertTrue(selected.All(call => call.CallId != "ZERO"), "zero duration is valid input but not billable");
    }

    private static void InvalidRowsAreRejected()
    {
        CallParseResult parsed = CallCsvParser.ParseLines(new string?[]
        {
            "CallId,DurationMinutes,Type",
            "GOOD,2,Local",
            "TOO-LONG,301,Local",
            "NOT-NUMERIC,abc,Local",
            ",3,Local",
            "BAD-TYPE,2,National",
            "TOO-MANY,1,Local,extra"
        });

        AssertEqual(1, parsed.Records.Count, "valid row count");
        AssertEqual(5, parsed.RejectedRows.Count, "rejected row count");
        AssertTrue(parsed.RejectedRows.Any(row => row.Reason.Contains("integer", StringComparison.OrdinalIgnoreCase)),
            "malformed numeric field has a clear reason");
        AssertTrue(parsed.RejectedRows.Any(row => row.Reason.Contains("CallId", StringComparison.Ordinal)),
            "missing ID has a clear reason");
    }

    private static void MissingRequiredInputIsRejected()
    {
        AssertThrows<ArgumentNullException>(
            () => CallCsvParser.ParseLines(null), "null CSV input");
        AssertThrows<FormatException>(
            () => CallCsvParser.ParseLines(Array.Empty<string?>()), "missing CSV header");
        AssertThrows<ArgumentNullException>(
            () => CallPricingPipeline.Run(null, "Standard", PricingPolicies.StandardRatePerMinute, 1, 0.05m),
            "null pipeline input");
        AssertThrows<FormatException>(
            () => CallCsvParser.ParseLines(new[] { "wrong,header,format" }), "wrong CSV header");
    }

    private static void PoliciesDifferForSameInput()
    {
        CallParseResult parsed = ParseSample();
        CallInvoiceSummary standard = CallPricingPipeline.Run(
            parsed.Records, "Standard", PricingPolicies.StandardRatePerMinute, 1, 0.05m);
        CallInvoiceSummary saver = CallPricingPipeline.Run(
            parsed.Records, "Saver", PricingPolicies.SaverRatePerMinute, 1, 0.05m);

        AssertEqual(5.40m, standard.TotalCost, "Standard total");
        AssertEqual(4.64m, saver.TotalCost, "Saver total");
        AssertTrue(standard.TotalCost != saver.TotalCost, "results differ because the policy changed");
        AssertEqual(standard.BillableCallCount, saver.BillableCallCount, "same selected calls");
    }

    private static void HigherOrderFunctionInvokesDelegate()
    {
        var calls = new[]
        {
            new CallRecord("A", 1, CallType.Local),
            new CallRecord("B", 2, CallType.International),
            new CallRecord("ZERO", 0, CallType.Local)
        };
        int invocationCount = 0;
        Func<CallRecord, decimal> policy = call =>
        {
            invocationCount++;
            return call.Type == CallType.Local ? 0.10m : 0.20m;
        };

        IReadOnlyList<CallRecord> selected = CallPricingPipeline.SelectBillableCalls(calls, 1);
        IReadOnlyList<CallQuote> quotes = CallPricingPipeline.PriceCalls(selected, policy, 0.05m);

        AssertEqual(2, invocationCount, "delegate invocation count");
        AssertEqual(2, quotes.Count, "quote count");
        AssertEqual(0.15m, quotes.Single(quote => quote.CallId == "A").TotalCost, "A quote");
        AssertEqual(0.45m, quotes.Single(quote => quote.CallId == "B").TotalCost, "B quote");
    }

    private static void PipelinePreservesCallerInput()
    {
        var source = new List<CallRecord>
        {
            new("KEEP", 2, CallType.Local),
            new("ZERO", 0, CallType.International)
        };
        CallRecord[] before = source.ToArray();

        IReadOnlyList<CallRecord> selected = CallPricingPipeline.SelectBillableCalls(source, 1);
        IReadOnlyList<CallQuote> quotes = CallPricingPipeline.PriceCalls(selected, PricingPolicies.StandardRatePerMinute, 0.05m);
        CallInvoiceSummary summary = CallPricingPipeline.BuildInvoiceSummary(quotes, "Standard");

        AssertSequenceEqual(before, source, "caller input list is unchanged");
        AssertEqual(2, source.Count, "caller list count");
        AssertEqual(1, selected.Count, "a new selected collection was created");
        AssertEqual(0.25m, summary.TotalCost, "summary from immutable input snapshot");
    }

    private static void IndividualStagesWork()
    {
        var records = new[]
        {
            new CallRecord("L1", 1, CallType.Local),
            new CallRecord("I2", 2, CallType.International)
        };

        IReadOnlyList<CallRecord> selected = CallPricingPipeline.SelectBillableCalls(records, 1);
        AssertEqual(2, selected.Count, "Stage 1 selection");

        IReadOnlyList<CallQuote> priced = CallPricingPipeline.PriceCalls(selected, PricingPolicies.StandardRatePerMinute, 0.05m);
        AssertEqual(0.15m, priced.Single(quote => quote.CallId == "L1").TotalCost, "Stage 2 local quote");
        AssertEqual(0.75m, priced.Single(quote => quote.CallId == "I2").TotalCost, "Stage 2 international quote");

        CallInvoiceSummary summary = CallPricingPipeline.BuildInvoiceSummary(priced, "Standard");
        AssertEqual("I2", summary.Quotes[0].CallId, "Stage 3 ordering");
        AssertEqual(0.90m, summary.TotalCost, "Stage 3 total");
    }

    private static void NegativeRateIsRejected()
    {
        var records = new[] { new CallRecord("NEG", 1, CallType.Local) };
        AssertThrows<InvalidOperationException>(
            () => CallPricingPipeline.PriceCalls(records, _ => -0.01m, 0.05m),
            "negative rate returned from policy");
    }

    private static CallParseResult ParseSample() => CallCsvParser.ParseLines(new string?[]
    {
        "CallId,DurationMinutes,Type",
        "C-100,2,Local",
        "C-101,4,International",
        "C-102,1,Local",
        "C-103,0,Local",
        "C-104,10,International",
        "C-105,301,Local",
        "C-106,abc,Local",
        ",3,Local"
    });

    private static CallInvoiceSummary RunStandard(IEnumerable<CallRecord> records) => CallPricingPipeline.Run(
        records,
        "Standard",
        PricingPolicies.StandardRatePerMinute,
        CallPricingPipeline.DefaultMinimumBillableMinutes,
        CallPricingPipeline.DefaultConnectionFee);

    private static void RunTest(string name, Action action)
    {
        try
        {
            action();
            _passed++;
            Console.WriteLine($"PASS: {name}");
        }
        catch (Exception exception)
        {
            _failed++;
            Console.WriteLine($"FAIL: {name}");
            Console.WriteLine($"      {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string context)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{context}: expected <{expected}>, actual <{actual}>.");
        }
    }

    private static void AssertTrue(bool condition, string context)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Assertion failed: {context}.");
        }
    }

    private static void AssertSequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string context)
    {
        if (!expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException($"Assertion failed: {context}.");
        }
    }

    private static void AssertThrows<TException>(Action action, string context) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"{context}: expected {typeof(TException).Name}, got {exception.GetType().Name}.", exception);
        }

        throw new InvalidOperationException($"{context}: expected {typeof(TException).Name}, but no exception was thrown.");
    }
}
