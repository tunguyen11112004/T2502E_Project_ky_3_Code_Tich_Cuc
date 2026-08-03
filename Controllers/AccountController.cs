using Bus_ticket.Data;
using Bus_ticket.Helpers;
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
    private const string ForgotPasswordMessage =
        "Nếu email tồn tại trong hệ thống, liên kết đặt lại mật khẩu đã được tạo.";

    private readonly UserService _userService;
    private readonly PasswordResetService _passwordResetService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserService userService,
        PasswordResetService passwordResetService,
        IWebHostEnvironment environment,
        ILogger<AccountController> logger)
    {
        _userService = userService;
        _passwordResetService = passwordResetService;
        _environment = environment;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login()
    {
        if (TempData["PasswordResetSuccess"] is string successMessage)
        {
            ViewBag.SuccessMessage = successMessage;
        }

        if (Request.Cookies.TryGetValue(AuthSessionHelper.AuthNoticeCookie, out var notice)
            && notice == AuthSessionHelper.SessionReplacedNotice)
        {
            ViewBag.LoginMessage =
                "Phiên đăng nhập đã kết thúc vì tài khoản được sử dụng ở thiết bị hoặc trình duyệt khác.";
            Response.Cookies.Delete(AuthSessionHelper.AuthNoticeCookie);
        }

        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
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

        var sessionToken = Guid.NewGuid().ToString("N");
        await _userService.SetActiveSessionAsync(user.Id!, sessionToken);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id ?? string.Empty),
            new Claim(ClaimTypes.Name, user.FullName ?? string.Empty),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Role, user.Role ?? string.Empty),
            new Claim("EmployeeCode", user.EmployeeCode ?? string.Empty),
            new Claim("RoleId", user.RoleId ?? string.Empty),
            new Claim(AuthSessionHelper.SessionTokenClaim, sessionToken)
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

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var user = await _userService.GetByEmailAsync(normalizedEmail);

        // Luôn trả cùng một thông báo để không làm lộ email nào có tài khoản.
        ViewBag.Message = ForgotPasswordMessage;

        if (user != null && user.Status == "Active")
        {
            try
            {
                var token = await _passwordResetService.CreateTokenAsync(user);
                var resetLink = Url.Action(
                    nameof(ResetPassword),
                    "Account",
                    new { email = user.Email, token },
                    Request.Scheme);

                // Hiện link trực tiếp khi chạy localhost để nhóm có thể test ngay.
                // Production phải gửi link bằng EmailService và không hiển thị token trên màn hình.
                if (_environment.IsDevelopment() && !string.IsNullOrWhiteSpace(resetLink))
                {
                    ViewBag.ResetLink = resetLink;
                }

                _logger.LogInformation(
                    "Password reset token created for user {UserId}. Expires after 15 minutes.",
                    user.Id);
            }
            catch (Exception ex)
            {
                // Không báo lỗi chi tiết ra giao diện vì có thể làm lộ trạng thái tài khoản.
                _logger.LogError(ex, "Could not create password reset token for email {Email}.", normalizedEmail);
            }
        }

        ModelState.Clear();
        return View(new ForgotPasswordViewModel());
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ResetPassword(string? email, string? token)
    {
        var model = new ResetPasswordViewModel
        {
            Email = email?.Trim().ToLowerInvariant() ?? string.Empty,
            Token = token ?? string.Empty
        };

        var isValid = !string.IsNullOrWhiteSpace(model.Email)
                      && !string.IsNullOrWhiteSpace(model.Token)
                      && await _passwordResetService.IsTokenValidAsync(model.Email, model.Token);

        if (!isValid)
        {
            ViewBag.InvalidLink = true;
            ViewBag.Message = "Liên kết đặt lại mật khẩu không hợp lệ, đã được sử dụng hoặc đã hết hạn.";
        }

        return View(model);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _passwordResetService.ResetPasswordAsync(
            model.Email,
            model.Token,
            model.NewPassword);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        TempData["PasswordResetSuccess"] =
            "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.";

        return RedirectToAction(nameof(Login));
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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await _userService.ClearActiveSessionAsync(userId);
        }

        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme
        );

        return RedirectToAction("Login", "Account");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> CheckSession()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessionToken = User.FindFirstValue(AuthSessionHelper.SessionTokenClaim);
        var isValid = await _userService.IsSessionValidAsync(userId ?? string.Empty, sessionToken ?? string.Empty);

        if (!isValid)
        {
            Response.Cookies.Append(
                AuthSessionHelper.AuthNoticeCookie,
                AuthSessionHelper.SessionReplacedNotice,
                new CookieOptions
                {
                    MaxAge = TimeSpan.FromMinutes(2),
                    HttpOnly = false,
                    IsEssential = true
                });

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return Json(new
            {
                valid = false,
                redirectUrl = Url.Action("Login", "Account")
            });
        }

        return Json(new { valid = true });
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        var user = await _userService.GetByIdAsync(userId);
        if (user == null || user.Status != "Active")
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.PasswordHash))
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Mật khẩu hiện tại không đúng.");
            return View(model);
        }

        if (BCrypt.Net.BCrypt.Verify(model.NewPassword, user.PasswordHash))
        {
            ModelState.AddModelError(nameof(model.NewPassword), "Mật khẩu mới không được trùng với mật khẩu hiện tại.");
            return View(model);
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword, 10);
        var updatedBy = User.FindFirstValue(ClaimTypes.Email) ?? userId;

        var changed = await _userService.UpdatePasswordAsync(
            userId,
            passwordHash,
            updatedBy);

        if (!changed)
        {
            ModelState.AddModelError(string.Empty, "Không thể cập nhật mật khẩu. Vui lòng thử lại.");
            return View(model);
        }

        // Đổi mật khẩu thành công sẽ vô hiệu hóa phiên hiện tại và buộc đăng nhập lại.
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        TempData["PasswordResetSuccess"] =
            "Đổi mật khẩu thành công. Vui lòng đăng nhập lại bằng mật khẩu mới.";

        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
