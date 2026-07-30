using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bus_ticket.ViewModels;

namespace Bus_ticket.Helpers;

public enum RevenuePeriodGranularity
{
    Day,
    Week,
    Month,
    Year
}

public class RevenuePeriodGroupItem
{
    public string DisplayLabel { get; set; } = string.Empty;
    public string QueryStartDate { get; set; } = string.Empty;
    public string QueryEndDate { get; set; } = string.Empty;
    public int TicketCount { get; set; }
    public decimal Revenue { get; set; }
    public long SortKey { get; set; }
}

public class RevenuePeriodDetailGroupItem : RevenuePeriodGroupItem
{
    public string RouteName { get; set; } = string.Empty;
    public string BusClass { get; set; } = string.Empty;
}

public class RevenuePeriodGroupingResult
{
    public RevenuePeriodGranularity Granularity { get; set; }
    public string ChartTitle { get; set; } = string.Empty;
    public string ChartSubtitle { get; set; } = string.Empty;
    public string TableTitle { get; set; } = string.Empty;
    public string TableSubtitle { get; set; } = string.Empty;
    public string PeriodColumnHeader { get; set; } = string.Empty;
    public List<RevenuePeriodGroupItem> ChartGroups { get; set; } = new();
    public List<RevenuePeriodDetailGroupItem> DetailGroups { get; set; } = new();
}

public static class RevenuePeriodGroupingHelper
{
    private const int DayThresholdDays = 14;
    private const int WeekThresholdDays = 62;
    private const int YearThresholdDays = 365 * 5 + 1;

    public static RevenuePeriodGranularity ResolveGranularity(DateTime fromDate, DateTime toDate)
    {
        var totalDays = (toDate.Date - fromDate.Date).Days + 1;

        if (totalDays <= DayThresholdDays)
            return RevenuePeriodGranularity.Day;
        if (totalDays <= WeekThresholdDays)
            return RevenuePeriodGranularity.Week;
        if (totalDays <= YearThresholdDays)
            return RevenuePeriodGranularity.Month;

        return RevenuePeriodGranularity.Year;
    }

    public static RevenuePeriodGroupingResult Build(
        IEnumerable<TransactionDetailDto>? transactions,
        DateTime fromDate,
        DateTime toDate)
    {
        var granularity = ResolveGranularity(fromDate, toDate);
        var items = transactions?.ToList() ?? new List<TransactionDetailDto>();
        var labels = GetPeriodLabels(granularity);

        var chartGroups = BuildChartGroups(items, granularity);
        var detailGroups = BuildDetailGroups(items, granularity);

        return new RevenuePeriodGroupingResult
        {
            Granularity = granularity,
            ChartTitle = $"Biến động doanh thu theo {labels.PeriodUnit}",
            ChartSubtitle = $"Click vào điểm trên biểu đồ để mở trang chi tiết thống kê trong {labels.PeriodUnitAccusative} đó",
            TableTitle = $"Thống kê theo {labels.PeriodUnit}",
            TableSubtitle = $"Biến động lượng vé và dòng tiền theo {labels.PeriodUnit}, tuyến đường và hạng xe",
            PeriodColumnHeader = labels.ColumnHeader,
            ChartGroups = chartGroups,
            DetailGroups = detailGroups
        };
    }

    private static (string PeriodUnit, string PeriodUnitAccusative, string ColumnHeader) GetPeriodLabels(
        RevenuePeriodGranularity granularity) =>
        granularity switch
        {
            RevenuePeriodGranularity.Day => ("ngày", "ngày", "NGÀY"),
            RevenuePeriodGranularity.Week => ("tuần", "tuần", "TUẦN"),
            RevenuePeriodGranularity.Month => ("tháng", "tháng", "THÁNG"),
            RevenuePeriodGranularity.Year => ("năm", "năm", "NĂM"),
            _ => ("tuần", "tuần", "TUẦN")
        };

