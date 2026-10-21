# AidynYeskendirov_IFP4 — Call Cost Quote Pipeline

**Course:** Introduction to Functional Programming (IFP)  
**Assignment:** 4 — Composing Functions Using Pipelining  
**Target framework:** .NET 8 (`net8.0`)  
**Submission type:** Individual work  
**Student / group:** Aidyn Yeskendirov — Individual (solo submission)

## 1. Topic and intended users

This reusable console module calculates customer call-cost quotes from call records. It could be used by a small telecom billing team or by a support operator who needs a consistent estimate for local and international calls. The project focuses on a single complete workflow rather than a GUI or external database, and its pure calculation stages can be extended in later assignments.

## 2. Inputs and business rules

The input file is a simple CSV with exactly these columns:

```csv
CallId,DurationMinutes,Type
```

- `CallId`: required non-empty identifier; whitespace around the identifier is trimmed.
- `DurationMinutes`: integer from **0 through 300**, inclusive. Zero is valid input, not a parse failure.
- `Type`: `Local` or `International` (case-insensitive).
- CSV fields are not quoted and must not contain commas. Every data row must contain exactly three fields.
- A dataset with only the header is a valid empty dataset. A missing file, missing header, or invalid header is an input error; it is not silently treated as an empty dataset.
- Invalid data rows are rejected with their line number and a readable reason. They do not enter the pricing pipeline.
- The minimum billable duration is **1 minute**. A valid zero-minute call is accepted by the parser but excluded from billing. Exactly one minute is billable.
- A billable call is priced as `duration × selected per-minute rate + connection fee`.
- The connection fee is **0.05** per billable call under both policies.
- Standard policy: Local = **0.10/min**, International = **0.35/min**.
- Saver policy: Local = **0.08/min**, International = **0.30/min**.
- Amounts are represented as `decimal` and rounded to two decimal places. Summary quotes are ordered by descending total cost, with `CallId` as a deterministic tie-breaker.

All monetary values are illustrative sample rates for this programming assignment, not real telecom tariffs.

## 3. Sample dataset and expected results

The main input is `data/sample-calls.csv`. It has five valid records and three invalid records:

| CSV line | Data | Expected treatment |
|---:|---|---|
| 2 | `C-100,2,Local` | Valid; billable |
| 3 | `C-101,4,International` | Valid; billable |
| 4 | `C-102,1,Local` | Valid; exact minimum boundary, billable |
| 5 | `C-103,0,Local` | Valid; excluded because it is below the billable threshold |
| 6 | `C-104,10,International` | Valid; billable |
| 7 | `C-105,301,Local` | Rejected; duration exceeds 300 |
| 8 | `C-106,abc,Local` | Rejected; duration is not an integer |
| 9 | `,3,Local` | Rejected; required call ID is missing |

Expected summaries, using the four billable calls, are:

| Call ID | Standard calculation | Standard total | Saver calculation | Saver total |
|---|---:|---:|---:|---:|
| `C-104` | `10 × 0.35 + 0.05` | 3.55 | `10 × 0.30 + 0.05` | 3.05 |
| `C-101` | `4 × 0.35 + 0.05` | 1.45 | `4 × 0.30 + 0.05` | 1.25 |
| `C-100` | `2 × 0.10 + 0.05` | 0.25 | `2 × 0.08 + 0.05` | 0.21 |
| `C-102` | `1 × 0.10 + 0.05` | 0.15 | `1 × 0.08 + 0.05` | 0.13 |
| **Summary** | **4 billable calls** | **5.40** | **4 billable calls** | **4.64** |

The key policy comparison is the same input producing totals of **5.40** and **4.64**. The pipeline source is unchanged between the runs; only the supplied rate delegate changes.

`data/empty-calls.csv` contains just the header and must produce an empty summary with zero calls and total 0.00. `data/malformed-calls.csv` demonstrates invalid rows while retaining valid rows.

## 4. Pipeline stages and composition

The functional core is in `src/CallPricing/Pipeline/CallPricingPipeline.cs`. The three transformation stages are named and return new read-only snapshots.

