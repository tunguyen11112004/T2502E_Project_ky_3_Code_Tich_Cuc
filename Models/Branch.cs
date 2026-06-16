using System;
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Bus_ticket.Models;

public class Branch
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonElement("branchCode")] [Required] public string BranchCode { get; set; }

    [BsonElement("branchName")] [Required] public string BranchName { get; set; }

    [BsonElement("address")] public string Address { get; set; }

    [BsonElement("phoneNumber")] [Phone] public string PhoneNumber { get; set; }

    [BsonElement("status")] public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; }
}