    private static List<RevenuePeriodGroupItem> BuildChartGroups(
        List<TransactionDetailDto> items,
        RevenuePeriodGranularity granularity)
    {
        return items
            .GroupBy(x => GetPeriodKey(x.PaymentDate, granularity))
            .Select(g =>
            {
                var periodDates = g.Select(x => x.PaymentDate.Date).ToList();
                var startDate = periodDates.Min();
                var endDate = periodDates.Max();

                return new RevenuePeriodGroupItem
                {
                    DisplayLabel = FormatPeriodLabel(g.Key, startDate, endDate, granularity),
                    QueryStartDate = startDate.ToString("yyyy-MM-dd"),
                    QueryEndDate = endDate.ToString("yyyy-MM-dd"),
                    TicketCount = g.Count(),
                    Revenue = g.Sum(x => x.Amount),
                    SortKey = GetSortKey(g.Key, granularity)
                };
            })
            .OrderBy(x => x.SortKey)
            .ToList();
    }

    private static List<RevenuePeriodDetailGroupItem> BuildDetailGroups(
        List<TransactionDetailDto> items,
        RevenuePeriodGranularity granularity)
    {
        return items
            .GroupBy(x => new
            {
                Period = GetPeriodKey(x.PaymentDate, granularity),
                RouteName = string.IsNullOrWhiteSpace(x.RouteName) ? "Không xác định" : x.RouteName,
                BusClass = string.IsNullOrWhiteSpace(x.BusClass) ? "Khác" : x.BusClass
            })
            .Select(g =>
            {
                var periodDates = g.Select(x => x.PaymentDate.Date).ToList();
                var startDate = periodDates.Min();
                var endDate = periodDates.Max();

                return new RevenuePeriodDetailGroupItem
                {
                    DisplayLabel = FormatPeriodLabel(g.Key.Period, startDate, endDate, granularity),
                    RouteName = g.Key.RouteName,
                    BusClass = g.Key.BusClass,
                    QueryStartDate = startDate.ToString("yyyy-MM-dd"),
                    QueryEndDate = endDate.ToString("yyyy-MM-dd"),
                    TicketCount = g.Count(),
                    Revenue = g.Sum(x => x.Amount),
                    SortKey = GetSortKey(g.Key.Period, granularity)
                };
            })
            .OrderByDescending(x => x.SortKey)
            .ThenByDescending(x => x.Revenue)
            .ThenBy(x => x.RouteName)
            .ToList();
    }

    private static string GetPeriodKey(DateTime paymentDate, RevenuePeriodGranularity granularity)
    {
        var date = paymentDate.Date;
        var cal = CultureInfo.InvariantCulture.Calendar;

        return granularity switch
        {
            RevenuePeriodGranularity.Day => date.ToString("yyyy-MM-dd"),
            RevenuePeriodGranularity.Week =>
                $"{date.Year}-W{cal.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday):D2}",
            RevenuePeriodGranularity.Month => $"{date.Year}-{date.Month:D2}",
            RevenuePeriodGranularity.Year => date.Year.ToString(),
            _ => date.ToString("yyyy-MM-dd")
        };
    }

    private static long GetSortKey(string periodKey, RevenuePeriodGranularity granularity)
    {
        if (granularity == RevenuePeriodGranularity.Day &&
            DateTime.TryParseExact(periodKey, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return day.Ticks;
        }

        if (granularity == RevenuePeriodGranularity.Week)
        {
            var parts = periodKey.Split("-W");
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out var year) &&
                int.TryParse(parts[1], out var week))
            {
                return year * 100L + week;
            }
        }

        if (granularity == RevenuePeriodGranularity.Month)
        {
            var parts = periodKey.Split('-');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out var year) &&
                int.TryParse(parts[1], out var month))
            {
                return year * 100L + month;
            }
        }

        if (granularity == RevenuePeriodGranularity.Year &&
            int.TryParse(periodKey, out var yearOnly))
        {
            return yearOnly;
        }

        return 0;
    }

    private static string FormatPeriodLabel(
        string periodKey,
        DateTime startDate,
        DateTime endDate,
        RevenuePeriodGranularity granularity)
    {
        var cal = CultureInfo.InvariantCulture.Calendar;

        return granularity switch
        {
            RevenuePeriodGranularity.Day => startDate.ToString("dd/MM/yyyy"),
            RevenuePeriodGranularity.Week =>
                $"Tuần {cal.GetWeekOfYear(startDate, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday)} ({startDate:dd/MM} - {endDate:dd/MM})",
            RevenuePeriodGranularity.Month => $"Tháng {startDate:MM/yyyy}",
            RevenuePeriodGranularity.Year => $"Năm {startDate:yyyy}",
            _ => startDate.ToString("dd/MM/yyyy")
        };
    }
}
