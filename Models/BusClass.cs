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
    public int Floor { get; set; }

    [BsonElement("seatType")]
    public string SeatType { get; set; } = "Standard";
}

[BsonIgnoreExtraElements]
public class BusClass
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonElement("className")]
    [Required]
    public string ClassName { get; set; }

    [BsonElement("classNameKey")]
    public string? ClassNameKey { get; set; }

    [BsonElement("busType")]
    [Required]
    public string BusType { get; set; }

    [BsonElement("imageUrl")]
    public string ImageUrl { get; set; } = string.Empty;

    [BsonElement("imagePublicId")]
    public string ImagePublicId { get; set; } = string.Empty;
  
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

    [BsonElement("status")]
    public string Status { get; set; } = "Active";

    [BsonElement("deletedAt")]
    public DateTime? DeletedAt { get; set; }

    [BsonElement("deletedBy")]
    public string? DeletedBy { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("createdBy")]
    public string CreatedBy { get; set; }

    [BsonElement("updatedAt")]
    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedBy")]
    public string UpdatedBy { get; set; }
}
