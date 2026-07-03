using Bus_ticket.Data;
using Bus_ticket.Models;
using Bus_ticket.ViewModels;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Bus_ticket.Services;

public class LowOccupancyTripsService
{
    private readonly ApplicationDbContext _context;

    public LowOccupancyTripsService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<LowOccupancyTripsViewModel> GetLowOccupancyTripsAsync(
        DateTime fromDate,
        DateTime toDate,
        double occupancyThreshold)
    {
        var from = fromDate.Date;
        var to = toDate.Date.AddDays(1).AddTicks(-1);

        var fromUtc = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(to, DateTimeKind.Utc);

        var pipeline = PipelineDefinition<Trip, BsonDocument>.Create(new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                {
                    "departureTime", new BsonDocument
                    {
                        { "$gte", fromUtc },
                        { "$lte", toUtc }
                    }
                }
            }),

            new BsonDocument("$addFields", new BsonDocument
            {
                {
                    "totalSeats", new BsonDocument("$size", new BsonDocument("$ifNull", new BsonArray
                    {
                        "$realtimeSeats",
                        new BsonArray()
                    }))
                },
                {
                    "bookedSeats", new BsonDocument("$size", new BsonDocument("$filter", new BsonDocument
                    {
                        {
                            "input", new BsonDocument("$ifNull", new BsonArray
                            {
                                "$realtimeSeats",
                                new BsonArray()
                            })
                        },
                        { "as", "seat" },
                        {
                            "cond", new BsonDocument("$eq", new BsonArray
                            {
                                new BsonDocument("$toLower", new BsonDocument("$ifNull", new BsonArray
                                {
                                    "$$seat.status",
                                    string.Empty
                                })),
                                "booked"
                            })
                        }
                    }))
                }
            }),

