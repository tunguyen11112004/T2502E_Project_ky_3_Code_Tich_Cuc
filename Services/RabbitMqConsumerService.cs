using System;
using System.Linq;
using System.Net.Mail; // Định nghĩa rõ namespace tránh lỗi Ambiguous invocation
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bus_ticket.Data; // Thay bằng namespace chứa ApplicationDbContext thật của bạn nếu khác
using Bus_ticket.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bus_ticket.Services
{
    public class RabbitMqConsumerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider; // Khai báo Service Provider để tạo scope DB
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration; // Khai báo thêm Configuration
        private IConnection _connection;
        private IChannel _channel;

        // BẮT BUỘC: Constructor để Inject IServiceProvider vào hệ thống
        public RabbitMqConsumerService(IServiceProvider serviceProvider, Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory();
    
            // Ưu tiên đọc chuỗi Uri đầy đủ (Dành cho CloudAMQP trên Production)
            var rabbitMqUri = _configuration["RabbitMqSettings:Uri"];
            if (!string.IsNullOrEmpty(rabbitMqUri))
            {
                factory.Uri = new Uri(rabbitMqUri);
            }
            else
            {
                // Fallback đọc cấu hình lẻ (Hoặc tự động về localhost nếu chạy ở máy cá nhân của bạn)
                factory.HostName = _configuration["RabbitMQ:HostName"] ?? "localhost";
                factory.UserName = _configuration["RabbitMQ:UserName"] ?? "guest";
                factory.Password = _configuration["RabbitMQ:Password"] ?? "guest";
                if (int.TryParse(_configuration["RabbitMQ:Port"], out int port))
                {
                    factory.Port = port;
                }
            }

            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            await _channel.QueueDeclareAsync(queue: "order_queue",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var bookingCode = Encoding.UTF8.GetString(body);

                Console.WriteLine(
                    $"\n 🔔 [RabbitMQ] Nhận đơn hàng: {bookingCode}. Bắt đầu xử lý gửi mail vé xe thật...");

                try
                {
                    // Tạo Scope để gọi ApplicationDbContext an toàn trong BackgroundService
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                        // 1. Tìm thông tin đơn hàng trong MongoDB
                        var booking = await dbContext.Bookings.Find(b => b.BookingCode == bookingCode)
                            .FirstOrDefaultAsync();

                        if (booking != null && !string.IsNullOrEmpty(booking.CustomerEmail))
                        {
                            var emailKhach = booking.CustomerEmail;
                            var tenKhach = booking.Passengers.FirstOrDefault()?.Name ?? "Quý khách";

                            // Lấy danh sách số ghế nối nhau bằng dấu phẩy (Ví dụ: A05, A08, A11)
                            var gheDaDat = string.Join(", ", booking.Passengers.Select(p => p.SeatNumber));

                            string busInfo = "Xe không xác định";

                            // Tìm chuyến xe (Trip) trước để lấy thông tin Id xe (BusId hoặc Bus)
                            var trip = await dbContext.Trips.Find(t => t.Id == booking.TripId).FirstOrDefaultAsync();
                            if (trip != null)
                            {
                                // Giả sử bảng Trip của bạn lưu trường BusId hoặc đối tượng Bus. 
                                // Bạn chỉnh lại 'trip.BusId' cho đúng tên thuộc tính trong Model Trip của bạn nhé
                                var bus = await dbContext.Buses.Find(b => b.Id == trip.BusId).FirstOrDefaultAsync();

                                busInfo = bus != null
                                    ? $"{bus.BusCode} - Biển: {bus.LicensePlate}"
                                    : "Xe không xác định";
                            }

                            // 2. CẤU HÌNH GỬI MAIL THẬT QUA SMTP CỦA GOOGLE
                            using (var message = new MailMessage())
                            {
                                message.From = new MailAddress("tyuvip8@gmail.com", "Nhà Xe Bus Ticket Limo");
                                message.To.Add(new MailAddress(emailKhach));
                                message.Subject = $"[Bus Ticket] Vé vừa đặt thành công - Mã {bookingCode}";

                                // Giao diện nội dung Email HTML mô phỏng chuẩn giao diện popup của bạn
                                message.Body = $@"
                                    <div style='font-family: Arial, sans-serif; max-width: 500px; border: 1px solid #ffc107; padding: 20px; border-radius: 10px; background-color: #1a1a1a; color: #ffffff;'>
                                        <h2 style='text-align: center; color: #ffc107;'>Vé vừa đặt thành công</h2>
                                        <hr style='border-color: #333;'/>
                                        <p><b>Mã vé:</b> <span style='color: #ffc107; font-size: 16px;'>{bookingCode}</span></p>
                                        <p><b>Thông tin xe:</b>Mã: {busInfo}</p>
                                        <p><b>Giờ khởi hành:</b> {booking.BookingTime.ToLocalTime():dd/MM/yyyy HH:mm}</p>
                                        <p><b>Khách hàng:</b> {tenKhach}</p>
                                        <p><b>Ghế:</b> {gheDaDat}</p>
                                        <p style='font-size: 18px;'><b>Tổng tiền:</b> <span style='color: #ffc107;'>{booking.FinalAmount:N0} đ</span></p>
                                        <hr style='border-color: #333;'/>
                                        <p style='text-align: center; font-style: italic; color: #aaa;'>Chúc quý khách có một chuyến đi thượng lộ bình an!</p>
                                    </div>";
                                message.IsBodyHtml = true;

                                using (var client = new SmtpClient("smtp.gmail.com", 587))
                                {
                                    client.EnableSsl = true;
                                    client.UseDefaultCredentials = false;

                                    // Thay bằng thông tin thật của bạn để kích hoạt bắn mail đi
                                    client.Credentials =
                                        new System.Net.NetworkCredential("tyuvip8@gmail.com", "ahcm pdot boph kbqq");

                                    await client.SendMailAsync(message);
                                }
                            }

                            Console.WriteLine(
                                $" ✅ [RabbitMQ] ĐÃ GỬI EMAIL  kèm thông tin vé đến hòm thư: {emailKhach}!");
                        }
                        else
                        {
                            Console.WriteLine(
                                $" ⚠️ Không thấy thông tin trong MongoDB hoặc đơn không đăng ký email cho mã: {bookingCode}");
                        }
                    }

                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" ❌ Lỗi khi gửi mail: {ex.Message}");
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            await _channel.BasicConsumeAsync(queue: "order_queue", autoAck: false, consumer: consumer);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        public override void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }
}