### Stage 1 — `SelectBillableCalls`

- **Input:** `IEnumerable<CallRecord>` and an explicit `minimumBillableMinutes` integer.
- **Output:** `IReadOnlyList<CallRecord>`.
- **Rule:** include records whose duration is greater than or equal to the minimum threshold.
- **Example:** at threshold `1`, `C-102` with one minute is included; `C-103` with zero minutes is excluded.

### Stage 2 — `PriceCalls`

- **Input:** selected call records, `Func<CallRecord, decimal>` rate policy, and connection fee.
- **Output:** `IReadOnlyList<CallQuote>`.
- **Rule:** invoke the supplied rate delegate once for every billable call and calculate `duration × rate + fee`; reject negative rates or connection fees.
- **Example:** a two-minute Local call under Standard is `2 × 0.10 + 0.05 = 0.25`.

### Stage 3 — `BuildInvoiceSummary`

- **Input:** calculated quotes and a policy name.
- **Output:** `CallInvoiceSummary` containing ordered quotes, billable call count, and total cost.
- **Rule:** order quotes by descending cost, break ties by call ID, and sum quote totals.
- **Example:** `C-104` at 3.55 is displayed before `C-101` at 1.45 under Standard.

`Run` composes these stages in that order. It snapshots the source records before processing. Records are immutable, pipeline output collections are created from private array snapshots and exposed read-only, and the pipeline does not read the console, perform file I/O, use random values or read the clock. Console and file operations remain in `Program.cs`, outside the functional core.

## 5. Higher-order function and policies

`PriceCalls` accepts a `Func<CallRecord, decimal>` and calls it during processing. `PricingPolicies.StandardRatePerMinute` and `PricingPolicies.SaverRatePerMinute` are the two policy functions. `Program.cs` selects each delegate outside `Run` and passes it in. There is no mutable global setting that switches the policy.

For an individual defense, explain that `Func<CallRecord, decimal>` is the higher-order input; the `ratePerMinutePolicy(call)` line is the delegate invocation; and the returned rate is used to compute a new `CallQuote`. The pricing stage can stay unchanged when the business rate changes.

## 6. Requirements-to-code map

| Assignment requirement | Implementation evidence |
|---|---|
| Task 1: documented workflow and at least five dataset records | This README; `data/sample-calls.csv` |
| Task 2: typed, immutable domain input and validation at the boundary | `Domain/CallRecord.cs`; `Input/CallCsvParser.cs`; `Input/CallParseResult.cs` |
| Distinguish invalid input from an empty dataset | `CallCsvParser.ParseLines`; header-only `data/empty-calls.csv`; missing header raises `FormatException` |
| Task 3: three useful pure stages composed in one workflow | `Pipeline/CallPricingPipeline.cs`: `SelectBillableCalls`, `PriceCalls`, `BuildInvoiceSummary`, and `Run` |
| Task 4: higher-order function with two policies | `PriceCalls`; `Pricing/PricingPolicies.cs`; policy selection in `Program.cs` |
| Task 5: functional core separate from I/O; avoid input mutation | `CallPricingPipeline.cs`; input snapshots; immutable `CallRecord`; read-only result snapshots |
| Automated tests | `tests/CallPricing.Tests/Program.cs` — self-contained assertion-based test runner |
| Sample input and failure demonstration | `data/sample-calls.csv`, `data/empty-calls.csv`, `data/malformed-calls.csv` |
| Exclude generated build output and IDE caches | `.gitignore`; the submitted ZIP omits `bin/` and `obj/` |

## 7. Automated test cases

The test runner is intentionally dependency-free: it compares expected and actual values, reports each case as `PASS` or `FAIL`, and returns a non-zero process exit code if any test fails. Run it using the command below.

