using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Bus_ticket.Services
{
    public class CrawlerProducer
    {
        private const string QueueName = "news_urls_queue";
        private readonly string _hostName = "localhost"; // Sửa nếu server RabbitMQ ở IP khác

        public async Task StartCrawlingAsync()
        {
            Console.WriteLine("[PRODUCER] Bắt đầu quét link bài viết từ trang chuyên mục...");
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

            var articleUrls = new List<string>();
            try
            {
                string htmlContent = await httpClient.GetStringAsync("https://sonhailimousine.com/tin-tuc/");
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);
                var aNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'newsnb_gr_title')]/a");

                if (aNodes != null)
                {
                    foreach (var node in aNodes)
                    {
                        string href = node.GetAttributeValue("href", "");
                        if (!string.IsNullOrEmpty(href)) articleUrls.Add(href);
                    }
                }

                // Lọc link rác
                var cleanUrls = articleUrls
                    .Where(url => !string.IsNullOrEmpty(url))
                    .Select(url => url.StartsWith("http") ? url : $"https://sonhailimousine.com/{url.TrimStart('/')}")
                    .Where(url =>
                        !url.Contains("/page/") && !url.Contains("#") &&
                        url.TrimEnd('/') != "https://sonhailimousine.com/tin-tuc")
                    .Distinct()
                    .ToList();

                // Gửi danh sách link vào RabbitMQ (Chuyển sang gọi hàm Async)
                await SendToQueueAsync(cleanUrls);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PRODUCER ERROR] Lỗi quét trang: {ex.Message}");
            }
        }

        private async Task SendToQueueAsync(List<string> links)
        {
            var factory = new ConnectionFactory { HostName = _hostName };

            // v7.x+: Mở kết nối và tạo channel dạng bất đồng bộ (Async)
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            // v7.x+: Khai báo hàng đợi bất đồng bộ
            await channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            // v7.x+: Khởi tạo cấu hình tin nhắn bền vững (Persistent) trực tiếp qua class BasicProperties
            var properties = new BasicProperties
            {
                DeliveryMode = DeliveryModes.Persistent // Lưu tin nhắn xuống đĩa cứng phòng khi RabbitMQ bị restart
            };

            foreach (var link in links)
            {
                var body = Encoding.UTF8.GetBytes(link);

                // v7.x+: Sử dụng BasicPublishAsync thay thế cho BasicPublish truyền thống
                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: QueueName,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);

                Console.WriteLine($"[PRODUCER] Đã ném vào Queue: {link}");
            }
        }
    }
}