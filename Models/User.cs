using System;
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Bus_ticket.Models;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonElement("userCode")] [Required] public string UserCode { get; set; }

    [BsonElement("fullName")] [Required] public string FullName { get; set; }

    [BsonElement("dob")] public DateTime Dob { get; set; }

    [BsonElement("email")] [EmailAddress] public string Email { get; set; }

    [BsonElement("phoneNumber")] [Phone] public string PhoneNumber { get; set; }

    [BsonElement("address")] public string Address { get; set; }

    [BsonElement("educationLevel")] public string EducationLevel { get; set; }

    [BsonElement("username")] [Required] public string Username { get; set; }

    [BsonElement("password")] [Required] public string Password { get; set; }

    [BsonElement("status")] public string Status { get; set; } = "Active";

    [BsonElement("roleId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string RoleId { get; set; }

    [BsonElement("branchId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string BranchId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; }
}