using System.ComponentModel.DataAnnotations;

namespace Bus_ticket.ViewModels;

public class BusClassListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string BusType { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int TotalColumns { get; set; }
    public int TotalSeats { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> LicensePlates { get; set; } = new();
}

public class BusSummaryViewModel
{
    public string Id { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class BusClassFormViewModel
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Tên hạng xe không được để trống.")]
    [Display(Name = "Tên hạng xe")]
    public string ClassName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Loại xe không được để trống.")]
    [Display(Name = "Loại xe")]
    public string BusType { get; set; } = "Express_Seat";

    [Range(1, 20, ErrorMessage = "Số hàng phải từ 1 đến 20.")]
    [Display(Name = "Số hàng ghế (dọc)")]
    public int TotalRows { get; set; } = 10;

    [Range(1, 10, ErrorMessage = "Số cột phải từ 1 đến 10.")]
    [Display(Name = "Số cột ghế (ngang)")]
    public int TotalColumns { get; set; } = 4;

    [Range(1, 2, ErrorMessage = "Số tầng phải từ 1 đến 2.")]
    [Display(Name = "Số tầng")]
    public int TotalFloors { get; set; } = 1;

    [Display(Name = "Trạng thái hoạt động")]
    public string Status { get; set; } = "Active";

    [Display(Name = "Biển số xe (tùy chọn)")]
    public string? LicensePlate { get; set; }

    public string ImageUrl { get; set; } = string.Empty;
    public string ImagePublicId { get; set; } = string.Empty;

    [Display(Name = "Ảnh xe")]
    public IFormFile? ImageFile { get; set; }

    public List<BusSummaryViewModel> LinkedBuses { get; set; } = new();
}
