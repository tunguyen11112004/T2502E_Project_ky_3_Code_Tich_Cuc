using System;
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Bus_ticket.Models;

[BsonIgnoreExtraElements]
public class Bus
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; }

    [BsonElement("busCode")] 
    [Required] 
    public string BusCode { get; set; } // Ví dụ: BUS-001, BUS-002

    [BsonElement("licensePlate")]
    [Required]
    public string LicensePlate { get; set; } // Ví dụ: 29B-123.45

    [BsonElement("status")] 
    public string Status { get; set; } = "Active"; // Active, Maintenance, Inactive

    // Liên kết với bảng chi nhánh quản lý xe này
    [BsonElement("branchId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? BranchId { get; set; }
    
    // Nhà xe/đối tác sở hữu xe này
    [BsonElement("operatorId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? OperatorId { get; set; }

    [BsonElement("busClassId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? BusClassId { get; set; }

  // Legacy schema (dữ liệu seed cũ)
    [BsonElement("busType")]
    public string? LegacyBusType { get; set; }

    [BsonElement("seatsLayout")]
    public List<SeatTemplate>? SeatsLayout { get; set; }

    [BsonElement("totalSeats")]
    public int? LegacyTotalSeats { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("createdBy")]
    public string CreatedBy { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedBy")]
    public string UpdatedBy { get; set; }
    
}