using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Bus_ticket.Models;

[BsonIgnoreExtraElements]
public class RealtimeSeat
{
    [BsonElement("seatNumber")] public string SeatNumber { get; set; }

    [BsonElement("status")] public string Status { get; set; } = "Available"; // Available, Holding, Booked

    [BsonElement("heldUntil")] public DateTime? HeldUntil { get; set; }

    [BsonElement("heldByCustomerId")] public string HeldByCustomerId { get; set; }
}
[BsonIgnoreExtraElements]
public class PriceConfig
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonElement("busType")]
    public string BusType { get; set; } // Lớp xe: Ghế ngồi, Giường nằm, Luxury

    [BsonElement("departurePoint")]
    public string DeparturePoint { get; set; } // Điểm đi (Ví dụ: Hà Nội)

    [BsonElement("destinationPoint")]
    public string DestinationPoint { get; set; } // Điểm đến (Ví dụ: Hải Phòng)

    [BsonElement("basePrice")]
    public decimal BasePrice { get; set; } // Giá vé cấu hình gốc

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public class Trip
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonElement("tripCode")]
    public string? TripCode { get; set; }

    [BsonElement("busId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string BusId { get; set; }

    [BsonElement("routeId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string RouteId { get; set; }
    
    [BsonElement("branchId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? BranchId { get; set; }

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