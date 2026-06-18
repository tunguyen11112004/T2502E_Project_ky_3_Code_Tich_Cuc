using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Bus_ticket.Models
{
    public class DynamicRole
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("name")]
        [Required(ErrorMessage = "Vui lòng nhập tên vai trò")]
        public string Name { get; set; } = null!;

        [BsonElement("description")]
        [Required(ErrorMessage = "Vui lòng nhập mô tả vai trò")]
        public string Description { get; set; } = null!;

        // Lưu danh sách ID (string) của các Permission được chọn
        [BsonElement("permissionIds")]
        public List<string> PermissionIds { get; set; } = new List<string>();
    }
}