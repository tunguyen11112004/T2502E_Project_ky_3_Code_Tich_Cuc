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
            // 1. TẠO HTTPCLIENT CÓ CẤU HÌNH USER-AGENT ĐỂ VƯỢT TƯỜNG LỬA CHẶN BOT
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "vi-VN,vi;q=0.9,en-US;q=0.8,en;q=0.7");

            // Khởi tạo service cào (Nếu hàm khởi tạo NewsScraperService cho phép nhận HttpClient, hãy truyền nó vào. 
            // Nếu không, ta vẫn dùng các hàm cấu hình trên diện rộng của hệ thống).
            var scraper = new NewsScraperService();
            
            string categoryUrl = "https://sonhailimousine.com/tin-tuc/";
            
            // Sử dụng một XPath bao quát, an toàn hơn để bốc trọn mọi liên kết bài viết từ Sơn Hải
            string listXpath = "//a[contains(@href, '/tin-tuc/')] | //h3[contains(@class,'post-title')]/a | //h2[contains(@class,'entry-title')]/a"; 
            
            Console.WriteLine("=== BẮT ĐẦU QUÉT TIN TỨC TỪ SƠN HẢI LIMOUSINE ===");
            System.Diagnostics.Debug.WriteLine("=== BẮT ĐẦU QUÉT TIN TỨC TỪ SƠN HẢI LIMOUSINE ===");

            List<string> articleUrls = new List<string>();
            try
            {
                // Gọi hàm lấy danh sách link
                articleUrls = await scraper.GetListUrlsAsync(categoryUrl, listXpath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Lỗi khi kết nối đến trang Sơn Hải: {ex.Message}");
            }

            // In số lượng link thô cào được ra màn hình Terminal/Console để dễ kiểm soát
            string debugCountMessage = $"[DEBUG] Số lượng link tìm thấy trên Sơn Hải: {articleUrls?.Count ?? 0}";
            Console.WriteLine(debugCountMessage);
            System.Diagnostics.Debug.WriteLine(debugCountMessage);

            if (articleUrls == null || articleUrls.Count == 0)
            {
                TempData["InfoMessage"] = "Không tìm thấy link bài viết nào. Hãy kiểm tra lại tab Terminal, trang gốc có thể đã nâng cấp tường lửa bảo mật.";
                return RedirectToAction("ManageNews");
            }

            // Làm sạch và lọc các URL hợp lệ thuộc trang sonhailimousine, loại bỏ các trang con lặp và thẻ neo '#'
            var cleanUrls = articleUrls
                .Where(url => !string.IsNullOrEmpty(url) && !url.Contains("#") && !url.EndsWith("/tin-tuc/") && !url.Contains("/page/"))
                .Select(url => url.StartsWith("http") ? url : $"https://sonhailimousine.com{url}")
                .Distinct()
                .ToList();

            Console.WriteLine($"[DEBUG] Số lượng link sau khi lọc trùng lặp: {cleanUrls.Count}");

            if (cleanUrls.Count == 0)
            {
                TempData["InfoMessage"] = "Hệ thống quét được liên kết tổng thể, nhưng không lọc được bài viết tin tức nào mới.";
                return RedirectToAction("ManageNews");
            }

            int count = 0;
            var newsCollection = _dbContext.News;

            // Tiến hành cào thử nghiệm 5 bài viết mới nhất
            foreach (var url in cleanUrls.Take(5))
            {
                Console.WriteLine($"[DEBUG] Đang xử lý bóc tách bài viết: {url}");
                
                // Kiểm tra trùng lặp bản ghi trên MongoDB bằng OriginalUrl
                var isExist = newsCollection.Find(n => n.OriginalUrl == url).Any();
                if (isExist) 
                {
                    Console.WriteLine("--> [Bỏ qua] Bài viết này đã tồn tại trong MongoDB.");
                    continue; 
                }

                // Tối ưu lại toàn bộ XPath chi tiết khớp chuẩn bố cục cấu trúc HTML của Sơn Hải Limousine
                var news = await scraper.ScrapePostDetailAsync(
                    postUrl: url,
                    titleXpath: "//h1[contains(@class,'post-title')] | //h1[contains(@class,'entry-title')] | //h1", 
                    descXpath: "//div[contains(@class,'post-excerpt')] | //p[contains(@class,'description')] | //div[contains(@class,'entry-content')]/p[1]",
                    contentXpath: "//div[contains(@class,'entry-content')] | //div[contains(@class,'post-content')] | //article", 
                    thumbXpath: "//div[contains(@class,'entry-content')]//img | //div[contains(@class,'post-content')]//img"
                );

                if (news != null && !string.IsNullOrEmpty(news.Title))
                {
                    news.SourceSite = "Sơn Hải Limousine"; // Cập nhật tên nguồn nhà xe
                    
                    // Chuẩn hóa đường dẫn ảnh Thumbnail nếu là link tương đối
                    if (!string.IsNullOrEmpty(news.ThumbnailUrl) && !news.ThumbnailUrl.StartsWith("http"))
                    {
                        news.ThumbnailUrl = $"https://sonhailimousine.com{news.ThumbnailUrl}";
                    }
                    
                    // Nếu không lấy được ảnh thumbnail, gán một ảnh mặc định để giao diện không bị trống
                    if (string.IsNullOrEmpty(news.ThumbnailUrl))
                    {
                        news.ThumbnailUrl = "https://images.unsplash.com/photo-1544620347-c4fd4a3d5957?q=80&w=600&auto=format&fit=crop";
                    }

                    // Chèn thêm dấu ấn chân trang dẫn về nguồn bài viết gốc công khai
                    news.Content += $"<p class='text-right text-xs italic mt-6 text-gray-400'>Theo {news.SourceSite} / Nguồn gốc: <a class='text-blue-500 hover:underline' href='{url}' target='_blank' rel='nofollow'>Xem bài viết gốc</a></p>";
                    
                    // Thêm dữ liệu vào MongoDB theo chế độ Async
                    await newsCollection.InsertOneAsync(news);
                    count++;
                    
                    Console.WriteLine($"--> [Thành công] Đã lưu bài vào MongoDB: {news.Title}");
                }
                else 
                {
                    Console.WriteLine("--> [Thất bại] Nội dung hoặc tiêu đề bài viết từ trang này trả về bị trống.");
                }
            }

            if (count > 0)
            {
                TempData["SuccessMessage"] = $"Đồng bộ thành công! Hệ thống đã cập nhật thêm {count} bài viết mới từ Sơn Hải Limousine vào MongoDB.";
            }
            else
            {
                TempData["InfoMessage"] = "Quá trình quét hoàn tất nhưng không có thêm bài viết mới nào được lưu (Tin tức đã trùng lặp hoặc đã cũ).";
            }

            return RedirectToAction("ManageNews"); 
        }

        // Trang quản lý và hiển thị danh sách tin tức dành cho Admin
        public IActionResult ManageNews()
        {
            var newsList = _dbContext.News.Find(_ => true).ToList()
                                          .OrderByDescending(n => n.CreatedDate)
                                          .ToList();
            return View(newsList);
        }
    }
}