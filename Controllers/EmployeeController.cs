using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bus_ticket.Controllers;

[Authorize(Roles = "Admin,Employee")]
public class EmployeeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}