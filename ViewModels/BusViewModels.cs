using System.ComponentModel.DataAnnotations;

namespace Bus_ticket.ViewModels;

public class BusListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string BusCode { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string? BranchId { get; set; }
    public string BranchName { get; set; } = "—";
    public string? BusClassId { get; set; }
    public string BusClassName { get; set; } = "—";
    public string BusType { get; set; } = "—";
    public int TotalSeats { get; set; }
    public int SeatMatrixCount { get; set; }
    public int TripCount { get; set; }
    public int BookingCount { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BusOptionViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public class BusFormViewModel
{
    public string? Id { get; set; }

    [Display(Name = "Mã xe")]
    [StringLength(30, ErrorMessage = "Mã xe không được vượt quá 30 ký tự.")]
    public string? BusCode { get; set; }

    [Required(ErrorMessage = "Biển số xe không được để trống.")]
    [Display(Name = "Biển số xe")]
    [StringLength(20, ErrorMessage = "Biển số xe không được vượt quá 20 ký tự.")]
    public string LicensePlate { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn loại/hạng xe.")]
    [Display(Name = "Loại xe")]
    public string BusClassId { get; set; } = string.Empty;

    [Display(Name = "Chi nhánh quản lý")]
    public string? BranchId { get; set; }

    [Required(ErrorMessage = "Trạng thái xe không được để trống.")]
    [Display(Name = "Trạng thái")]
    public string Status { get; set; } = "Active";
}

public class BusIndexViewModel
{
    public List<BusListItemViewModel> Items { get; set; } = new();
    public BusFormViewModel CreateForm { get; set; } = new();
    public List<BusOptionViewModel> BusClasses { get; set; } = new();
    public List<BusOptionViewModel> Branches { get; set; } = new();
    public string SearchTerm { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string BusClassId { get; set; } = string.Empty;
    public string BranchId { get; set; } = string.Empty;
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalPages { get; set; }
    public long TotalItems { get; set; }
}
