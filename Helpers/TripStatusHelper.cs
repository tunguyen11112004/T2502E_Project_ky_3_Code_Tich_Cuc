using System;
using System.Collections.Generic;

namespace Bus_ticket.Helpers;

public static class TripStatusHelper
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Scheduled"] = "Sẵn sàng đặt vé",
        ["Active"] = "Đang chạy",
        ["Completed"] = "Hoàn thành",
        ["Cancelled"] = "Đã hủy"
    };

    public static string GetDisplayName(string? status) =>
        Labels.TryGetValue(status ?? string.Empty, out var label) ? label : status ?? "—";

    public static string GetBadgeClass(string? status) => (status ?? string.Empty) switch
    {
        "Scheduled" => "text-amber-400",
        "Active" => "text-emerald-400",
        "Completed" => "text-gray-400",
        "Cancelled" => "text-rose-400",
        _ => "text-gray-400"
    };
}
