using Bus_ticket.Data;
using Bus_ticket.Models;
using Microsoft.AspNetCore.Authorization;
using Bus_ticket.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Net.Http;
using System;
using System.Linq;
using System.Threading.Tasks; // 🎯 Đã thêm thư viện này để dùng async Task

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
            return RedirectToAction("Index", "Dashboard");
        }

        [HttpGet]
        public IActionResult RenderDropdownDatePicker(string targetType, string currentFromDate = "", string currentToDate = "")
        {
            ViewBag.TargetType = targetType;
            ViewBag.CurrentFromDate = currentFromDate;
            ViewBag.CurrentToDate = currentToDate;

            return PartialView("_DropdownDatePicker");
        }
        
        [HttpPost]
        public IActionResult CrawlNews([FromServices] CrawlerProducer crawlerProducer)
        {
            // Gọi Producer chạy ngầm đẩy link vào Queue (Không bắt web phải đứng chờ)
            _ = Task.Run(() => crawlerProducer.StartCrawlingAsync());

            // Trả về thông báo luôn cho Admin đỡ sốt ruột
            TempData["SuccessMessage"] = "Đã gửi lệnh thu thập tin tức! Dữ liệu đang được cào ngầm bằng RabbitMQ, vui lòng tải lại trang sau ít phút để xem bài mới.";
            
            return RedirectToAction("ManageNews");
        }
        
        public IActionResult ManageNews()
        {
            var newsList = _dbContext.News.Find(_ => true).ToList()
                                          .OrderByDescending(n => n.CreatedDate)
                                          .ToList();
            return View(newsList);
        }
        
        // ====================================================================
        // 🎯 ĐÃ THÊM: HÀM DUYỆT TIN TỨC (STATUS = 1)
        // ====================================================================
        [HttpPost]
        public async Task<IActionResult> ApproveNews(string id)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest();

            try
            {
                var filter = Builders<News>.Filter.Eq(n => n.Id, id);
                var update = Builders<News>.Update.Set(n => n.Status, 1);
                
                await _dbContext.News.UpdateOneAsync(filter, update);
                
                TempData["SuccessMessage"] = "Đã duyệt bài viết! Bài này sẽ lập tức hiển thị trên trang chủ.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi duyệt bài: " + ex.Message;
            }

            return RedirectToAction("ManageNews");
        }

        // ====================================================================
        // 🎯 ĐÃ THÊM: HÀM XÓA BÀI VIẾT RÁC
        // ====================================================================
        [HttpPost]
        public async Task<IActionResult> DeleteNews(string id)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest();

            try
            {
                var filter = Builders<News>.Filter.Eq(n => n.Id, id);
                await _dbContext.News.DeleteOneAsync(filter);
                
                TempData["SuccessMessage"] = "Đã xóa bản tin rác thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi xóa: " + ex.Message;
            }

            return RedirectToAction("ManageNews");
        }
        // ====================================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string licensePlate, string vehicleClass, string route, decimal distanceKm)
        {
            if (string.IsNullOrWhiteSpace(licensePlate) || string.IsNullOrWhiteSpace(vehicleClass) || string.IsNullOrWhiteSpace(route))
            {
                ModelState.AddModelError(string.Empty, "Biển số, lớp xe và tuyến đường là bắt buộc.");
                return View();
            }

            var bus = new Bus
            {
                BusCode = GenerateBusCode(),
                LicensePlate = licensePlate,
                BranchId = DataSeeder.BranchHanoiId,
                BusClassId = DataSeeder.BusClassExpress45Id,
                Status = "Active",
                CreatedBy = User.Identity?.Name ?? "Admin",
                UpdatedBy = User.Identity?.Name ?? "Admin"
            };

            await _dbContext.Buses.InsertOneAsync(bus);

            return RedirectToAction("Index");
        }

        private static string GenerateBusCode()
        {
            return new Random().Next(10000, 99999).ToString();
        }
        
        [HttpGet]
        public async Task<IActionResult> PriceConfig()
        {
            // Lấy thẳng danh sách giá độc lập, cực nhẹ và sạch code
            var priceList = await _dbContext.PriceConfigs.Find(_ => true).ToListAsync();
            return View(priceList);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePriceConfig(string busType, string departurePoint, string destinationPoint, decimal basePrice)
        {
            if (string.IsNullOrEmpty(busType) || string.IsNullOrEmpty(departurePoint) || string.IsNullOrEmpty(destinationPoint) || basePrice <= 0)
            {
                TempData["ErrorMessage"] = "Dữ liệu cấu hình giá vé không hợp lệ!";
                return RedirectToAction(nameof(PriceConfig));
            }

            try
            {
                // Bộ lọc tìm kiếm: Khớp chính xác bộ ba: Loại xe + Điểm đi + Điểm đến
                var filter = Builders<PriceConfig>.Filter.And(
                    Builders<PriceConfig>.Filter.Eq(p => p.BusType, busType),
                    Builders<PriceConfig>.Filter.Eq(p => p.DeparturePoint, departurePoint),
                    Builders<PriceConfig>.Filter.Eq(p => p.DestinationPoint, destinationPoint)
                );

                var update = Builders<PriceConfig>.Update
                    .Set(p => p.BasePrice, basePrice)
                    .Set(p => p.UpdatedAt, DateTime.UtcNow);

                var options = new UpdateOptions { IsUpsert = true };
                await _dbContext.PriceConfigs.UpdateOneAsync(filter, update, options);
                
                TempData["SuccessMessage"] = "Cập nhật cấu hình giá vé nền thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi xử lý database MongoDB: " + ex.Message;
            }

            return RedirectToAction(nameof(PriceConfig));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePriceConfig(string id)
        {
            if (string.IsNullOrEmpty(id)) return BadRequest();

            try
            {
                await _dbContext.PriceConfigs.DeleteOneAsync(p => p.Id == id);
                TempData["SuccessMessage"] = "Xóa cấu hình giá vé thành công!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi thực hiện xóa dữ liệu: " + ex.Message;
            }

            return RedirectToAction(nameof(PriceConfig));
        }
    }
}