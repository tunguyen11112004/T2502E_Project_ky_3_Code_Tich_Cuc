using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Bus_ticket.Models;

public class Seat
{
    [BsonElement("seatNumber")] public string SeatNumber { get; set; }

    [BsonElement("row")] public int Row { get; set; }

    [BsonElement("column")] public int Column { get; set; }

    [BsonElement("floor")] public int Floor { get; set; } // 1: Tầng dưới, 2: Tầng trên

    [BsonElement("seatType")] public string SeatType { get; set; } = "Standard"; // Standard / VIP
}

public class Bus
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonElement("busCode")] [Required] public string BusCode { get; set; }

    [BsonElement("licensePlate")]
    [Required]
    public string LicensePlate { get; set; }

    [BsonElement("busType")] public string BusType { get; set; } // Express_Seat, Luxury_Sleeper

    [BsonElement("totalSeats")] public int TotalSeats { get; set; }

    [BsonElement("totalRows")] public int TotalRows { get; set; }

    [BsonElement("totalColumns")] public int TotalColumns { get; set; }

    [BsonElement("totalFloors")] public int TotalFloors { get; set; } = 1;

    [BsonElement("status")] public string Status { get; set; } = "Active";

    [BsonElement("branchId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string BranchId { get; set; }

    [BsonElement("seatsLayout")] public List<Seat> SeatsLayout { get; set; } = new List<Seat>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; }
}