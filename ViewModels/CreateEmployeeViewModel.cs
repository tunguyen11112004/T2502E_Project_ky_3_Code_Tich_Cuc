using System.ComponentModel.DataAnnotations;

namespace Bus_ticket.ViewModels;

public class CreateEmployeeViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Initial password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Age is required.")]
    [Range(18, 100, ErrorMessage = "Age must be between 18 and 100.")]
    public int Age { get; set; }

    [Phone(ErrorMessage = "Invalid phone number.")]
    public string PhoneNumber { get; set; } = string.Empty;

    public string Qualifications { get; set; } = string.Empty;
}