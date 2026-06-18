using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Bus_ticket.Models;

public class Permission
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; } // Thêm dấu hỏi (?) để cho phép null khi Tạo mới

    [BsonElement("name")]
    [Required(ErrorMessage = "Vui lòng nhập tên quyền")]
    public string Name { get; set; } = null!;

    [BsonElement("description")]
    [Required(ErrorMessage = "Vui lòng nhập mô tả")]
    public string Description { get; set; } = null!;

    [BsonElement("link")]
    [Required(ErrorMessage = "Vui lòng nhập đường dẫn")]
    public string Link { get; set; } = null!;

    [BsonElement("method")]
    [Required(ErrorMessage = "Vui lòng chọn phương thức")]
    public string Method { get; set; } = null!;
}