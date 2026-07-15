using System.ComponentModel.DataAnnotations;

namespace Bus_ticket.ViewModels;

public class BusClassListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int TotalColumns { get; set; }
    public int TotalFloors { get; set; }
    public int TotalSeats { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class BusClassFormViewModel
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Tên loại xe không được để trống.")]
    [StringLength(120, ErrorMessage = "Tên loại xe tối đa 120 ký tự.")]
    [Display(Name = "Tên loại xe")]
    public string ClassName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số hàng ghế không được để trống.")]
    [Range(1, 20, ErrorMessage = "Số hàng phải từ 1 đến 20.")]
    [Display(Name = "Số hàng ghế (dọc)")]
    public int TotalRows { get; set; } = 10;

    [Required(ErrorMessage = "Số cột ghế không được để trống.")]
    [Range(1, 10, ErrorMessage = "Số cột phải từ 1 đến 10.")]
    [Display(Name = "Số cột ghế (ngang)")]
    public int TotalColumns { get; set; } = 4;

    [Required(ErrorMessage = "Số tầng không được để trống.")]
    [Range(1, 2, ErrorMessage = "Số tầng phải từ 1 đến 2.")]
    [Display(Name = "Số tầng")]
    public int TotalFloors { get; set; } = 1;

    [Required(ErrorMessage = "Trạng thái không được để trống.")]
    [RegularExpression("^(Active|Inactive)$", ErrorMessage = "Trạng thái không hợp lệ.")]
    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = "Active";

    public string ImageUrl { get; set; } = string.Empty;
    public string ImagePublicId { get; set; } = string.Empty;

    [Display(Name = "Ảnh loại xe")]
    public IFormFile? ImageFile { get; set; }
}
