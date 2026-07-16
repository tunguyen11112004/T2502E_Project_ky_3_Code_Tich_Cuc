using Bus_ticket.Data;
using Bus_ticket.Models;
using Bus_ticket.ViewModels;
using MongoDB.Driver;

namespace Bus_ticket.Services;

public class VehicleRevenueStatisticsService
{
    private readonly ApplicationDbContext _dbContext;

    public VehicleRevenueStatisticsService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<VehicleRevenueStatisticsViewModel> GetVehicleRevenueStatisticsAsync(DateTime fromDate, DateTime toDate)
    {
        var (fromUtc, toUtc) = ToUtcDateRange(fromDate, toDate);

        var bookings = await _dbContext.Bookings
            .Find(booking => booking.BookingTime >= fromUtc && booking.BookingTime <= toUtc)
            .ToListAsync();

        var validBookings = bookings
            .Where(booking => IsCompletedBooking(booking.BookingStatus) && IsPaidBooking(booking.PaymentStatus))
            .ToList();

        var trips = await _dbContext.Trips.Find(_ => true).ToListAsync();
        var buses = await _dbContext.Buses.Find(_ => true).ToListAsync();
        var busClasses = await _dbContext.BusClasses.Find(_ => true).ToListAsync();

        var tripDictionary = trips
            .Where(trip => !string.IsNullOrWhiteSpace(trip.Id))
            .GroupBy(trip => trip.Id)
            .ToDictionary(group => group.Key, group => group.First());

        var busDictionary = buses
            .Where(bus => !string.IsNullOrWhiteSpace(bus.Id))
            .GroupBy(bus => bus.Id)
            .ToDictionary(group => group.Key, group => group.First());

        var busClassDictionary = busClasses
            .Where(busClass => !string.IsNullOrWhiteSpace(busClass.Id))
            .GroupBy(busClass => busClass.Id)
            .ToDictionary(group => group.Key, group => group.First());

        var mappedBookings = validBookings
            .Select(booking =>
            {
                tripDictionary.TryGetValue(booking.TripId, out var trip);

                var busId = trip?.BusId ?? "unknown";
                busDictionary.TryGetValue(busId, out var bus);

                BusClass? busClass = null;
                if (!string.IsNullOrWhiteSpace(bus?.BusClassId))
                {
                    busClassDictionary.TryGetValue(bus.BusClassId, out busClass);
                }

                var busType = busClass?.BusType
                              ?? bus?.LegacyBusType
                              ?? "Không xác định";

                var busClassName = busClass?.ClassName
                                    ?? bus?.LegacyBusType
                                    ?? "Không xác định";

                return new
                {
                    Booking = booking,
                    BusId = bus?.Id ?? "unknown",
                    BusCode = bus?.BusCode ?? "Chưa xác định",
                    LicensePlate = bus?.LicensePlate ?? "Chưa xác định",
                    BusType = busType,
                    BusClassName = busClassName
                };
            })
            .ToList();

        var busItems = mappedBookings
            .GroupBy(item => new
            {
                item.BusId,
                item.BusCode,
                item.LicensePlate,
                item.BusType,
                item.BusClassName
            })
            .Select(group =>
            {
                var groupBookings = group.Select(item => item.Booking).ToList();

                return new VehicleRevenueByBusViewModel
                {
                    BusId = group.Key.BusId,
                    BusCode = group.Key.BusCode,
                    LicensePlate = group.Key.LicensePlate,
                    BusType = group.Key.BusType,
                    BusClassName = group.Key.BusClassName,
                    TotalBookings = groupBookings.Count,
                    TotalTickets = groupBookings.Sum(GetTicketCount),
                    TotalRevenue = groupBookings.Sum(booking => booking.FinalAmount)
                };
            })
            .OrderByDescending(item => item.TotalRevenue)
            .ThenByDescending(item => item.TotalTickets)
            .ThenByDescending(item => item.TotalBookings)
            .ThenBy(item => item.BusCode)
            .ToList();

        var typeItems = mappedBookings
            .GroupBy(item => new
            {
                item.BusType,
                item.BusClassName
            })
            .Select(group =>
            {
                var groupBookings = group.Select(item => item.Booking).ToList();

                return new VehicleRevenueByBusTypeViewModel
                {
                    BusType = group.Key.BusType,
                    BusClassName = group.Key.BusClassName,
                    TotalBookings = groupBookings.Count,
                    TotalTickets = groupBookings.Sum(GetTicketCount),
                    TotalRevenue = groupBookings.Sum(booking => booking.FinalAmount)
                };
            })
            .OrderByDescending(item => item.TotalRevenue)
            .ThenByDescending(item => item.TotalTickets)
            .ThenByDescending(item => item.TotalBookings)
            .ThenBy(item => item.BusType)
            .ToList();

        var grandTotalRevenue = busItems.Sum(item => item.TotalRevenue);
        var grandTotalBookings = busItems.Sum(item => item.TotalBookings);
        var grandTotalTickets = busItems.Sum(item => item.TotalTickets);

        foreach (var item in busItems)
        {
            item.Percentage = grandTotalRevenue > 0
                ? Math.Round((double)(item.TotalRevenue / grandTotalRevenue * 100), 2)
                : 0;
        }

        foreach (var item in typeItems)
        {
            item.Percentage = grandTotalRevenue > 0
                ? Math.Round((double)(item.TotalRevenue / grandTotalRevenue * 100), 2)
                : 0;
        }

        return new VehicleRevenueStatisticsViewModel
        {
            FromDate = fromDate.Date,
            ToDate = toDate.Date,
            GrandTotalRevenue = grandTotalRevenue,
            GrandTotalBookings = grandTotalBookings,
            GrandTotalTickets = grandTotalTickets,
            BusRevenueItems = busItems,
            BusTypeRevenueItems = typeItems
        };
    }

    private static (DateTime FromUtc, DateTime ToUtc) ToUtcDateRange(DateTime fromDate, DateTime toDate)
    {
        var from = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(toDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        return (from, to);
    }

    private static bool IsCompletedBooking(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;

        var value = status.Trim().ToLowerInvariant();

        return value == "completed"
               || value == "complete"
               || value == "success"
               || value == "successful"
               || value == "confirmed";
    }

    private static bool IsPaidBooking(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;

        var value = status.Trim().ToLowerInvariant();

        return value == "paid"
               || value == "completed"
               || value == "success"
               || value == "successful";
    }

    private static int GetTicketCount(Booking booking)
    {
        return booking.Passengers?.Count > 0 ? booking.Passengers.Count : 1;
    }
}