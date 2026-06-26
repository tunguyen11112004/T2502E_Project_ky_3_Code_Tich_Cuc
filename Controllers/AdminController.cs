using Bus_ticket.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bus_ticket.Models;
using MongoDB.Driver;
using System.Net.Http;

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

        // Trang chủ Admin
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Booking()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> CrawlNews()
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "vi-VN,vi;q=0.9,en-US;q=0.8,en;q=0.7");

            string categoryUrl = "https://sonhailimousine.com/tin-tuc/";
            var articleUrls = new List<string>();

            Console.WriteLine("=== BẮT ĐẦU TẢI TRANG DANH MỤC SƠN HẢI TỪ CONTROLLER ===");

            try
            {
                string htmlContent = await httpClient.GetStringAsync(categoryUrl);
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(htmlContent);
                var aNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'newsnb_gr_title')]/a");
                if (aNodes != null)
                {
                    foreach (var node in aNodes)
                    {
                        string href = node.GetAttributeValue("href", "");
                        if (!string.IsNullOrEmpty(href))
                        {
                            articleUrls.Add(href);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Không thể kết nối hoặc tải trang Sơn Hải: {ex.Message}");
                TempData["InfoMessage"] = $"Kiểm tra lại kết nối hoặc dịch vụ mạng: {ex.Message}";
                return RedirectToAction("ManageNews");
            }
            if (articleUrls.Count == 0)
            {
                TempData["InfoMessage"] = "Không tìm thấy link bài viết nào. Hãy kiểm tra lại cấu trúc HTML trang gốc hoặc tab Terminal.";
                return RedirectToAction("ManageNews");
            }
            var cleanUrls = articleUrls
                .Where(url => !string.IsNullOrEmpty(url))
                .Select(url => url.StartsWith("http") ? url : $"https://sonhailimousine.com/{url.TrimStart('/')}")
                .Where(url => !url.Contains("/page/") 
                           && !url.Contains("#")
                           && url.TrimEnd('/') != "https://sonhailimousine.com/tin-tuc"
                           && url.TrimEnd('/') != "https://sonhailimousine.com")
                .Distinct()
                .ToList();

            Console.WriteLine($"[DEBUG] Tìm thấy {cleanUrls.Count} link chi tiết bài viết thực thụ sau khi lọc rác.");

            if (cleanUrls.Count == 0)
            {
                TempData["InfoMessage"] = "Hệ thống tìm thấy liên kết chung nhưng không lọc được bài viết cụ thể nào mới.";
                return RedirectToAction("ManageNews");
            }
            int count = 0;
            var newsCollection = _dbContext.News;
            var scraper = new NewsScraperService();
            foreach (var url in cleanUrls.Take(5))
            {
                var isExist = newsCollection.Find(n => n.OriginalUrl == url).Any();
                if (isExist) continue;
                var news = await scraper.ScrapePostDetailAsync(
                    postUrl: url,
                    titleXpath: "//h1[contains(@class,'post-title')] | //h1[contains(@class,'entry-title')] | //h1 | //div[contains(@class,'title-main')]", 
                    descXpath: "//div[contains(@class,'post-excerpt')] | //div[contains(@class,'entry-content')]/p[1] | //div[contains(@class,'content-main')]/p[1]",
                    contentXpath: "//div[contains(@class,'entry-content')] | //div[contains(@class,'post-content')] | //div[contains(@class,'content-main')]", 
                    thumbXpath: "//div[contains(@class,'entry-content')]//img | //div[contains(@class,'content-main')]//img"
                );
                if (news != null && !string.IsNullOrEmpty(news.Title) && !news.Title.Contains("Tin tức"))
                {
                    news.SourceSite = "Sơn Hải Limousine";
                    news.CreatedDate = DateTime.Now;
                    news.OriginalUrl = url;
                    if (!string.IsNullOrEmpty(news.ThumbnailUrl) && !news.ThumbnailUrl.StartsWith("http"))
                    {
                        news.ThumbnailUrl = $"https://sonhailimousine.com/{news.ThumbnailUrl.TrimStart('/')}";
                    }
                    else if (string.IsNullOrEmpty(news.ThumbnailUrl))
                    {
                        news.ThumbnailUrl = "https://images.unsplash.com/photo-1544620347-c4fd4a3d5957?q=80&w=600&auto=format&fit=crop";
                    }
                    news.Content += $"<p class='text-right text-xs italic mt-6 text-gray-400'>Theo {news.SourceSite} / Nguồn gốc: <a class='text-blue-500 hover:underline' href='{url}' target='_blank' rel='nofollow'>Xem bài viết gốc</a></p>";
                    await newsCollection.InsertOneAsync(news);
                    count++;
                }
            }
            if (count > 0)
            {
                TempData["SuccessMessage"] = $"Đồng bộ thành công! Hệ thống đã cập nhật thêm {count} bài viết chi tiết từ Sơn Hải Limousine vào MongoDB.";
            }
            else
            {
                TempData["InfoMessage"] = "Quá trình hoàn tất. Không có tin tức mới nào được lưu (Dữ liệu đã tồn tại đầy đủ hoặc lỗi bóc tách trang con).";
            }

            return RedirectToAction("ManageNews");
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