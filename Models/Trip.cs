using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Bus_ticket.Models;

public class RealtimeSeat
{
    [BsonElement("seatNumber")] public string SeatNumber { get; set; }

    [BsonElement("status")] public string Status { get; set; } = "Available"; // Available, Holding, Booked

    [BsonElement("heldUntil")] public DateTime? HeldUntil { get; set; }

    [BsonElement("heldByCustomerId")] public string HeldByCustomerId { get; set; }
}

public class Trip
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonElement("busId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string BusId { get; set; }

    [BsonElement("routeId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string RouteId { get; set; }

    [BsonElement("baseFare")] public decimal BaseFare { get; set; }

    [BsonElement("departureTime")] public DateTime DepartureTime { get; set; }

    [BsonElement("arrivalTime")] public DateTime ArrivalTime { get; set; }

    [BsonElement("status")] public string Status { get; set; } = "Scheduled";

    [BsonElement("realtimeSeats")] public List<RealtimeSeat> RealtimeSeats { get; set; } = new List<RealtimeSeat>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; }
}