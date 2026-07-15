using RabbitMQ.Client;
using System.Text;
using HtmlAgilityPack;

namespace Bus_ticket.Services // 👈 Đã bọc đúng chuẩn namespace để các file khác nhìn thấy
{
    public class CrawlerProducer // 👈 Phải có chữ public ở đây
    {
        private const string QueueName = "news_urls_queue";
        private readonly string _hostName = "localhost"; // Sửa nếu server RabbitMQ ở IP khác

        public async Task StartCrawlingAsync()
        {
            Console.WriteLine("[PRODUCER] Bắt đầu quét link bài viết từ trang chuyên mục...");
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

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
                    .Where(url => !url.Contains("/page/") && !url.Contains("#") && url.TrimEnd('/') != "https://sonhailimousine.com/tin-tuc")
                    .Distinct()
                    .ToList();

                // Gửi danh sách link vào RabbitMQ
                SendToQueue(cleanUrls);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PRODUCER ERROR] Lỗi quét trang: {ex.Message}");
            }
        }

        private void SendToQueue(List<string> links)
        {
            var factory = new ConnectionFactory { HostName = _hostName };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            // Khai báo hàng đợi (nếu chưa có thì tự tạo)
            channel.QueueDeclare(queue: QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

            foreach (var link in links)
            {
                var body = Encoding.UTF8.GetBytes(link);
                var properties = channel.CreateBasicProperties();
                properties.Persistent = true; // Lưu tin nhắn xuống đĩa cứng để không mất khi RabbitMQ sập

                channel.BasicPublish(exchange: "", routingKey: QueueName, basicProperties: properties, body: body);
                Console.WriteLine($"[PRODUCER] Đã ném vào Queue: {link}");
            }
        }
    }
}