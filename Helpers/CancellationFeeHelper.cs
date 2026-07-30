using System;

namespace Bus_ticket.Helpers;

public static class CancellationFeeHelper
{
    public static decimal GetPenaltyPercent(DateTime departureTimeUtc)
    {
        var hoursDifference = (departureTimeUtc - DateTime.UtcNow).TotalHours;
        if (hoursDifference >= 48) return 0m;
        if (hoursDifference >= 24) return 0.15m;
        return 0.30m;
    }

    public static decimal CalculatePenaltyAmount(decimal ticketAmount, DateTime departureTimeUtc)
    {
        return ticketAmount * GetPenaltyPercent(departureTimeUtc);
    }

    public static string GetPenaltyPercentDisplay(DateTime departureTimeUtc)
    {
        var percent = GetPenaltyPercent(departureTimeUtc);
        if (percent == 0m) return "Miễn phí (0%)";
        if (percent == 0.15m) return "15%";
        return "30%";
    }

    public static bool IsPastDeparture(DateTime departureTimeUtc)
    {
        return DateTime.UtcNow >= departureTimeUtc;
    }
}
