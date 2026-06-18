using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Bus_ticket.Models;

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("userCode")]
    public string UserCode { get; set; } = string.Empty;

    [BsonElement("employeeCode")]
    public string EmployeeCode { get; set; } = string.Empty;

    [BsonElement("fullName")]
    [Required]
    public string FullName { get; set; } = string.Empty;

    [BsonElement("dob")]
    public DateTime? Dob { get; set; }

    [BsonElement("email")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [BsonElement("phoneNumber")]
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;

    [BsonElement("address")]
    public string Address { get; set; } = string.Empty;

    [BsonElement("educationLevel")]
    public string EducationLevel { get; set; } = string.Empty;

    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;

    [BsonElement("passwordHash")]
    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("role")]
    public string Role { get; set; } = string.Empty; // Admin, Employee

    [BsonElement("status")]
    public string Status { get; set; } = "Active";

    [BsonElement("roleId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? RoleId { get; set; }

    [BsonElement("branchId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? BranchId { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedBy")]
    public string UpdatedBy { get; set; } = string.Empty;
}