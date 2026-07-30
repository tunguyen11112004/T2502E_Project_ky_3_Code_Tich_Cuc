using System;
using System.Collections.Generic;
using System.Linq;
using Bus_ticket.Models;
using Bus_ticket.ViewModels;

namespace Bus_ticket.Helpers;

public class PaymentMethodSummary
{
    public string Method { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TotalAmount { get; set; }
}

public static class PaymentMethodDisplayHelper
{
    public static readonly string CounterPayment = "Thanh toán tại quầy";
    public static readonly string VnpayPayment = "VNPAY (Quét mã QR)";
    public static readonly string MomoPayment = "MOMO (Quét mã QR)";
    public static readonly string PayOsPayment = "Thanh toán chuyển khoản QR (PayOS)";
    public static readonly string OtherPayment = "Khác";

    public static readonly IReadOnlyList<string> CanonicalMethods = new[]
    {
        CounterPayment,
        VnpayPayment,
        MomoPayment,
        PayOsPayment
    };

    public static readonly IReadOnlyDictionary<string, string> TableDotColors =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { CounterPayment, "bg-amber-500" },
            { VnpayPayment, "bg-blue-500" },
            { MomoPayment, "bg-pink-500" },
            { PayOsPayment, "bg-indigo-500" },
            { OtherPayment, "bg-gray-500" }
        };

    public static readonly IReadOnlyList<string> ChartColors = new[]
    {
        "#f59e0b",
        "#3b82f6",
        "#ec4899",
        "#6366f1",
        "#6b7280"
    };

    public static string GetDisplayName(string? dbValue)
    {
        if (string.IsNullOrWhiteSpace(dbValue))
            return CounterPayment;

        var val = dbValue.Trim().ToLowerInvariant();

        if (val is "cash" or "tiền mặt" or "tien mat"
            || val.Contains("quầy", StringComparison.Ordinal)
            || val.Contains("quay", StringComparison.Ordinal))
        {
            return CounterPayment;
        }

        if (val.Contains("vnpay", StringComparison.Ordinal))
            return VnpayPayment;

        if (val.Contains("momo", StringComparison.Ordinal))
            return MomoPayment;

        if (val.Contains("payos", StringComparison.Ordinal)
            || val.Contains("banking", StringComparison.Ordinal)
            || val.Contains("chuyển khoản", StringComparison.Ordinal)
            || val.Contains("chuyen khoan", StringComparison.Ordinal))
        {
            return PayOsPayment;
        }

        return OtherPayment;
    }

    /// <summary>
    /// Suy ra phương thức thanh toán gốc từ PaymentMethod và TransactionCode.
    /// </summary>
    public static string ResolveRawMethod(PaymentInfo? payment)
    {
        if (payment == null)
            return "Cash";

        var method = payment.PaymentMethod?.Trim() ?? string.Empty;
        var txn = payment.TransactionCode?.Trim() ?? string.Empty;
        var combined = $"{method}|{txn}".ToLowerInvariant();

        if (combined.Contains("vnpay", StringComparison.Ordinal))
            return "VnPay";

        if (combined.Contains("momo", StringComparison.Ordinal))
            return "MOMO";

        if (combined.Contains("payos", StringComparison.Ordinal)
            || method.Equals("PAYOS", StringComparison.OrdinalIgnoreCase))
        {
            return "PAYOS";
        }

        if (method.Equals("Banking", StringComparison.OrdinalIgnoreCase))
            return "PAYOS";

        if (string.IsNullOrWhiteSpace(method)
            || method.Equals("Cash", StringComparison.OrdinalIgnoreCase)
            || method.Equals("Tiền mặt", StringComparison.OrdinalIgnoreCase))
        {
            return "Cash";
        }

        return method;
    }

    public static string ResolveRawMethod(string? paymentMethod, string? transactionCode) =>
        ResolveRawMethod(new PaymentInfo
        {
            PaymentMethod = paymentMethod ?? string.Empty,
            TransactionCode = transactionCode ?? string.Empty
        });

    public static List<PaymentMethodSummary> BuildSummaries(IEnumerable<TransactionDetailDto>? transactions)
    {
        var grouped = (transactions ?? Enumerable.Empty<TransactionDetailDto>())
            .GroupBy(x => GetDisplayName(x.PaymentMethod))
            .ToDictionary(g => g.Key, g => new PaymentMethodSummary
            {
                Method = g.Key,
                TransactionCount = g.Count(),
                TotalAmount = g.Sum(x => x.Amount)
            }, StringComparer.OrdinalIgnoreCase);

        var summaries = CanonicalMethods
            .Select(method => grouped.TryGetValue(method, out var summary)
                ? summary
                : new PaymentMethodSummary { Method = method })
            .ToList();

        if (grouped.TryGetValue(OtherPayment, out var otherSummary) && otherSummary.TransactionCount > 0)
        {
            summaries.Add(otherSummary);
        }

        return summaries.OrderByDescending(x => x.TotalAmount).ThenBy(x => x.Method).ToList();
    }

    public static (List<string> Labels, List<decimal> Values, List<string> Colors) BuildChartData(
        IEnumerable<PaymentMethodSummary> summaries)
    {
        var positiveItems = summaries
            .Where(x => x.TotalAmount > 0)
            .ToList();

        var labels = positiveItems.Select(x => x.Method).ToList();
        var values = positiveItems.Select(x => x.TotalAmount).ToList();
        var colors = positiveItems
            .Select(x => TableDotColors.TryGetValue(x.Method, out var cssClass)
                ? CssClassToHex(cssClass)
                : "#6b7280")
            .ToList();

        return (labels, values, colors);
    }

    private static string CssClassToHex(string cssClass) =>
        cssClass switch
        {
            "bg-amber-500" => "#f59e0b",
            "bg-blue-500" => "#3b82f6",
            "bg-pink-500" => "#ec4899",
            "bg-indigo-500" => "#6366f1",
            _ => "#6b7280"
        };
}
