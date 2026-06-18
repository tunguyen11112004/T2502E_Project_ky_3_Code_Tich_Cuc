using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bus_ticket.Data;
using Bus_ticket.Models; 

namespace T2502E_Project_ky_3_Code_Tich_Cuc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class BookingController : Controller
    {
        private readonly ApplicationDbContext _dbContext; 

        // Inject DbContext để làm việc với MongoDB
        public BookingController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // =======================================================
        // GET: /Booking
        // Hiển thị giao diện danh sách chuyến xe thực tế từ cơ sở dữ liệu
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var trips = await _dbContext.Trips.Find(_ => true).ToListAsync();
            return View(trips);
        }

        // =======================================================
        // GET: /Booking/GetSeats?tripId=xxx
        // API kết hợp Trip và Bus để Frontend vẽ sơ đồ
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> GetSeats(string tripId)
        {
            if (string.IsNullOrEmpty(tripId)) return BadRequest("Mã chuyến xe không hợp lệ.");

            var trip = await _dbContext.Trips.Find(t => t.Id == tripId).FirstOrDefaultAsync();
            if (trip == null) return NotFound("Không tìm thấy chuyến xe.");

            var bus = await _dbContext.Buses.Find(b => b.Id == trip.BusId).FirstOrDefaultAsync();
            if (bus == null) return NotFound("Không tìm thấy thông tin xe tương ứng với chuyến này.");

            var realtimeSeatsList = trip.RealtimeSeats ?? new List<RealtimeSeat>();
            var detailedSeats = new List<object>();

            if (realtimeSeatsList.Any())
            {
                int index = 1;
                foreach (var rSeat in realtimeSeatsList)
                {
                    detailedSeats.Add(new
                    {
                        seatNumber = rSeat.SeatNumber,
                        row = (index - 1) / 5 + 1,
                        column = (index - 1) % 5 + 1,
                        floor = 1,
                        seatType = "Standard",
                        status = rSeat.Status ?? "Available"
                    });
                    index++;
                }
            }
            else
            {
                for (int i = 1; i <= 40; i++)
                {
                    string seatName = (i < 10) ? $"A0{i}" : $"A{i}";
                    detailedSeats.Add(new
                    {
                        seatNumber = seatName,
                        row = (i - 1) / 5 + 1,
                        column = (i - 1) % 5 + 1,
                        floor = 1,
                        seatType = "Standard",
                        status = "Available"
                    });
                }
            }

            return Json(new { 
                baseFare = trip.BaseFare, 
                busType = bus.BusType ?? "Standard Layout (40 Seats)",
                totalFloors = 1,
                totalColumns = 5,
                realtimeSeats = detailedSeats,
                seats = detailedSeats 
            });
        }

        // =======================================================
        // POST: /Booking/BookTicket
        // ĐÃ SỬA: Tách đơn hàng độc lập - Mỗi ghế được chọn sinh ra 1 Document riêng biệt
        // =======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookTicket(string tripId, List<string> seatNumbers, string passengerName, int passengerAge, string passengerPhone, string passengerEmail)
        {
            if (seatNumbers == null || seatNumbers.Count == 0 || string.IsNullOrEmpty(tripId))
            {
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ! Vui lòng tích chọn ít nhất một vị trí ghế.";
                return RedirectToAction("Index");
            }

            try
            {
                var trip = await _dbContext.Trips.Find(t => t.Id == tripId).FirstOrDefaultAsync();
                if (trip == null)
                {
                    TempData["ErrorMessage"] = "Chuyến xe không tồn tại hoặc đã bị hủy.";
                    return RedirectToAction("Index");
                }

                var realtimeSeatsList = trip.RealtimeSeats ?? new List<RealtimeSeat>();

                // 1. Kiểm tra trùng toàn bộ danh sách ghế xem có ghế nào đã bị đặt trước đó chưa
                var unavailableSeats = realtimeSeatsList
                    .Where(s => seatNumbers.Contains(s.SeatNumber) && s.Status != "Available")
                    .Select(s => s.SeatNumber)
                    .ToList();

                if (unavailableSeats.Any())
                {
                    TempData["ErrorMessage"] = $"Lỗi dính ghế: Vị trí ({string.Join(", ", unavailableSeats)}) đã có người đặt trước!";
                    return RedirectToAction("Index");
                }

                string finalCustomerName = string.IsNullOrWhiteSpace(passengerName) ? "Khách mua tại quầy" : passengerName.Trim();
                decimal pricePerSeat = trip.BaseFare;

                // Tính toán giảm giá chung theo tuổi
                decimal seatDiscount = 0m;
                if (passengerAge < 12 || passengerAge > 60)
                {
                    seatDiscount = pricePerSeat * 0.1m; 
                }
                decimal singleSeatTax = (pricePerSeat - seatDiscount) * 0.1m;
                decimal singleSeatFinalAmount = pricePerSeat - seatDiscount + singleSeatTax;

                // 2. VÒNG LẶP CHÍ MẠNG: Duyệt qua từng ghế để sinh đơn hàng độc lập
                foreach (var seat in seatNumbers)
                {
                    // Tạo mảng hành khách con chỉ chứa đúng 1 ghế này
                    var singlePassengerList = new List<PassengerDetail>
                    {
                        new PassengerDetail
                        {
                            SeatNumber = seat,
                            Name = finalCustomerName,
                            Dob = DateTime.UtcNow.AddYears(-passengerAge),
                            FinalSeatPrice = pricePerSeat - seatDiscount
                        }
                    };

                    // Khởi tạo một document Booking riêng biệt cho từng ghế
                    var newBooking = new Booking
                    {
                        BookingCode = "BK-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper(), // Sinh mã đơn ngẫu nhiên không trùng lặp
                        TripId = trip.Id, 
                        CustomerId = "65fe9876543210fedcba4321", 
                        UserId = "6a33e2acda7e37484733da51",     
                        BranchId = "65fe1234567890abcdef1234",   
                        BookingTime = DateTime.UtcNow,
                        TotalPrice = pricePerSeat,
                        TaxAmount = singleSeatTax,
                        DiscountAmount = seatDiscount,
                        FinalAmount = singleSeatFinalAmount,
                        BookingStatus = "Completed",
                        PaymentStatus = "Paid",
                        Passengers = singlePassengerList, 
                        Payment = new PaymentInfo
                        {
                            PaymentMethod = "Cash",
                            AmountPaid = singleSeatFinalAmount,
                            TransactionCode = "CASH-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper()
                        },
                        CreatedBy = "Admin-Counter",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    // 3. Tiến hành lưu trực tiếp document này vào MongoDB
                    await _dbContext.Bookings.InsertOneAsync(newBooking);

                    // 4. Cập nhật trạng thái ghế trong bảng Trips tương ứng
                    var checkSeatExist = realtimeSeatsList.FirstOrDefault(s => s.SeatNumber == seat);
                    if (checkSeatExist == null)
                    {
                        var newSeatObj = new RealtimeSeat { SeatNumber = seat, Status = "Booked" };
                        var pushUpdate = Builders<Trip>.Update.Push("RealtimeSeats", newSeatObj);
                        await _dbContext.Trips.UpdateOneAsync(t => t.Id == tripId, pushUpdate);
                    }
                    else
                    {
                        var updateFilter = Builders<Trip>.Filter.And(
                            Builders<Trip>.Filter.Eq(t => t.Id, tripId),
                            Builders<Trip>.Filter.Eq("RealtimeSeats.SeatNumber", seat)
                        );
                        var update = Builders<Trip>.Update.Set("RealtimeSeats.$.Status", "Booked");
                        await _dbContext.Trips.UpdateOneAsync(updateFilter, update);
                    }
                }
                
                TempData["SuccessMessage"] = $"Đặt thành công {seatNumbers.Count} đơn độc lập cho khách hàng {finalCustomerName} (Ghế: {string.Join(", ", seatNumbers)})!";
                return RedirectToAction("Index");
            }
            catch (Exception ex) {
                TempData["ErrorMessage"] = "Lỗi hệ thống MongoDB: " + ex.Message;
                return RedirectToAction("Index");
            }
        }
    }
}