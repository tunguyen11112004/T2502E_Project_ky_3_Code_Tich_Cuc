using System;
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Bus_ticket.Models;

[BsonIgnoreExtraElements]
public class BusOperator
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("operatorCode")]
    [Required(ErrorMessage = "Operator code is required.")]
    [StringLength(30)]
    [RegularExpression(@"^OP-[A-Z]{2}-\d{2}$", ErrorMessage = "Operator code must follow format OP-HL-03.")]
    [Display(Name = "Operator Code")]
    public string OperatorCode { get; set; } = string.Empty;

    [BsonElement("operatorName")]
    [Required(ErrorMessage = "Operator name is required.")]
    [StringLength(150)]
    [Display(Name = "Operator Name")]
    public string OperatorName { get; set; } = string.Empty;

    [BsonElement("phoneNumber")]
    [Required(ErrorMessage = "Phone number is required.")]
    [Phone]
    [StringLength(20)]
    [Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [BsonElement("email")]
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress]
    [StringLength(120)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("address")]
    [Required(ErrorMessage = "Address is required.")]
    [StringLength(255)]
    [Display(Name = "Address")]
    public string Address { get; set; } = string.Empty;

    [BsonElement("contactPerson")]
    [Required(ErrorMessage = "Contact person is required.")]
    [StringLength(120)]
    [Display(Name = "Contact Person")]
    public string ContactPerson { get; set; } = string.Empty;

    [BsonElement("status")]
    [Required]
    [RegularExpression("Active|Inactive")]
    [Display(Name = "Status")]
    public string Status { get; set; } = "Active";

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("createdBy")]
    public string CreatedBy { get; set; } = "SystemSeeder";
}