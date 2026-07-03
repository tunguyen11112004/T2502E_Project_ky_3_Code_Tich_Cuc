using Bus_ticket.Data;
using Bus_ticket.Models;
using Bus_ticket.ViewModels;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Bus_ticket.Services;

public class VehicleRevenueStatisticsService
{
    private readonly ApplicationDbContext _context;

    private static readonly string[] CompletedStatuses =
    {
        "completed"
    };

    private static readonly string[] PaidStatuses =
    {
        "paid",
        "completed",
        "success",
        "successful"
    };

    public VehicleRevenueStatisticsService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<VehicleRevenueStatisticsViewModel> GetVehicleRevenueStatisticsAsync(
        DateTime fromDate,
        DateTime toDate)
    {
        var from = fromDate.Date;
        var to = toDate.Date.AddDays(1).AddTicks(-1);

        var fromUtc = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(to, DateTimeKind.Utc);

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
                    "normalizedBookingStatus", new BsonDocument("$toLower", new BsonDocument("$ifNull", new BsonArray
                    {
                        "$bookingStatus",
                        string.Empty
                    }))
                },
                {
                    "normalizedPaymentStatus", new BsonDocument("$toLower", new BsonDocument("$ifNull", new BsonArray
                    {
                        "$paymentStatus",
                        string.Empty
                    }))
                },
                {
                    "ticketCount", new BsonDocument("$cond", new BsonArray
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
                    })
                },
                {
                    "revenueAmount", new BsonDocument("$ifNull", new BsonArray
                    {
                        "$finalAmount",
                        0
                    })
                }
            }),

            new BsonDocument("$match", new BsonDocument
            {
                {
                    "normalizedBookingStatus", new BsonDocument("$in", new BsonArray(CompletedStatuses))
                },
                {
                    "normalizedPaymentStatus", new BsonDocument("$in", new BsonArray(PaidStatuses))
                }
            }),

            new BsonDocument("$lookup", new BsonDocument
            {
                { "from", "trips" },
                { "localField", "tripId" },
                { "foreignField", "_id" },
                { "as", "trip" }
            }),
            new BsonDocument("$unwind", "$trip"),

            new BsonDocument("$lookup", new BsonDocument
            {
                { "from", "buses" },
                { "localField", "trip.busId" },
                { "foreignField", "_id" },
                { "as", "bus" }
            }),
            new BsonDocument("$unwind", "$bus"),

            new BsonDocument("$lookup", new BsonDocument
            {
                { "from", "busclasses" },
                { "localField", "bus.busClassId" },
                { "foreignField", "_id" },
                { "as", "busClass" }
            }),
            new BsonDocument("$unwind", new BsonDocument
            {
                { "path", "$busClass" },
                { "preserveNullAndEmptyArrays", true }
            }),

            new BsonDocument("$addFields", new BsonDocument
            {
                {
                    "displayBusType", new BsonDocument("$ifNull", new BsonArray
                    {
                        "$busClass.busType",
                        new BsonDocument("$ifNull", new BsonArray
                        {
                            "$bus.busType",
                            "Unknown"
                        })
                    })
                },
                {
                    "displayBusClassName", new BsonDocument("$ifNull", new BsonArray
                    {
                        "$busClass.className",
                        new BsonDocument("$ifNull", new BsonArray
                        {
                            "$bus.busType",
                            "Unknown"
                        })
                    })
                }
            }),

            new BsonDocument("$facet", new BsonDocument
            {
                {
                    "byBus", new BsonArray
                    {
                        new BsonDocument("$group", new BsonDocument
                        {
                            { "_id", "$bus._id" },
                            { "busCode", new BsonDocument("$first", "$bus.busCode") },
                            { "licensePlate", new BsonDocument("$first", "$bus.licensePlate") },
                            { "busType", new BsonDocument("$first", "$displayBusType") },
                            { "busClassName", new BsonDocument("$first", "$displayBusClassName") },
                            { "totalBookings", new BsonDocument("$sum", 1) },
                            { "totalTickets", new BsonDocument("$sum", "$ticketCount") },
                            { "totalRevenue", new BsonDocument("$sum", "$revenueAmount") }
                        }),
                        new BsonDocument("$sort", new BsonDocument("totalRevenue", -1))
                    }
                },
                {
                    "byBusType", new BsonArray
                    {
                        new BsonDocument("$group", new BsonDocument
                        {
                            { "_id", "$displayBusType" },
                            { "busClassName", new BsonDocument("$first", "$displayBusClassName") },
                            { "totalBookings", new BsonDocument("$sum", 1) },
                            { "totalTickets", new BsonDocument("$sum", "$ticketCount") },
                            { "totalRevenue", new BsonDocument("$sum", "$revenueAmount") }
                        }),
                        new BsonDocument("$sort", new BsonDocument("totalRevenue", -1))
                    }
                }
            })
        });

        var aggregationResult = await _context.Bookings
            .Aggregate(pipeline)
            .ToListAsync();

        var root = aggregationResult.FirstOrDefault();

        var busItems = new List<VehicleRevenueByBusItemViewModel>();
        var typeItems = new List<VehicleRevenueByTypeItemViewModel>();

        if (root != null)
        {
            foreach (var value in root.GetValue("byBus", new BsonArray()).AsBsonArray)
            {
                var item = value.AsBsonDocument;

                busItems.Add(new VehicleRevenueByBusItemViewModel
                {
                    BusId = GetBsonValueAsString(item.GetValue("_id", string.Empty)),
                    BusCode = item.GetValue("busCode", "Unknown").AsString,
                    LicensePlate = item.GetValue("licensePlate", "Unknown").AsString,
                    BusType = item.GetValue("busType", "Unknown").AsString,
                    BusClassName = item.GetValue("busClassName", "Unknown").AsString,
                    TotalBookings = item.GetValue("totalBookings", 0).ToInt32(),
                    TotalTickets = item.GetValue("totalTickets", 0).ToInt32(),
                    TotalRevenue = GetDecimalValue(item, "totalRevenue")
                });
            }

            foreach (var value in root.GetValue("byBusType", new BsonArray()).AsBsonArray)
            {
                var item = value.AsBsonDocument;

                typeItems.Add(new VehicleRevenueByTypeItemViewModel
                {
                    BusType = item.GetValue("_id", "Unknown").AsString,
                    BusClassName = item.GetValue("busClassName", "Unknown").AsString,
                    TotalBookings = item.GetValue("totalBookings", 0).ToInt32(),
                    TotalTickets = item.GetValue("totalTickets", 0).ToInt32(),
                    TotalRevenue = GetDecimalValue(item, "totalRevenue")
                });
            }
        }

        var grandTotalRevenue = busItems.Sum(x => x.TotalRevenue);
        var grandTotalBookings = busItems.Sum(x => x.TotalBookings);
        var grandTotalTickets = busItems.Sum(x => x.TotalTickets);

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
            FromDate = from,
            ToDate = toDate.Date,
            GrandTotalRevenue = grandTotalRevenue,
            GrandTotalBookings = grandTotalBookings,
            GrandTotalTickets = grandTotalTickets,
            BusRevenueItems = busItems,
            BusTypeRevenueItems = typeItems
        };
    }

    private static string GetBsonValueAsString(BsonValue value)
    {
        if (value == null || value.IsBsonNull)
        {
            return string.Empty;
        }

        return value.BsonType == BsonType.ObjectId
            ? value.AsObjectId.ToString()
            : value.ToString();
    }

    private static decimal GetDecimalValue(BsonDocument document, string fieldName)
    {
        if (!document.TryGetValue(fieldName, out var value) || value.IsBsonNull)
        {
            return 0m;
        }

        return value.BsonType switch
        {
            BsonType.Decimal128 => (decimal)value.AsDecimal128,
            BsonType.Double => Convert.ToDecimal(value.AsDouble),
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            _ => decimal.TryParse(value.ToString(), out var parsed) ? parsed : 0m
        };
    }
}