using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bus_ticket.Data;
using MongoDB.Driver;

namespace Bus_ticket.Services
{
    // Kế thừa BackgroundService để nó có thể chạy ngầm liên tục
    public class ArticleProcessorConsumer : BackgroundService
    {
        private const string QueueName = "news_urls_queue";
        private readonly string _hostName = "localhost";
        private readonly IServiceScopeFactory _scopeFactory;

        // Bắt buộc dùng IServiceScopeFactory vì BackgroundService là Singleton, 
        // trong khi ApplicationDbContext thường là Scoped.
        public ArticleProcessorConsumer(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory { HostName = _hostName };
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();

            channel.QueueDeclare(queue: QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            
            // YÊU CẦU BÀI TẬP: BasicQos(prefetchCount: 1) - Xử lý từng bài một cho đỡ nghẽn
            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var url = Encoding.UTF8.GetString(body);
                Console.WriteLine($"[CONSUMER] Nhận được URL từ Queue: {url}");

                try
                {
                    await ProcessAndSaveArticleAsync(url);
                    // Báo cho RabbitMQ biết là đã xử lý xong, có thể xóa URL này khỏi hàng đợi
                    channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CONSUMER ERROR] Lỗi xử lý {url}: {ex.Message}");
                    // Nếu lỗi, trả lại vào Queue để xử lý sau
                    channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                }
            };

            channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);
            
            return Task.CompletedTask;
        }

        private async Task ProcessAndSaveArticleAsync(string url)
        {
            // Mở Scope tạm để lấy Scraper và DbContext
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var scraper = scope.ServiceProvider.GetRequiredService<NewsScraperService>();

            var newsCollection = dbContext.News;

            // YÊU CẦU BÀI TẬP: Check trùng lặp
            var isExist = await newsCollection.Find(n => n.OriginalUrl == url).AnyAsync();
            if (isExist)
            {
                Console.WriteLine($"[CONSUMER] Bỏ qua, bài viết đã tồn tại: {url}");
                return;
            }

            // Bóc tách dữ liệu chi tiết
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
                    news.ThumbnailUrl = $"https://sonhailimousine.com/{news.ThumbnailUrl.TrimStart('/')}";
                else if (string.IsNullOrEmpty(news.ThumbnailUrl))
                    news.ThumbnailUrl = "https://images.unsplash.com/photo-1544620347-c4fd4a3d5957?q=80&w=600&auto=format&fit=crop";
                
                news.Content += $"<p class='text-right text-xs italic mt-6 text-gray-400'>Theo {news.SourceSite} / Nguồn gốc: <a class='text-blue-500 hover:underline' href='{url}' target='_blank' rel='nofollow'>Xem bài viết gốc</a></p>";
                
                // Lưu vào Mongo
                await newsCollection.InsertOneAsync(news);
                Console.WriteLine($"[CONSUMER SUCCESS] Đã cào và lưu thành công: {news.Title}");
            }
        }
    }
}