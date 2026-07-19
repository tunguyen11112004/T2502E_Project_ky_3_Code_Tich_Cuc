using Bus_ticket.Data;
using Bus_ticket.ViewModels;
using MongoDB.Driver;

namespace Bus_ticket.Services;

public class LowOccupancyTripsService
{
    private readonly ApplicationDbContext _dbContext;

    public LowOccupancyTripsService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LowOccupancyTripsViewModel> GetLowOccupancyTripsAsync(
        DateTime fromDate,
        DateTime toDate,
        double occupancyThreshold = 40)
    {
        var (fromUtc, toUtc) = ToUtcDateRange(fromDate, toDate);

        var trips = await _dbContext.Trips
            .Find(trip => trip.DepartureTime >= fromUtc && trip.DepartureTime <= toUtc)
            .ToListAsync();

        var routes = await _dbContext.BusRoutes
            .Find(_ => true)
            .ToListAsync();

        var buses = await _dbContext.Buses
            .Find(_ => true)
            .ToListAsync();

        var routeDictionary = routes
            .Where(route => !string.IsNullOrWhiteSpace(route.Id))
            .GroupBy(route => route.Id)
            .ToDictionary(group => group.Key, group => group.First());

        var busDictionary = buses
            .Where(bus => !string.IsNullOrWhiteSpace(bus.Id))
            .GroupBy(bus => bus.Id)
            .ToDictionary(group => group.Key, group => group.First());

        var tripStatistics = trips
            .Select(trip =>
            {
                var seats = trip.RealtimeSeats ?? new();

                var totalSeats = seats.Count;
                var bookedSeats = seats.Count(seat => IsBookedSeat(seat.Status));
                var emptySeats = Math.Max(totalSeats - bookedSeats, 0);

                var occupancyRate = totalSeats > 0
                    ? Math.Round((double)bookedSeats / totalSeats * 100, 2)
                    : 0;

                routeDictionary.TryGetValue(trip.RouteId, out var route);
                busDictionary.TryGetValue(trip.BusId, out var bus);

                var routeName = route == null
                    ? "Không xác định"
                    : $"{route.DeparturePoint} - {route.DestinationPoint}";

                return new LowOccupancyTripItemViewModel
                {
                    TripId = trip.Id,
                    TripCode = trip.TripCode,
                    RouteName = routeName,
                    BusCode = bus?.BusCode ?? "Chưa xác định",
                    LicensePlate = bus?.LicensePlate ?? "Chưa xác định",
                    DepartureTime = trip.DepartureTime,
                    TotalSeats = totalSeats,
                    BookedSeats = bookedSeats,
                    EmptySeats = emptySeats,
                    OccupancyRate = occupancyRate
                };
            })
            .Where(item => item.TotalSeats > 0)
            .ToList();

        var lowOccupancyTrips = tripStatistics
            .Where(item => item.OccupancyRate < occupancyThreshold)
            .OrderBy(item => item.OccupancyRate)
            .ThenByDescending(item => item.EmptySeats)
            .ThenBy(item => item.DepartureTime)
            .ToList();

        var soldOutTrips = tripStatistics
            .Where(item => item.TotalSeats > 0 && item.BookedSeats >= item.TotalSeats)
            .ToList();

        var soldOutTimeFrames = soldOutTrips
            .GroupBy(item => item.DepartureTime.ToString("HH:00"))
            .Select(group => new SoldOutTimeFrameViewModel
            {
                TimeFrame = group.Key,
                SoldOutTripCount = group.Count(),
                TotalSeats = group.Sum(item => item.TotalSeats),
                BookedSeats = group.Sum(item => item.BookedSeats)
            })
            .OrderBy(item => item.TimeFrame)
            .ToList();

        return new LowOccupancyTripsViewModel
        {
            FromDate = fromDate.Date,
            ToDate = toDate.Date,
            OccupancyThreshold = occupancyThreshold,
            TotalTripsChecked = tripStatistics.Count,
            LowOccupancyTripCount = lowOccupancyTrips.Count,
            SoldOutTripCount = soldOutTrips.Count,
            TotalEmptySeats = lowOccupancyTrips.Sum(item => item.EmptySeats),
            AverageOccupancyRate = tripStatistics.Any()
                ? Math.Round(tripStatistics.Average(item => item.OccupancyRate), 2)
                : 0,
            LowOccupancyTrips = lowOccupancyTrips,
            SoldOutTimeFrames = soldOutTimeFrames
        };
    }

    private static (DateTime FromUtc, DateTime ToUtc) ToUtcDateRange(DateTime fromDate, DateTime toDate)
    {
        var from = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(toDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        return (from, to);
    }

    private static bool IsBookedSeat(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;

        var value = status.Trim().ToLowerInvariant();

        return value == "booked"
               || value == "sold"
               || value == "paid"
               || value == "reserved";
    }
}