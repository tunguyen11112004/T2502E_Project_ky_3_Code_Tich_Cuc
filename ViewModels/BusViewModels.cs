using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Bus_ticket.ViewModels;

public class BusListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string BusCode { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string BusClassName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public int TotalSeats { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class BusFormViewModel
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Mã xe không được để trống.")]
    [StringLength(30)]
    public string BusCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Biển số xe không được để trống.")]
    [StringLength(20)]
    public string LicensePlate { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn loại xe.")]
    public string BusClassId { get; set; } = string.Empty;

    public string? BranchId { get; set; }
    public string? OperatorId { get; set; }

    [Required]
    public string Status { get; set; } = "Active";

    public List<SelectListItem> BusClassOptions { get; set; } = new();
    public List<SelectListItem> BranchOptions { get; set; } = new();
    public List<SelectListItem> OperatorOptions { get; set; } = new();
}