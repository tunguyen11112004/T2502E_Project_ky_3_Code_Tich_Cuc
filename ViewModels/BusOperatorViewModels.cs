using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Bus_ticket.ViewModels;

public class BusOperatorListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string OperatorCode { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string TaxCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class BusOperatorFormViewModel
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Mã nhà xe không được để trống.")]
    public string OperatorCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên nhà xe không được để trống.")]
    public string OperatorName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email liên hệ không được để trống.")]
    [EmailAddress]
    public string ContactEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số điện thoại không được để trống.")]
    public string PhoneNumber { get; set; } = string.Empty;

    public string? TaxCode { get; set; }

    [Required]
    public string Status { get; set; } = "Active";
}