            new BsonDocument("$addFields", new BsonDocument
            {
                {
                    "occupancyRate", new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$gt", new BsonArray { "$totalSeats", 0 }),
                        new BsonDocument("$multiply", new BsonArray
                        {
                            new BsonDocument("$divide", new BsonArray
                            {
                                "$bookedSeats",
                                "$totalSeats"
                            }),
                            100
                        }),
                        0
                    })
                }
            }),

            new BsonDocument("$lookup", new BsonDocument
            {
                { "from", "buses" },
                { "localField", "busId" },
                { "foreignField", "_id" },
                { "as", "bus" }
            }),
            new BsonDocument("$unwind", new BsonDocument
            {
                { "path", "$bus" },
                { "preserveNullAndEmptyArrays", true }
            }),

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

            new BsonDocument("$lookup", new BsonDocument
            {
                { "from", "busroutes" },
                { "localField", "routeId" },
                { "foreignField", "_id" },
                { "as", "route" }
            }),
            new BsonDocument("$unwind", new BsonDocument
            {
                { "path", "$route" },
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
                    "routeName", new BsonDocument("$concat", new BsonArray
                    {
                        new BsonDocument("$ifNull", new BsonArray { "$route.departurePoint", "Unknown" }),
                        " - ",
                        new BsonDocument("$ifNull", new BsonArray { "$route.destinationPoint", "Unknown" })
                    })
                }
            }),

            new BsonDocument("$facet", new BsonDocument
            {
                {
                    "summary", new BsonArray
                    {
                        new BsonDocument("$group", new BsonDocument
                        {
                            { "_id", BsonNull.Value },
                            { "totalTrips", new BsonDocument("$sum", 1) },
                            {
                                "lowOccupancyTripCount", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray
                                {
                                    new BsonDocument("$and", new BsonArray
                                    {
                                        new BsonDocument("$gt", new BsonArray { "$totalSeats", 0 }),
                                        new BsonDocument("$lt", new BsonArray { "$occupancyRate", occupancyThreshold })
                                    }),
                                    1,
                                    0
                                }))
                            },
                            {
                                "soldOutTripCount", new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray
                                {
                                    new BsonDocument("$and", new BsonArray
                                    {
                                        new BsonDocument("$gt", new BsonArray { "$totalSeats", 0 }),
                                        new BsonDocument("$eq", new BsonArray { "$bookedSeats", "$totalSeats" })
                                    }),
                                    1,
                                    0
                                }))
                            },
                            { "averageOccupancyRate", new BsonDocument("$avg", "$occupancyRate") }
                        })
                    }
                },

                {
                    "lowOccupancyTrips", new BsonArray
                    {
                        new BsonDocument("$match", new BsonDocument("$expr", new BsonDocument("$and", new BsonArray
                        {
                            new BsonDocument("$gt", new BsonArray { "$totalSeats", 0 }),
                            new BsonDocument("$lt", new BsonArray { "$occupancyRate", occupancyThreshold })
                        }))),
                        new BsonDocument("$sort", new BsonDocument
                        {
                            { "occupancyRate", 1 },
                            { "departureTime", 1 }
                        }),
                        new BsonDocument("$project", new BsonDocument
                        {
                            { "_id", 1 },
                            { "tripCode", 1 },
                            { "departureTime", 1 },
                            { "totalSeats", 1 },
                            { "bookedSeats", 1 },
                            { "occupancyRate", 1 },
                            { "busCode", "$bus.busCode" },
                            { "licensePlate", "$bus.licensePlate" },
                            { "busType", "$displayBusType" },
                            { "routeName", 1 }
                        })
                    }
                },

                {
                    "soldOutTimeFrames", new BsonArray
                    {
                        new BsonDocument("$match", new BsonDocument("$expr", new BsonDocument("$and", new BsonArray
                        {
                            new BsonDocument("$gt", new BsonArray { "$totalSeats", 0 }),
                            new BsonDocument("$eq", new BsonArray { "$bookedSeats", "$totalSeats" })
                        }))),
                        new BsonDocument("$group", new BsonDocument
                        {
                            {
                                "_id", new BsonDocument("$dateToString", new BsonDocument
                                {
                                    { "format", "%H:00" },
                                    { "date", "$departureTime" }
                                })
                            },
                            { "soldOutTripCount", new BsonDocument("$sum", 1) },
                            { "totalSeats", new BsonDocument("$sum", "$totalSeats") },
                            { "bookedSeats", new BsonDocument("$sum", "$bookedSeats") }
                        }),
                        new BsonDocument("$sort", new BsonDocument("_id", 1))
                    }
                }
            })
        });

        var aggregationResult = await _context.Trips
            .Aggregate(pipeline)
            .ToListAsync();

        var root = aggregationResult.FirstOrDefault();

        var model = new LowOccupancyTripsViewModel
        {
            FromDate = from,
            ToDate = toDate.Date,
            OccupancyThreshold = occupancyThreshold
        };

        if (root == null)
        {
            return model;
        }

        var summaryArray = root.GetValue("summary", new BsonArray()).AsBsonArray;

        if (summaryArray.Any())
        {
            var summary = summaryArray.First().AsBsonDocument;

            model.TotalTrips = summary.GetValue("totalTrips", 0).ToInt32();
            model.LowOccupancyTripCount = summary.GetValue("lowOccupancyTripCount", 0).ToInt32();
            model.SoldOutTripCount = summary.GetValue("soldOutTripCount", 0).ToInt32();
            model.AverageOccupancyRate = Math.Round(GetDoubleValue(summary, "averageOccupancyRate"), 2);
        }

        foreach (var value in root.GetValue("lowOccupancyTrips", new BsonArray()).AsBsonArray)
        {
            var item = value.AsBsonDocument;

            model.LowOccupancyTrips.Add(new LowOccupancyTripItemViewModel
            {
                TripId = GetBsonValueAsString(item.GetValue("_id", string.Empty)),
                TripCode = item.GetValue("tripCode", "Unknown").ToString(),
                BusCode = item.GetValue("busCode", "Unknown").ToString(),
                LicensePlate = item.GetValue("licensePlate", "Unknown").ToString(),
                BusType = item.GetValue("busType", "Unknown").ToString(),
                RouteName = item.GetValue("routeName", "Unknown").ToString(),
                DepartureTime = item.GetValue("departureTime", DateTime.MinValue).ToUniversalTime(),
                TotalSeats = item.GetValue("totalSeats", 0).ToInt32(),
                BookedSeats = item.GetValue("bookedSeats", 0).ToInt32(),
                OccupancyRate = Math.Round(GetDoubleValue(item, "occupancyRate"), 2)
            });
        }

        foreach (var value in root.GetValue("soldOutTimeFrames", new BsonArray()).AsBsonArray)
        {
            var item = value.AsBsonDocument;

            model.SoldOutTimeFrames.Add(new SoldOutTimeFrameItemViewModel
            {
                TimeFrame = item.GetValue("_id", string.Empty).ToString(),
                SoldOutTripCount = item.GetValue("soldOutTripCount", 0).ToInt32(),
                TotalSeats = item.GetValue("totalSeats", 0).ToInt32(),
                BookedSeats = item.GetValue("bookedSeats", 0).ToInt32()
            });
        }

        return model;
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

    private static double GetDoubleValue(BsonDocument document, string fieldName)
    {
        if (!document.TryGetValue(fieldName, out var value) || value.IsBsonNull)
        {
            return 0;
        }

        return value.BsonType switch
        {
            BsonType.Double => value.AsDouble,
            BsonType.Decimal128 => Convert.ToDouble(value.AsDecimal128),
            BsonType.Int32 => value.AsInt32,
            BsonType.Int64 => value.AsInt64,
            _ => double.TryParse(value.ToString(), out var parsed) ? parsed : 0
        };
    }
}