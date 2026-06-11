using Microsoft.AspNetCore.Identity;

namespace Bus_ticket.Models;

public class ApplicationUser : IdentityUser
{
    public string EmployeeCode { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public int Age { get; set; }

    public string Education { get; set; } = string.Empty;
}