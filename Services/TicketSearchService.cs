using System.Text.RegularExpressions;
using Bus_ticket.Data;
using Bus_ticket.DTOs;
using Bus_ticket.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Bus_ticket.Services;

public class TicketSearchService : ITicketSearchService
{
    private const int SeatCount = 40;
    private const string ActiveTripStatus = "Scheduled";

    private readonly ApplicationDbContext _db;

    public TicketSearchService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<TicketSearchResponse> SearchAsync(TicketSearchQuery query)
    {
        var routeFilter = BuildRouteFilter(query.From, query.To);
        var routes = await _db.BusRoutes.Find(routeFilter).ToListAsync();

        var hasRouteFilter = !string.IsNullOrWhiteSpace(query.From) || !string.IsNullOrWhiteSpace(query.To);
        if (hasRouteFilter && routes.Count == 0)
        {
            return new TicketSearchResponse();
        }

        var routeMap = routes.ToDictionary(r => r.Id, r => r);
        var routeIds = routes.Select(r => r.Id).ToList();

        var tripFilter = BuildTripFilter(routeIds, query.Date, hasRouteFilter);
        var trips = await _db.Trips.Find(tripFilter).SortBy(t => t.DepartureTime).ToListAsync();

        if (trips.Count == 0)
        {
            return new TicketSearchResponse();
        }

        var busIds = trips.Select(t => t.BusId).Distinct().ToList();
        var buses = await _db.Buses
            .Find(Builders<Bus>.Filter.In(b => b.Id, busIds))
            .ToListAsync();
        var busMap = buses.ToDictionary(b => b.Id, b => b);

        var busClassIds = buses.Select(b => b.BusClassId).Distinct().ToList();
        var busClasses = await _db.BusClasses
            .Find(Builders<BusClass>.Filter.In(c => c.Id, busClassIds))
            .ToListAsync();
        var busClassMap = busClasses.ToDictionary(c => c.Id, c => c);

        var results = new List<TicketSearchResultDto>();

        foreach (var trip in trips)
        {
            if (!busMap.TryGetValue(trip.BusId, out var bus))
            {
                continue;
            }

            busClassMap.TryGetValue(bus.BusClassId, out var busClass);
            routeMap.TryGetValue(trip.RouteId, out var route);

            results.Add(new TicketSearchResultDto
            {
                TripCode = trip.Id,
                LicensePlate = bus.LicensePlate,
                BusClass = busClass?.BusType ?? busClass?.ClassName ?? string.Empty,
                DepartureTime = trip.DepartureTime,
                DeparturePoint = route?.DeparturePoint ?? string.Empty,
                DestinationPoint = route?.DestinationPoint ?? string.Empty,
                Seats = BuildSeatStatuses(trip.RealtimeSeats, busClass?.DefaultLayout)
            });
        }

        return new TicketSearchResponse
        {
            Total = results.Count,
            Data = results
        };
    }

    private static FilterDefinition<BusRoute> BuildRouteFilter(string? from, string? to)
    {
        var filter = Builders<BusRoute>.Filter.Empty;

        if (!string.IsNullOrWhiteSpace(from))
        {
            filter &= Builders<BusRoute>.Filter.Regex(r => r.DeparturePoint, BuildContainsRegex(from));
        }

        if (!string.IsNullOrWhiteSpace(to))
        {
            filter &= Builders<BusRoute>.Filter.Regex(r => r.DestinationPoint, BuildContainsRegex(to));
        }

        return filter;
    }

    private static BsonRegularExpression BuildContainsRegex(string value)
    {
        return new BsonRegularExpression(Regex.Escape(value.Trim()), "i");
    }

    private static FilterDefinition<Trip> BuildTripFilter(
        List<string> routeIds,
        DateTime? date,
        bool hasRouteFilter)
    {
        var filter = Builders<Trip>.Filter.Eq(t => t.Status, ActiveTripStatus);

        if (hasRouteFilter && routeIds.Count > 0)
        {
            filter &= Builders<Trip>.Filter.In(t => t.RouteId, routeIds);
        }

        if (date.HasValue)
        {
            var start = date.Value.Date;
            var end = start.AddDays(1);
            filter &= Builders<Trip>.Filter.Gte(t => t.DepartureTime, start)
                     & Builders<Trip>.Filter.Lt(t => t.DepartureTime, end);
        }

        return filter;
    }

    private static List<SeatStatusDto> BuildSeatStatuses(
        List<RealtimeSeat> realtimeSeats,
        List<SeatTemplate>? seatLayout)
    {
        var seatMap = realtimeSeats.ToDictionary(s => s.SeatNumber, s => s, StringComparer.OrdinalIgnoreCase);
        var layout = seatLayout is { Count: > 0 }
            ? seatLayout
            : GenerateDefaultLayout(SeatCount);

        return layout
            .Take(SeatCount)
            .Select(seat =>
            {
                seatMap.TryGetValue(seat.SeatNumber, out var realtime);
                return new SeatStatusDto
                {
                    SeatNumber = seat.SeatNumber,
                    Status = ResolveSeatStatus(realtime)
                };
            })
            .ToList();
    }

    private static string ResolveSeatStatus(RealtimeSeat? seat)
    {
        if (seat == null)
        {
            return "Available";
        }

        if (seat.Status.Equals("Holding", StringComparison.OrdinalIgnoreCase)
            && seat.HeldUntil.HasValue
            && seat.HeldUntil.Value <= DateTime.UtcNow)
        {
            return "Available";
        }

        return seat.Status;
    }

    private static List<SeatTemplate> GenerateDefaultLayout(int totalSeats)
    {
        var seats = new List<SeatTemplate>();
        var rows = totalSeats / 4;

        for (var row = 1; row <= rows; row++)
        {
            for (var col = 1; col <= 4; col++)
            {
                seats.Add(new SeatTemplate
                {
                    SeatNumber = $"{row}{GetColumnLabel(col)}",
                    Row = row,
                    Column = col,
                    Floor = 1
                });
            }
        }

        return seats;
    }

    private static string GetColumnLabel(int column) => column switch
    {
        1 => "A",
        2 => "B",
        3 => "C",
        4 => "D",
        _ => column.ToString()
    };
}
