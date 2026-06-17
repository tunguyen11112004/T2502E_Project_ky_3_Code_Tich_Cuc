using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Bus_ticket.Models;
using Microsoft.AspNetCore.Localization;

namespace Bus_ticket.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }
    
    public IActionResult SetLanguage(string culture, string returnUrl)
    {
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
        );

        return LocalizeRedirect(returnUrl);
    }

    private IActionResult LocalizeRedirect(string returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("Index");
    }

    public IActionResult Index()
    {
        return View();
    }
    
    public IActionResult About() 
    {
        return View();
    }
    
    public IActionResult Contact() 
    {
        return View();
    }

    public IActionResult Policy()
    {
        return View();
    }

    // Tìm đến nạp file Views/Home/Policy.cshtml
    public IActionResult Pricing()
    {
        // Trả về file giao diện Views/Home/Pricing.cshtml vừa tạo
        return View();
    }

    public IActionResult FAQ()
    {
        // Ngày cập nhật FAQ cuối cùng (cố định)
        ViewBag.FAQLastUpdated = new DateTime(2026, 6, 12);
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
    
    
}