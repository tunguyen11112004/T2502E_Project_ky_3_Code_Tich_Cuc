using System;

namespace Bus_ticket.Helpers;

public static class VietnamTimeHelper
{
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    public static TimeZoneInfo Zone => VietnamTimeZone;

    public static DateTime ToVietnamLocal(DateTime dateTime)
    {
        var utc = dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(utc, VietnamTimeZone);
    }

    public static (DateTime FromUtc, DateTime ToUtc) ToUtcDateRange(DateTime fromDate, DateTime toDate)
    {
        var fromLocalDate = fromDate.Date;
        var toLocalDate = toDate.Date;

        if (fromLocalDate > toLocalDate)
        {
            (fromLocalDate, toLocalDate) = (toLocalDate, fromLocalDate);
        }

        var fromLocal = DateTime.SpecifyKind(fromLocalDate, DateTimeKind.Unspecified);
        var toLocalEnd = DateTime.SpecifyKind(toLocalDate.AddDays(1).AddTicks(-1), DateTimeKind.Unspecified);

        return (
            TimeZoneInfo.ConvertTimeToUtc(fromLocal, VietnamTimeZone),
            TimeZoneInfo.ConvertTimeToUtc(toLocalEnd, VietnamTimeZone));
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        foreach (var timeZoneId in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "Vietnam",
            TimeSpan.FromHours(7),
            "Vietnam Standard Time",
            "Vietnam Standard Time");
    }
}