1. **Normal workflow:** five valid rows and three rejected rows; Standard has four billable calls and total 5.40.
2. **Empty dataset:** header-only input yields zero quotes and total 0.00.
3. **Boundary value:** one-minute call is included; zero-minute call is valid but not billable.
4. **Invalid rows:** out-of-range duration, malformed number, missing ID, unknown type, and wrong field count produce rejection messages.
5. **Missing required input:** null CSV input, absent CSV header, null pipeline input, and incorrect header are rejected.
6. **Two policies:** same calls produce Standard 5.40 and Saver 4.64, with the same billable count.
7. **Higher-order delegate:** the passed-in rate function is invoked once per billable call and calculates the expected quotes.
8. **Purity / no mutation:** source list and input records remain unchanged after the stages run.
9. **Individual stages:** selection, calculation, ordering, and aggregation are checked independently.
10. **Invalid policy result:** a negative per-minute rate is rejected.

**Latest test outcome:** the suite is included but could not be executed in the packaging environment because the .NET SDK/runtime is not installed there (`dotnet` is unavailable). No test-pass claim is made. Run the test command from a clean extraction and record the actual result before submission.

## 8. Build, run, and test instructions

### Prerequisites

- .NET 8 SDK (an 8.0.x SDK is recommended). Check it with `dotnet --version`.
- No third-party NuGet packages are required.
- Run the commands from the extracted project root, the directory containing `AidynYeskendirov_IFP4.sln` and `README.md`.

### Build

```bash
dotnet restore AidynYeskendirov_IFP4.sln
dotnet build AidynYeskendirov_IFP4.sln --configuration Release
```

### Run the normal demonstration

```bash
dotnet run --project src/CallPricing/CallPricing.csproj -- data/sample-calls.csv
```

Expected key output: `Valid input records: 5`, `Rejected input rows: 3`, Standard `Total: 5.40`, Saver `Total: 4.64`. Exact row formatting may vary by terminal.

### Run the empty dataset demonstration

```bash
dotnet run --project src/CallPricing/CallPricing.csproj -- data/empty-calls.csv
```

Expected result: no billable calls under either policy, with totals of `0.00` and a clear empty-data message.

### Run the malformed-data demonstration

```bash
dotnet run --project src/CallPricing/CallPricing.csproj -- data/malformed-calls.csv
```

Expected result: valid rows are processed, invalid rows show their line number and rejection reason.

### Run automated tests

```bash
dotnet run --project tests/CallPricing.Tests/CallPricing.Tests.csproj
```

Expected result: ten `PASS` lines followed by `Test result: 10 passed, 0 failed.` If any test fails, the runner exits with code 1.

## 9. Defense walkthrough

1. Run the normal input and point out that parsing/rejection happens before pricing.
2. Trace `C-102`: the parser creates a `CallRecord`; Stage 1 retains its one-minute duration; Stage 2 calculates 0.15 under Standard and 0.13 under Saver; Stage 3 includes it in the ordered summary.
3. Open `PriceCalls` and point to the `Func<CallRecord, decimal>` parameter and `ratePerMinutePolicy(call)` invocation.
4. Explain that the pure stages only transform explicit inputs into new values. `Program.cs` owns the file read and console output.
5. Run the empty input and automated test runner.
6. Practice a small live change: change the Local Saver rate from `0.08m` to `0.09m` in `PricingPolicies.SaverRatePerMinute`, then update the expected Saver total assertion. This demonstrates that policy behavior is configurable without rewriting the pipeline.

## 10. Individual contribution

- **Name:** Aidyn Yeskendirov
- **Group:** Individual submission
- **Implemented logic:** typed call record and quote models; CSV parsing and row rejection; the three pure pipeline stages; configurable Standard and Saver rate delegates; console workflow.
- **Tests:** contributed the ten automated scenarios covering normal and empty workflows, the threshold boundary, invalid and missing inputs, policy comparison, delegate invocation, stage results, and input purity.
- **Documentation:** documented domain assumptions, expected sample outputs, stage contracts, requirement mapping, commands, and defense notes in this README.

The solution and tests are a runnable snapshot intended to be retained and extended in the next assignment. Before submitting, run the commands above from a clean extraction and verify the test output in the target .NET 8 environment.
