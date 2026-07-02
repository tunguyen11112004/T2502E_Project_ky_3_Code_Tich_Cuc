using Bus_ticket.Data;
using Bus_ticket.Models;
using Bus_ticket.ViewModels;
using MongoDB.Driver;

namespace Bus_ticket.Services;

public class DashboardService
{
    private readonly ApplicationDbContext _dbContext;

    public DashboardService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RouteRevenueReportViewModel> GetRouteRevenueReportAsync(DateTime fromDate, DateTime toDate)
    {
        var from = fromDate.Date;
        var to = toDate.Date.AddDays(1).AddTicks(-1);

        var fromUtc = from.ToUniversalTime();
        var toUtc = to.ToUniversalTime();

        var bookings = await _dbContext.Bookings
            .Find(b => b.BookingTime >= fromUtc && b.BookingTime <= toUtc)
            .ToListAsync();

        var paidCompletedBookings = bookings
            .Where(b => IsCompletedBooking(b.BookingStatus) && IsPaidBooking(b.PaymentStatus))
            .ToList();

        var trips = await _dbContext.Trips
            .Find(_ => true)
            .ToListAsync();

        var routes = await _dbContext.BusRoutes
            .Find(_ => true)
            .ToListAsync();

        var reportItems = paidCompletedBookings
            .GroupBy(b => b.TripId)
            .Select(group =>
            {
                var trip = trips.FirstOrDefault(t => t.Id == group.Key);
                var route = routes.FirstOrDefault(r => r.Id == trip?.RouteId);

                var routeId = route?.Id ?? "unknown";
                var routeName = route == null
                    ? "Không xác định"
                    : $"{route.DeparturePoint} - {route.DestinationPoint}";

                return new
                {
                    RouteId = routeId,
                    RouteName = routeName,
                    Bookings = group.ToList()
                };
            })
            .GroupBy(x => x.RouteId)
            .Select(group =>
            {
                var first = group.First();

                var allBookingsOfRoute = group
                    .SelectMany(x => x.Bookings)
                    .ToList();

                return new RouteRevenueItemViewModel
                {
                    RouteId = first.RouteId,
                    RouteName = first.RouteName,
                    TotalBookings = allBookingsOfRoute.Count,
                    TotalTickets = allBookingsOfRoute.Sum(b => b.Passengers?.Count ?? 0),
                    TotalRevenue = allBookingsOfRoute.Sum(b => b.FinalAmount)
                };
            })
            .OrderByDescending(x => x.TotalRevenue)
            .ToList();

        var grandTotalRevenue = reportItems.Sum(x => x.TotalRevenue);
        var grandTotalBookings = reportItems.Sum(x => x.TotalBookings);
        var grandTotalTickets = reportItems.Sum(x => x.TotalTickets);

        foreach (var item in reportItems)
        {
            item.Percentage = grandTotalRevenue > 0
                ? Math.Round((double)(item.TotalRevenue / grandTotalRevenue * 100), 2)
                : 0;
        }

        return new RouteRevenueReportViewModel
        {
            FromDate = fromDate.Date,
            ToDate = toDate.Date,
            GrandTotalRevenue = grandTotalRevenue,
            GrandTotalBookings = grandTotalBookings,
            GrandTotalTickets = grandTotalTickets,
            Items = reportItems
        };
    }

    private static bool IsCompletedBooking(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        var value = status.Trim().ToLowerInvariant();

        return value == "completed"
               || value == "complete"
               || value == "success"
               || value == "successful"
               || value == "confirmed";
    }

    private static bool IsPaidBooking(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        var value = status.Trim().ToLowerInvariant();

        return value == "paid"
               || value == "success"
               || value == "successful"
               || value == "completed";
    }
}