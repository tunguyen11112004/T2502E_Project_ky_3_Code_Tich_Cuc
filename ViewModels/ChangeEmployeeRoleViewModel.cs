using System.ComponentModel.DataAnnotations;

namespace Bus_ticket.ViewModels;

public class ChangeEmployeeRoleViewModel
{
    [Required]
    public string EmployeeId { get; set; } = string.Empty;

    public string EmployeeCode { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string CurrentRoleId { get; set; } = string.Empty;

    public string CurrentRoleName { get; set; } = "No role assigned";

    [Required(ErrorMessage = "Please select a dynamic role.")]
    public string NewRoleId { get; set; } = string.Empty;
}
