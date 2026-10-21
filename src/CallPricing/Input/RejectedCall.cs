namespace CallPricing.Input;

public sealed record RejectedCall(int LineNumber, string RawLine, string Reason);
