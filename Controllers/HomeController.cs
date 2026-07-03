using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Bus_ticket.Models;
using Microsoft.AspNetCore.Localization;
using MongoDB.Driver;
using Bus_ticket.Models;
using Bus_ticket.Data;

namespace Bus_ticket.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _dbContext;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
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
        var topNews = _dbContext.News.Find(_ => true)
            .SortByDescending(n => n.CreatedDate)
            .Limit(2)
            .ToList();
        return View(topNews);
    }
    
    public IActionResult About() 
    {
        return View();
    }
    
    public IActionResult Contact() 
    {
        return View();
    }
    
    public IActionResult News() 
    {
        var newsList = _dbContext.News.Find(_ => true).ToList()
            .OrderByDescending(n => n.CreatedDate)
            .ToList();
        return View(newsList);
    }
    public IActionResult NewsDetail(string id)
    {
        if (string.IsNullOrEmpty(id)) 
        {
            return NotFound();
        }
        var article = _dbContext.News.Find(n => n.Id == id).FirstOrDefault();
    
        if (article == null) 
        {
            return NotFound();
        }
        return View(article);
    }
    public IActionResult Recruitment() 
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