using System;
using System.Text;
using System.Threading.Tasks;
using Bus_ticket.Interfaces;
using Microsoft.Extensions.Configuration; // Thêm namespace này để dùng IConfiguration
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bus_ticket.Services
{
    public class RabbitMQService : IRabbitMQService
    {
        // 1. Khai báo biến cấu hình hệ thống
        private readonly IConfiguration _configuration;

        // 2. Inject IConfiguration thông qua Constructor
        public RabbitMQService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task PublishOrderAsync(string orderId)
        {
            var factory = new ConnectionFactory();

            // 3. Logic nạp Uri thông minh kết nối tới CloudAMQP hoặc localhost
            var rabbitMqUri = _configuration["RabbitMqSettings:Uri"];
            if (!string.IsNullOrEmpty(rabbitMqUri))
            {
                factory.Uri = new Uri(rabbitMqUri);
            }
            else
            {
                factory.HostName = _configuration["RabbitMQ:HostName"] ?? "localhost";
                factory.UserName = _configuration["RabbitMQ:UserName"] ?? "guest";
                factory.Password = _configuration["RabbitMQ:Password"] ?? "guest";
                if (int.TryParse(_configuration["RabbitMQ:Port"], out int port))
                {
                    factory.Port = port;
                }
            }

            // 4. Sử dụng cú pháp Async mới chuẩn phiên bản RabbitMQ.Client v7+
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            // 5. Khai báo hàng đợi (QueueDeclareAsync)
            await channel.QueueDeclareAsync(queue: "order_queue", 
                durable: true, 
                exclusive: false, 
                autoDelete: false, 
                arguments: null);

            // 6. Mã hóa chuỗi dữ liệu orderId
            var message = orderId;
            var body = Encoding.UTF8.GetBytes(message);

            // 7. Bắn dữ liệu vào hàng đợi một cách bất đồng bộ
            await channel.BasicPublishAsync(exchange: "",
                routingKey: "order_queue",
                body: body);

            Console.WriteLine($" >>> [RabbitMQ] Đã đẩy orderId: {orderId} vào order_queue thành công!");
        }
    }
}