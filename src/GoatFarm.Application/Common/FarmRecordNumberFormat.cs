namespace GoatFarm.Application.Common;

/// <summary>V242-style display numbers: PREFIX-yyyyMMdd-DEV-NNN (sequence is caller-owned per day).</summary>
public static class FarmRecordNumberFormat
{
    public static string Format(string prefix, string deviceCode, DateOnly date, int sequence) =>
        $"{prefix.Trim().ToUpperInvariant()}-{date:yyyyMMdd}-{deviceCode.Trim().ToUpperInvariant()}-{sequence:000}";

    /// <summary>Product sale invoices: SF-yyyyMM-DEV-NNNN (per month sequence).</summary>
    public static string FormatSaleInvoice(string deviceCode, DateOnly date, int sequence) =>
        $"SF-{date:yyyyMM}-{deviceCode.Trim().ToUpperInvariant()}-{sequence:0000}";

    /// <summary>Trading sell bills: TR-yyyyMM-DEV-NNN (per month sequence).</summary>
    public static string FormatTradingBill(string deviceCode, DateOnly date, int sequence) =>
        $"TR-{date:yyyyMM}-{deviceCode.Trim().ToUpperInvariant()}-{sequence:000}";
}
