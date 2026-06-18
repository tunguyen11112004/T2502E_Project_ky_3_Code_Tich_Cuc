using System;
using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Bus_ticket.Models;

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
    [Required]
    public string BranchId { get; set; }

    // THAY ĐỔI QUAN TRỌNG: Liên kết tham chiếu đến Hạng xe / Sơ đồ xe tương ứng
    [BsonElement("busClassId")]
    [BsonRepresentation(BsonType.ObjectId)]
    [Required]
    public string BusClassId { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("createdBy")]
    public string CreatedBy { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedBy")]
    public string UpdatedBy { get; set; }
}