using Bus_ticket.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bus_ticket.Models;
using MongoDB.Driver;
using System.Net.Http;
using AngleSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bus_ticket.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        public AdminController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Booking()
        {
            return View();
        }
        
        [HttpGet]
        public IActionResult CrawlNews()
        {
            return View(new List<string>());
        }
        
        [HttpPost]
        public async Task<IActionResult> CrawlNews(string sourceUrl, string cssSelector)
        {
            var extractedLinks = new List<string>();
            ViewBag.SourceUrl = sourceUrl;
            ViewBag.CssSelector = cssSelector;

            if (string.IsNullOrEmpty(sourceUrl) || string.IsNullOrEmpty(cssSelector))
            {
                TempData["InfoMessage"] = "Vui lòng nhập đầy đủ URL nguồn và CSS Selector.";
                return View(extractedLinks);
            }

            try
            {
                var config = Configuration.Default.WithDefaultLoader();
                var context = BrowsingContext.New(config);
                var document = await context.OpenAsync(sourceUrl);
                var elements = document.QuerySelectorAll(cssSelector);
                foreach (var element in elements)
                {
                    string href = element.GetAttribute("href");
                    if (!string.IsNullOrEmpty(href))
                    {
                        if (Uri.TryCreate(new Uri(sourceUrl), href, out var absoluteUri))
                        {
                            extractedLinks.Add(absoluteUri.ToString());
                        }
                    }
                }
                extractedLinks = extractedLinks.Distinct().ToList();
                if (extractedLinks.Count == 0)
                {
                    TempData["InfoMessage"] = "Không tìm thấy đường dẫn nào khớp với CSS Selector đã nhập.";
                }
                else
                {
                    TempData["SuccessMessage"] = $"Quét thành công! Tìm thấy {extractedLinks.Count} đường dẫn con.";
                }
            }
            catch (Exception ex)
            {
                TempData["InfoMessage"] = $"Lỗi trong quá trình quét dữ liệu: {ex.Message}";
            }
            return View(extractedLinks);
        }
        
        [HttpPost]
        public async Task<IActionResult> PushToQueue(string targetUrl)
        {
            if (string.IsNullOrEmpty(targetUrl))
            {
                TempData["InfoMessage"] = "Đường dẫn gửi đi không hợp lệ.";
                return RedirectToAction("CrawlNews");
            }

            try
            {
                TempData["SuccessMessage"] = $"Đã gửi link: {targetUrl} vào hàng đợi xử lý!";
            }
            catch (Exception ex)
            {
                TempData["InfoMessage"] = $"Lỗi khi đẩy vào Queue: {ex.Message}";
            }
            return RedirectToAction("CrawlNews");
        }
        
        public IActionResult ManageNews()
        {
            var newsList = _dbContext.News.Find(_ => true).ToList()
                                          .OrderByDescending(n => n.CreatedDate)
                                          .ToList();
            return View(newsList);
        }
    }
}