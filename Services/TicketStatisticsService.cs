using Bus_ticket.Data;
using Bus_ticket.Models;
using Bus_ticket.ViewModels;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Bus_ticket.Services;

public class TicketStatisticsService
{
    private readonly ApplicationDbContext _context;

    // Chỉ lấy đúng trạng thái thành công trong DB hiện tại
    private static readonly string[] SuccessfulStatuses =
    {
        "completed"
    };

    // Chỉ lấy đúng trạng thái hủy trong DB hiện tại
    private static readonly string[] CancelledStatuses =
    {
        "cancelled"
    };

    public TicketStatisticsService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TicketStatusStatisticsViewModel> GetTicketStatusStatisticsAsync(
        DateTime fromDate,
        DateTime toDate)
    {
        var from = fromDate.Date;
        var to = toDate.Date.AddDays(1).AddTicks(-1);

        // Giữ ngày theo UTC để khớp với MongoDB ISODate
        var fromUtc = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(to, DateTimeKind.Utc);

        var allStatuses = SuccessfulStatuses
            .Concat(CancelledStatuses)
            .Distinct()
            .ToArray();

        var pipeline = PipelineDefinition<Booking, BsonDocument>.Create(new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                {
                    "bookingTime", new BsonDocument
                    {
                        { "$gte", fromUtc },
                        { "$lte", toUtc }
                    }
                }
            }),

            new BsonDocument("$addFields", new BsonDocument
            {
                {
                    "normalizedStatus", new BsonDocument("$toLower", new BsonDocument("$ifNull", new BsonArray
                    {
                        "$bookingStatus",
                        string.Empty
                    }))
                }
            }),

            new BsonDocument("$match", new BsonDocument
            {
                {
                    "normalizedStatus", new BsonDocument("$in", new BsonArray(allStatuses))
                }
            }),

            new BsonDocument("$group", new BsonDocument
            {
                {
                    "_id", new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$in", new BsonArray
                        {
                            "$normalizedStatus",
                            new BsonArray(SuccessfulStatuses)
                        }),
                        "successful",
                        "cancelled"
                    })
                },
                {
                    "bookingCount", new BsonDocument("$sum", 1)
                },
                {
                    "ticketCount", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$and", new BsonArray
                        {
                            new BsonDocument("$isArray", "$passengers"),
                            new BsonDocument("$gt", new BsonArray
                            {
                                new BsonDocument("$size", "$passengers"),
                                0
                            })
                        }),
                        new BsonDocument("$size", "$passengers"),
                        1
                    }))
                }
            })
        });

        var aggregationResult = await _context.Bookings
            .Aggregate(pipeline)
            .ToListAsync();

        var successfulBookings = 0;
        var cancelledBookings = 0;
        var successfulTickets = 0;
        var cancelledTickets = 0;

        foreach (var item in aggregationResult)
        {
            var statusKey = item.GetValue("_id", string.Empty).AsString;
            var bookingCount = item.GetValue("bookingCount", 0).ToInt32();
            var ticketCount = item.GetValue("ticketCount", 0).ToInt32();

            if (statusKey == "successful")
            {
                successfulBookings += bookingCount;
                successfulTickets += ticketCount;
            }
            else if (statusKey == "cancelled")
            {
                cancelledBookings += bookingCount;
                cancelledTickets += ticketCount;
            }
        }

        var totalTickets = successfulTickets + cancelledTickets;

        var successfulPercentage = totalTickets > 0
            ? Math.Round((double)successfulTickets / totalTickets * 100, 2)
            : 0;

        var cancelledPercentage = totalTickets > 0
            ? Math.Round((double)cancelledTickets / totalTickets * 100, 2)
            : 0;

        return new TicketStatusStatisticsViewModel
        {
            FromDate = from,
            ToDate = toDate.Date,

            SuccessfulBookings = successfulBookings,
            CancelledBookings = cancelledBookings,

            SuccessfulTickets = successfulTickets,
            CancelledTickets = cancelledTickets,

            SuccessfulPercentage = successfulPercentage,
            CancelledPercentage = cancelledPercentage,

            Items = new List<TicketStatusStatisticsItemViewModel>
            {
                new()
                {
                    StatusKey = "successful",
                    StatusLabel = "Successful Tickets",
                    BookingCount = successfulBookings,
                    TicketCount = successfulTickets,
                    Percentage = successfulPercentage
                },
                new()
                {
                    StatusKey = "cancelled",
                    StatusLabel = "Cancelled Tickets",
                    BookingCount = cancelledBookings,
                    TicketCount = cancelledTickets,
                    Percentage = cancelledPercentage
                }
            }
        };
    }
}