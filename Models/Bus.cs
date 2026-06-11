using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Bus_ticket.Models;

public enum BusType
{
    None = 0,
    Express = 1,
    Luxury = 2,
    VolvoNonAC = 3,
    VolvoAC = 4
}

public enum BusStatus
{
    Cancelled = 0,
    Active = 1
}

public class Bus
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [Required(ErrorMessage = "Mã xe không được để trống")]
    [RegularExpression(@"^\d{5}$", ErrorMessage = "Mã xe phải gồm đúng 5 chữ số")]
    public string BusCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số xe không được để trống")]
    [StringLength(30, ErrorMessage = "Số xe không được vượt quá 30 ký tự")]
    [Display(Name = "Số xe")]
    public string BusNumber { get; set; } = string.Empty;

    [Range(1, 4, ErrorMessage = "Vui lòng chọn loại xe")]
    [Display(Name = "Loại xe")]
    public BusType BusType { get; set; } = BusType.None;

    [Required(ErrorMessage = "Tuyến đường không được để trống")]
    [StringLength(150)]
    [Display(Name = "Tuyến đường")]
    public string Route { get; set; } = string.Empty;

    [Range(1, 5000, ErrorMessage = "Quãng đường phải lớn hơn 0")]
    [Display(Name = "Quãng đường")]
    public decimal Distance { get; set; }

    [Required(ErrorMessage = "Ngày giờ xuất phát không được để trống")]
    [Display(Name = "Ngày giờ xuất phát")]
    public DateTime DepartureTime { get; set; } = DateTime.Now.AddHours(1);

    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public BusStatus Status { get; set; } = BusStatus.Active;

    [BsonIgnore]
    [Display(Name = "Ảnh xe khách")]
    public IFormFile? ImageFile { get; set; }
}