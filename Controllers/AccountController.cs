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
    private readonly MongoUserService _mongoUserService;

    public AccountController(MongoUserService mongoUserService)
    {
        _mongoUserService = mongoUserService;
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

        var user = await _mongoUserService.GetByEmailAsync(email);

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

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id ?? string.Empty),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("EmployeeCode", user.EmployeeCode)
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

        if (user.Role == "Employee")
        {
            return RedirectToAction("Index", "Employee");
        }

        return RedirectToAction("AccessDenied", "Account");
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