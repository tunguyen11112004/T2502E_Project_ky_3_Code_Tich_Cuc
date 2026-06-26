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
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "vi-VN,vi;q=0.9,en-US;q=0.8,en;q=0.7");
    }
    public async Task<List<string>> GetListUrlsAsync(string categoryUrl, string xpathSelector)
    {
        var urls = new List<string>();
        try
        {
            var html = await _httpClient.GetStringAsync(categoryUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var nodes = doc.DocumentNode.SelectNodes(xpathSelector);
            if (nodes == null)
            {
                nodes = doc.DocumentNode.SelectNodes("//article//h3/a | //div[contains(@class,'post')]//a");
            }

            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    string href = node.GetAttributeValue("href", "");
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
    public async Task<Bus_ticket.Models.News?> ScrapePostDetailAsync(string postUrl, string titleXpath, string descXpath, string contentXpath, string thumbXpath)
    {
        try
        {
            var html = await _httpClient.GetStringAsync(postUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var titleNode = doc.DocumentNode.SelectSingleNode(titleXpath) ?? doc.DocumentNode.SelectSingleNode("//h1");
            string title = titleNode?.InnerText?.Trim() ?? "";
            if (string.IsNullOrEmpty(title)) return null;
            var descNode = doc.DocumentNode.SelectSingleNode(descXpath);
            string description = descNode?.InnerText?.Trim() ?? "";
            var contentNode = doc.DocumentNode.SelectSingleNode(contentXpath) 
                               ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class,'entry-content')]")
                               ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class,'post-content')]");
            string content = contentNode?.InnerHtml?.Trim() ?? "Nội dung bài viết đang được cập nhật.";
            var thumbNode = doc.DocumentNode.SelectSingleNode(thumbXpath) ?? contentNode?.SelectSingleNode(".//img");
            string thumbUrl = thumbNode?.GetAttributeValue("src", "") 
                               ?? thumbNode?.GetAttributeValue("data-src", "") ?? "";
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