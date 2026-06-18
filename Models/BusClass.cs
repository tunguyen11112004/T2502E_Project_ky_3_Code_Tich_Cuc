using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Bus_ticket.Models;

public class SeatTemplate
{
    [BsonElement("seatNumber")] 
    public string SeatNumber { get; set; }

    [BsonElement("row")] 
    public int Row { get; set; }

    [BsonElement("column")] 
    public int Column { get; set; }

    [BsonElement("floor")] 
    public int Floor { get; set; } // 1: Tầng dưới, 2: Tầng trên

    [BsonElement("seatType")] 
    public string SeatType { get; set; } = "Standard"; // Standard / VIP / Sleeper
}

public class BusClass
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonElement("className")] 
    [Required] 
    public string ClassName { get; set; } // Ví dụ: Luxury Limousine 22, Express Seat 45

    [BsonElement("busType")] 
    [Required]
    public string BusType { get; set; } // Express_Seat, Luxury_Sleeper, Limousine_Sleeper

    [BsonElement("totalSeats")] 
    public int TotalSeats { get; set; }

    [BsonElement("totalRows")] 
    public int TotalRows { get; set; }

    [BsonElement("totalColumns")] 
    public int TotalColumns { get; set; }

    [BsonElement("totalFloors")] 
    public int TotalFloors { get; set; } = 1;

    [BsonElement("defaultLayout")] 
    public List<SeatTemplate> DefaultLayout { get; set; } = new List<SeatTemplate>();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("createdBy")]
    public string CreatedBy { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedBy")]
    public string UpdatedBy { get; set; }
}