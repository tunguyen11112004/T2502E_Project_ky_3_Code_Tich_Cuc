using System;
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Bus_ticket.Models;

[BsonIgnoreExtraElements]
public class Branch
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("branchCode")]
    [Required(ErrorMessage = "Mã chi nhánh là bắt buộc.")]
    [StringLength(20, ErrorMessage = "Mã chi nhánh không được vượt quá 20 ký tự.")]
    [RegularExpression(@"^[A-Z0-9-]+$", ErrorMessage = "Mã chi nhánh chỉ nên chứa chữ in hoa, số và dấu gạch ngang.")]
    public string BranchCode { get; set; } = string.Empty;

    [BsonElement("branchName")]
    [Required(ErrorMessage = "Tên chi nhánh là bắt buộc.")]
    [StringLength(120, ErrorMessage = "Tên chi nhánh không được vượt quá 120 ký tự.")]
    public string BranchName { get; set; } = string.Empty;

    [BsonElement("address")]
    [Required(ErrorMessage = "Địa chỉ chi nhánh là bắt buộc.")]
    [StringLength(255, ErrorMessage = "Địa chỉ không được vượt quá 255 ký tự.")]
    public string Address { get; set; } = string.Empty;

    [BsonElement("phoneNumber")]
    [Required(ErrorMessage = "Số điện thoại chi nhánh là bắt buộc.")]
    [Phone(ErrorMessage = "Số điện thoại không đúng định dạng.")]
    [StringLength(20, ErrorMessage = "Số điện thoại không được vượt quá 20 ký tự.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [BsonElement("status")]
    [Required(ErrorMessage = "Trạng thái là bắt buộc.")]
    [RegularExpression("Active|Inactive|Maintenance", ErrorMessage = "Trạng thái chỉ được là Active, Inactive hoặc Maintenance.")]
    public string Status { get; set; } = "Active";

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("createdBy")]
    public string CreatedBy { get; set; } = "System";

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedBy")]
    public string UpdatedBy { get; set; } = "System";
}
