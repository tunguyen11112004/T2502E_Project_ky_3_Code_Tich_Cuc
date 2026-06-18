using Bus_ticket.Services;
using Bus_ticket.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Bus_ticket.Controllers;

[Authorize(Roles = "Admin")]
[Route("Admin/Employees")]
public class AdminEmployeesController : Controller
{
    private readonly UserService _userService;

    public AdminEmployeesController(UserService userService)
    {
        _userService = userService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var employees = await _userService.GetEmployeesAsync();

        return View("~/Views/Admin/Employees/Index.cshtml", employees);
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        return View("~/Views/Admin/Employees/Create.cshtml", new CreateEmployeeViewModel());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateEmployeeViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("~/Views/Admin/Employees/Create.cshtml", model);
        }

        var createdBy = User.FindFirstValue(ClaimTypes.Email)
                        ?? User.Identity?.Name
                        ?? "System Admin";

        try
        {
            await _userService.CreateEmployeeAsync(model, createdBy);

            TempData["SuccessMessage"] = "Employee account created successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(model.Email), ex.Message);

            return View("~/Views/Admin/Employees/Create.cshtml", model);
        }
    }

    // API-style endpoint for AJAX/modal later.
    // Route: POST /Admin/Employees/CreateApi
    // Return: 201 Created or 400 Bad Request.
    [HttpPost("CreateApi")]
    public async Task<IActionResult> CreateApi([FromBody] CreateEmployeeViewModel model)
    {
        if (!User.Identity?.IsAuthenticated == true || !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        if (!TryValidateModel(model))
        {
            return BadRequest(new
            {
                message = "Invalid employee data.",
                errors = ModelState
            });
        }

        var createdBy = User.FindFirstValue(ClaimTypes.Email)
                        ?? User.Identity?.Name
                        ?? "System Admin";

        try
        {
            var employee = await _userService.CreateEmployeeAsync(model, createdBy);

            return StatusCode(StatusCodes.Status201Created, new
            {
                message = "Employee account created successfully.",
                employeeId = employee.Id,
                employeeCode = employee.EmployeeCode,
                role = employee.Role
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }
}