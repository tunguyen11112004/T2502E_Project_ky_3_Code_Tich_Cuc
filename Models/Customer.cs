using System;
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Bus_ticket.Models;

public class Customer
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }
    
    public int ConsecutiveUnpaidCount { get; set; } = 0;

    public bool IsBlocked { get; set; } = false;

    public string? BlockReason { get; set; }

    public DateTime? BlockedAt { get; set; }

    [BsonElement("customerCode")] public string CustomerCode { get; set; }

    [BsonElement("fullName")] [Required] public string FullName { get; set; }

    [BsonElement("dob")] public DateTime Dob { get; set; }

    [BsonElement("gender")] public string Gender { get; set; }

    [BsonElement("phoneNumber")]
    [Required]
    [Phone]
    public string PhoneNumber { get; set; }

    [BsonElement("email")] [EmailAddress] public string Email { get; set; }

    [BsonElement("membershipRank")] public string MembershipRank { get; set; } = "Standard";

    [BsonElement("totalPoints")] public int TotalPoints { get; set; } = 0;

    [BsonElement("customerNotes")] public string CustomerNotes { get; set; }

    [BsonElement("status")] public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; }
}