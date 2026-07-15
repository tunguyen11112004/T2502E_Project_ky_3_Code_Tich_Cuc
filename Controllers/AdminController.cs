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
        public async Task<IActionResult> SaveDraftLink(string targetUrl)
        {
            if (string.IsNullOrEmpty(targetUrl))
            {
                TempData["InfoMessage"] = "Đường dẫn không hợp lệ.";
                return RedirectToAction("CrawlNews");
            }
            try
            {
                var newsCollection = _dbContext.News;
                var isExist = await newsCollection.Find(n => n.OriginalUrl == targetUrl).AnyAsync();
                if (isExist)
                {
                    TempData["InfoMessage"] = "Liên kết này đã tồn tại trong danh sách quản lý!";
                    return RedirectToAction("CrawlNews");
                }
                string sourceName = "Nguồn khác";
                try
                {
                    var uri = new Uri(targetUrl);
                    string host = uri.Host.ToLower(); 
                    sourceName = host.StartsWith("www.") ? host.Substring(4) : host;
                    sourceName = char.ToUpper(sourceName[0]) + sourceName.Substring(1);
                }
                catch { }
                string realTitle = "Bài viết chưa lấy được tiêu đề";
                string? realThumbnail = null;
                try
                {
                    var scraper = new NewsScraperService();
                    var scrapedData = await scraper.ScrapePostDetailAsync(
                        postUrl: targetUrl,
                        titleXpath: "//h1[contains(@class,'post-title')] | //h1[contains(@class,'entry-title')] | //h1 | //div[contains(@class,'title-main')]", 
                        descXpath: "//div[contains(@class,'post-excerpt')] | //div[contains(@class,'entry-content')]/p[1] | //div[contains(@class,'content-main')]/p[1]",
                        contentXpath: "//div[contains(@class,'entry-content')] | //div[contains(@class,'post-content')] | //div[contains(@class,'content-main')]", 
                        thumbXpath: "//div[contains(@class,'entry-content')]//img | //div[contains(@class,'content-main')]//img"
                    );
                    if (scrapedData != null)
                    {
                        if (!string.IsNullOrEmpty(scrapedData.Title))
                        {
                            realTitle = scrapedData.Title;
                        }
                        realThumbnail = scrapedData.ThumbnailUrl;
                    }
                }
                catch (Exception)
                {
                    realTitle = "Lỗi tự động cào tiêu đề (Bấm Biên tập để thử lại)";
                }
                if (string.IsNullOrEmpty(realThumbnail))
                {
                    realThumbnail = "https://images.unsplash.com/photo-1544620347-c4fd4a3d5957?q=80&w=600&auto=format&fit=crop";
                }
                var draftNews = new News
                {
                    Title = realTitle,            
                    ThumbnailUrl = realThumbnail, 
                    OriginalUrl = targetUrl,
                    SourceSite = sourceName,
                    CreatedDate = DateTime.Now,
                    IsCrawled = false,            
                    IsApproved = false
                };
                await newsCollection.InsertOneAsync(draftNews);
                TempData["SuccessMessage"] = $"Đã lưu thành công bài viết: \"{realTitle}\"!";
            }
            catch (Exception ex)
            {
                TempData["InfoMessage"] = $"Lỗi khi lưu bài viết: {ex.Message}";
            }
            return RedirectToAction("CrawlNews");
        }
        
        [HttpPost]
        public async Task<IActionResult> ApproveNews(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var filter = Builders<News>.Filter.Eq(n => n.Id, id);
            var update = Builders<News>.Update.Set(n => n.IsApproved, true);
    
            await _dbContext.News.UpdateOneAsync(filter, update);

            TempData["SuccessMessage"] = "Đã xuất bản bài viết thành công lên trang tin!";
            return RedirectToAction("ManageNews");
        }
        
        [HttpGet]
        public async Task<IActionResult> EditNews(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var news = await _dbContext.News.Find(n => n.Id == id).FirstOrDefaultAsync();
            if (news == null) return NotFound();
            if (string.IsNullOrEmpty(news.TitleXpath)) news.TitleXpath = "//h1";
            if (string.IsNullOrEmpty(news.DescXpath)) news.DescXpath = "//p[contains(@class,'description')] | //h2";
            if (string.IsNullOrEmpty(news.ContentXpath)) news.ContentXpath = "//div[contains(@class,'content')] | //article";
            if (string.IsNullOrEmpty(news.ThumbXpath)) news.ThumbXpath = "//img";
            return View(news);
        }
        
        [HttpPost]
        public async Task<IActionResult> EditNews(News model, string actionSubmit)
        {
            if (model == null || string.IsNullOrEmpty(model.Id)) return BadRequest();
            if (actionSubmit == "CrawlNow")
            {
                if (string.IsNullOrEmpty(model.OriginalUrl))
                {
                    ViewBag.ErrorMessage = "Không tìm thấy URL nguồn để cào tin!";
                    return View(model);
                }
                try
                {
                    var scraper = new NewsScraperService();
                    var scrapedData = await scraper.ScrapePostDetailAsync(
                        postUrl: model.OriginalUrl,
                        titleXpath: model.TitleXpath ?? "//h1",
                        descXpath: model.DescXpath ?? "//h2",
                        contentXpath: model.ContentXpath ?? "//article",
                        thumbXpath: model.ThumbXpath ?? "//img"
                    );
                    if (scrapedData != null)
                    {
                        model.Title = !string.IsNullOrEmpty(scrapedData.Title) ? scrapedData.Title : model.Title;
                        model.Description = scrapedData.Description;
                        model.Content = scrapedData.Content;
                        if (!string.IsNullOrEmpty(scrapedData.ThumbnailUrl))
                        {
                            model.ThumbnailUrl = scrapedData.ThumbnailUrl;
                        }
                        ModelState.Clear(); 
                        TempData["SuccessMessage"] = "Đã lấy dữ liệu theo cấu hình XPath thành công! Cậu kiểm tra lại nội dung phía dưới nhé.";
                    }
                    else
                    {
                        ViewBag.ErrorMessage = "Không lấy được dữ liệu. Kiểm tra lại biểu thức XPath và đường dẫn bài viết gốc.";
                    }
                }
                catch (Exception ex)
                {
                    ViewBag.ErrorMessage = $"Lỗi khi chạy cào thử: {ex.Message}";
                }
                return View(model);
            }
            try
            {
                var filter = Builders<News>.Filter.Eq(n => n.Id, model.Id);
                var update = Builders<News>.Update
                    .Set(n => n.Title, model.Title)
                    .Set(n => n.Description, model.Description)
                    .Set(n => n.Content, model.Content)
                    .Set(n => n.ThumbnailUrl, model.ThumbnailUrl)
                    .Set(n => n.TitleXpath, model.TitleXpath)
                    .Set(n => n.DescXpath, model.DescXpath)
                    .Set(n => n.ContentXpath, model.ContentXpath)
                    .Set(n => n.ThumbXpath, model.ThumbXpath)
                    .Set(n => n.IsCrawled, true) 
                    .Set(n => n.IsApproved, model.IsApproved);
                await _dbContext.News.UpdateOneAsync(filter, update);
                TempData["SuccessMessage"] = "Đã lưu bản ghi bài viết và hoàn tất biên tập!";
                return RedirectToAction("ManageNews");
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi hệ thống khi cập nhật DB: {ex.Message}";
                return View(model);
            }
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