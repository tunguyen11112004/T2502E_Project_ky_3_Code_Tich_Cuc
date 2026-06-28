using Bus_ticket.Data;
using Bus_ticket.Services;
using Bus_ticket.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Security.Claims;

namespace Bus_ticket.Controllers;

[Authorize(Roles = "Admin,Employee")]
[Route("Admin/Users")]
public class UserController : Controller
{
    private readonly UserService _userService;
    private readonly ApplicationDbContext _context;

    public UserController(UserService userService, ApplicationDbContext context)
    {
        _userService = userService;
        _context = context;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var employees = await _userService.GetEmployeesAsync();

        var dynamicRoles = await _context.DynamicRoles
            .Find(_ => true)
            .ToListAsync();

        ViewBag.DynamicRoleNames = dynamicRoles
            .Where(role => !string.IsNullOrWhiteSpace(role.Id))
            .ToDictionary(
                role => role.Id!,
                role => role.RoleName ?? "Unnamed role"
            );

        return View("~/Views/Admin/Users/Index.cshtml", employees);
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        await LoadDynamicRolesAsync();

        return View("~/Views/Admin/Users/Create.cshtml", new CreateEmployeeViewModel());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateEmployeeViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await LoadDynamicRolesAsync();
            return View("~/Views/Admin/Users/Create.cshtml", model);
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
            ModelState.AddModelError(string.Empty, ex.Message);

            await LoadDynamicRolesAsync();
            return View("~/Views/Admin/Users/Create.cshtml", model);
        }
    }

    [HttpGet("ChangeRole/{id}")]
    public async Task<IActionResult> ChangeRole(string id)
    {
        var employee = await _userService.GetByIdAsync(id);

        if (employee == null || employee.Role != "Employee")
        {
            TempData["ErrorMessage"] = "Employee không tồn tại trong hệ thống.";
            return RedirectToAction(nameof(Index));
        }

        var currentRole = string.IsNullOrWhiteSpace(employee.RoleId)
            ? null
            : await _context.DynamicRoles
                .Find(role => role.Id == employee.RoleId)
                .FirstOrDefaultAsync();

        var model = new ChangeEmployeeRoleViewModel
        {
            EmployeeId = employee.Id ?? string.Empty,
            EmployeeCode = employee.EmployeeCode,
            FullName = employee.FullName,
            Email = employee.Email,
            CurrentRoleId = employee.RoleId ?? string.Empty,
            CurrentRoleName = currentRole?.RoleName ?? "No role assigned",
            NewRoleId = employee.RoleId ?? string.Empty
        };

        await LoadDynamicRolesAsync();

        return View("~/Views/Admin/Users/ChangeRole.cshtml", model);
    }

    [HttpPost("ChangeRole")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(ChangeEmployeeRoleViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await LoadDynamicRolesAsync();
            return View("~/Views/Admin/Users/ChangeRole.cshtml", model);
        }

        var updatedBy = User.FindFirstValue(ClaimTypes.Email)
                        ?? User.Identity?.Name
                        ?? "System Admin";

        try
        {
            await _userService.UpdateEmployeeRoleAsync(
                model.EmployeeId,
                model.NewRoleId,
                updatedBy
            );

            TempData["SuccessMessage"] = "Employee role updated successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);

            await LoadDynamicRolesAsync();
            return View("~/Views/Admin/Users/ChangeRole.cshtml", model);
        }
    }

    [HttpPost("Delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var updatedBy = User.FindFirstValue(ClaimTypes.Email)
                        ?? User.Identity?.Name
                        ?? "System Admin";

        try
        {
            await _userService.DeactivateEmployeeAsync(id, updatedBy);

            TempData["SuccessMessage"] = "Employee deactivated successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("CreateApi")]
    public async Task<IActionResult> CreateApi([FromBody] CreateEmployeeViewModel model)
    {
        if (User.Identity?.IsAuthenticated != true || !User.IsInRole("Admin"))
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
                role = employee.Role,
                roleId = employee.RoleId
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

    private async Task LoadDynamicRolesAsync()
    {
        ViewBag.DynamicRoles = await _context.DynamicRoles
            .Find(_ => true)
            .SortBy(role => role.RoleName)
            .ToListAsync();
    }
}
