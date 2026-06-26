using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;

public class NewsScraperService
{
    private readonly HttpClient _httpClient;

    public NewsScraperService()
    {
        _httpClient = new HttpClient();
        // Giả lập đầy đủ Header của trình duyệt Chrome thật để vượt qua các lớp chặn cơ bản
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "vi-VN,vi;q=0.9,en-US;q=0.8,en;q=0.7");
    }

    // 1. CẬP NHẬT HÀM LẤY DANH SÁCH LINK BÀI VIẾT
    public async Task<List<string>> GetListUrlsAsync(string categoryUrl, string xpathSelector)
    {
        var urls = new List<string>();
        try
        {
            var html = await _httpClient.GetStringAsync(categoryUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Thử nghiệm tìm kiếm rộng hơn nếu selector cụ thể bị trượt
            var nodes = doc.DocumentNode.SelectNodes(xpathSelector);
            
            // DỰ PHÒNG: Nếu selector truyền vào không tìm thấy gì, quét tất cả thẻ a nằm trong khối bài viết
            if (nodes == null)
            {
                nodes = doc.DocumentNode.SelectNodes("//article//h3/a | //div[contains(@class,'post')]//a");
            }

            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    string href = node.GetAttributeValue("href", "");
                    // Loại bỏ các link nhãn, link bình luận hoặc link trùng lặp
                    if (!string.IsNullOrEmpty(href) && (href.Contains("/tin-tuc/") || href.EndsWith(".html") || href.Split('/').Length > 4))
                    {
                        if (!urls.Contains(href))
                        {
                            urls.Add(href);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi nghiêm trọng khi lấy danh sách URL: {ex.Message}");
        }
        return urls;
    }

    // 2. CẬP NHẬT HÀM CÀO CHI TIẾT BÀI VIẾT (TỐI ƯU SELECTOR HOẶC)
    public async Task<Bus_ticket.Models.News?> ScrapePostDetailAsync(string postUrl, string titleXpath, string descXpath, string contentXpath, string thumbXpath)
    {
        try
        {
            var html = await _httpClient.GetStringAsync(postUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Quét Tiêu đề (Bảo hiểm bằng cách quét thẻ h1 duy nhất của trang nếu trượt class)
            var titleNode = doc.DocumentNode.SelectSingleNode(titleXpath) ?? doc.DocumentNode.SelectSingleNode("//h1");
            string title = titleNode?.InnerText?.Trim() ?? "";

            if (string.IsNullOrEmpty(title)) return null;

            // Quét Mô tả
            var descNode = doc.DocumentNode.SelectSingleNode(descXpath);
            string description = descNode?.InnerText?.Trim() ?? "";

            // Quét Nội dung (Dự phòng quét khối chứa bài viết chuẩn WordPress: .entry-content)
            var contentNode = doc.DocumentNode.SelectSingleNode(contentXpath) 
                               ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class,'entry-content')]")
                               ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class,'post-content')]");
            string content = contentNode?.InnerHtml?.Trim() ?? "Nội dung bài viết đang được cập nhật.";

            // Quét Ảnh đại diện
            var thumbNode = doc.DocumentNode.SelectSingleNode(thumbXpath) ?? contentNode?.SelectSingleNode(".//img");
            string thumbUrl = thumbNode?.GetAttributeValue("src", "") 
                               ?? thumbNode?.GetAttributeValue("data-src", "") ?? ""; // Một số trang dùng Lazyload ảnh qua thuộc tính data-src

            return new Bus_ticket.Models.News
            {
                Title = title,
                Description = description,
                Content = content,
                ThumbnailUrl = thumbUrl,
                OriginalUrl = postUrl,
                IsCloned = true,
                CreatedDate = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Lỗi khi cào bài viết chi tiết {postUrl}: {ex.Message}");
            return null;
        }
    }
}