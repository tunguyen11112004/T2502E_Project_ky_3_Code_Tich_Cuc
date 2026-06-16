using System;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace Bus_ticket.Models;

public class AgeDiscountRule
{
    [BsonElement("minAge")] public int MinAge { get; set; }

    [BsonElement("maxAge")] public int MaxAge { get; set; }

    [BsonElement("discountPercentage")] public decimal DiscountPercentage { get; set; }
}

public class CancellationPolicy
{
    [BsonElement("hoursBeforeDeparture")] public int HoursBeforeDeparture { get; set; }

    [BsonElement("penaltyPercentage")] public decimal PenaltyPercentage { get; set; }
}

public class SystemConfig
{
    [BsonId] public string Id { get; set; } = "global_system_configuration";

    [BsonElement("ageDiscountRules")]
    public List<AgeDiscountRule> AgeDiscountRules { get; set; } = new List<AgeDiscountRule>();

    [BsonElement("cancellationPolicies")]
    public List<CancellationPolicy> CancellationPolicies { get; set; } = new List<CancellationPolicy>();

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; }
}