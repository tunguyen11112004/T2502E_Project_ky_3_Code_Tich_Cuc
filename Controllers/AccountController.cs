using Bus_ticket.Data;
using Bus_ticket.Services;
using Bus_ticket.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Bus_ticket.Controllers;

public class AccountController : Controller
{
    private readonly UserService _userService;

    public AccountController(UserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var email = model.Email.Trim().ToLower();

        var user = await _userService.GetByEmailAsync(email);

        if (user == null || user.Status != "Active")
        {
            ModelState.AddModelError("", "Invalid email or password.");
            return View(model);
        }

        var isPasswordValid = BCrypt.Net.BCrypt.Verify(
            model.Password,
            user.PasswordHash
        );

        if (!isPasswordValid)
        {
            ModelState.AddModelError("", "Invalid email or password.");
            return View(model);
        }

        if (user.Role != "Admin" && string.IsNullOrWhiteSpace(user.RoleId))
        {
            ModelState.AddModelError("", "Tài khoản chưa được gán vai trò nghiệp vụ. Vui lòng liên hệ quản trị viên.");
            return View(model);
        }

        var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id ?? string.Empty),
                new Claim(ClaimTypes.Name, user.FullName ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(ClaimTypes.Role, user.Role ?? string.Empty),
                new Claim("EmployeeCode", user.EmployeeCode ?? string.Empty),
                new Claim("RoleId", user.RoleId ?? string.Empty)
            };
        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme
        );

        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal
        );

        if (user.Role == "Admin")
        {
            return RedirectToAction("Index", "Admin");
        }

        if (user.Role == "Employee" || !string.IsNullOrWhiteSpace(user.RoleId))
        {
            return Redirect(ResolveEmployeeLandingPath(user.RoleId));
        }

        return RedirectToAction("AccessDenied", "Account");
    }

    private static string ResolveEmployeeLandingPath(string? roleId)
    {
        if (string.Equals(roleId, DataSeeder.RoleTicketAgentId, StringComparison.Ordinal))
            return "/Booking/Create";
        if (string.Equals(roleId, DataSeeder.RoleOperationsStaffId, StringComparison.Ordinal))
            return "/Admin/PriceConfig";
        if (string.Equals(roleId, DataSeeder.RoleAccountantId, StringComparison.Ordinal))
            return "/Booking/RefundList";
        if (string.Equals(roleId, DataSeeder.RoleBranchManagerId, StringComparison.Ordinal))
            return "/Dashboard";

        return "/Booking/Index";
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme
        );

        return RedirectToAction("Login", "Account");
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View();
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}