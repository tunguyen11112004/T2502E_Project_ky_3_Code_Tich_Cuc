using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Bus_ticket.Models;

public class Station
{
    [BsonElement("stationName")] public string StationName { get; set; }

    [BsonElement("stopOrder")] public int StopOrder { get; set; }
}

public class FareConfig
{
    [BsonElement("busType")] public string BusType { get; set; }

    [BsonElement("flatPrice")] public decimal FlatPrice { get; set; }

    [BsonElement("vatPercentage")] public decimal VatPercentage { get; set; }
}

public class BusRoute
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonElement("departurePoint")]
    [Required]
    public string DeparturePoint { get; set; }

    [BsonElement("destinationPoint")]
    [Required]
    public string DestinationPoint { get; set; }

    [BsonElement("distanceKm")] public int DistanceKm { get; set; }

    [BsonElement("stations")] public List<Station> Stations { get; set; } = new List<Station>();

    [BsonElement("fareConfigs")] public List<FareConfig> FareConfigs { get; set; } = new List<FareConfig>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; }
}