using Bus_ticket.Models;
using Bus_ticket.Services;
using Bus_ticket.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Bus_ticket.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly MongoUserService _mongoUserService;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        MongoUserService mongoUserService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _mongoUserService = mongoUserService;
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var existingUser = await _userManager.FindByNameAsync(model.Username);

        if (existingUser != null)
        {
            ModelState.AddModelError("", "Username already exists.");
            return View(model);
        }

        var existingEmail = await _userManager.FindByEmailAsync(model.Email);

        if (existingEmail != null)
        {
            ModelState.AddModelError("", "Email already exists.");
            return View(model);
        }

        var random = new Random();
        string employeeCode;

        do
        {
            employeeCode = random.Next(100000, 1000000).ToString();
        }
        while (await _userManager.Users
                   .AnyAsync(u => u.EmployeeCode == employeeCode));

        var user = new ApplicationUser
        {
            UserName = model.Username,
            Email = model.Email,
            FullName = model.FullName,
            PhoneNumber = model.PhoneNumber,
            Age = model.Age,
            Education = model.Education,
            EmployeeCode = employeeCode
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            if (!await _roleManager.RoleExistsAsync("Employee"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Employee"));
            }

            await _userManager.AddToRoleAsync(user, "Employee");

            return RedirectToAction("Login");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }

        return View(model);
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

        var user = await _mongoUserService.GetByEmailAsync(model.Email);

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
            IdentityConstants.ApplicationScheme
        );

        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
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

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user == null)
        {
            ViewBag.Message = "If the email exists, a reset password link has been generated.";
            return View();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        var resetLink = Url.Action(
            "ResetPassword",
            "Account",
            new
            {
                email = model.Email,
                token = token
            },
            Request.Scheme
        );

        ViewBag.Message = "Reset password link generated successfully.";
        ViewBag.ResetLink = resetLink;

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return RedirectToAction("Login", "Account");
    }

    [Authorize(Roles = "Employee")]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View();
    }

    [HttpGet]
    public IActionResult ResetPassword(string email, string token)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
        {
            return RedirectToAction("Login");
        }

        var model = new ResetPasswordViewModel
        {
            Email = email,
            Token = token
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user == null)
        {
            ModelState.AddModelError("", "User not found.");
            return View(model);
        }

        var result = await _userManager.ResetPasswordAsync(
            user,
            model.Token,
            model.NewPassword
        );

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] =
                "Password has been reset successfully.";

            return RedirectToAction("Login");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }

        return View(model);
    }

    [Authorize(Roles = "Employee")]
    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.GetUserAsync(User);

        if (user == null)
            return RedirectToAction("Login");

        var result = await _userManager.ChangePasswordAsync(
            user,
            model.CurrentPassword,
            model.NewPassword
        );

        if (result.Succeeded)
        {
            ViewBag.Message = "Password changed successfully.";
            return View();
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}