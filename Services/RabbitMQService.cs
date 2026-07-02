using System;
using System.Text;
using System.Threading.Tasks;
using Bus_ticket.Interfaces;
using RabbitMQ.Client;

namespace Bus_ticket.Services
{
    public class RabbitMQService : IRabbitMQService
    {
        public async Task PublishOrderAsync(string orderId)
        {
            // 1. Khởi tạo Factory kết nối đến localhost
            var factory = new ConnectionFactory() { HostName = "localhost" };

            // 2. Sử dụng cú pháp Cú pháp Async mới chuẩn phiên bản RabbitMQ.Client v7+
            using var connection = await factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            // 3. Khai báo hàng đợi (QueueDeclareAsync)
            await channel.QueueDeclareAsync(queue: "order_queue", 
                durable: true, 
                exclusive: false, 
                autoDelete: false, 
                arguments: null);

            // 4. Mã hóa chuỗi dữ liệu orderId
            var message = orderId;
            var body = Encoding.UTF8.GetBytes(message);

            // 5. Bắn dữ liệu vào hàng đợi một cách bất đồng bộ
            await channel.BasicPublishAsync(exchange: "",
                routingKey: "order_queue",
                body: body);

            Console.WriteLine($" >>> [RabbitMQ] Đã đẩy orderId: {orderId} vào order_queue thành công!");
        }
    }
}