using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Bus_ticket.Data;
using Bus_ticket.Helpers;
using Bus_ticket.Models;
using MongoDB.Bson.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace Bus_ticket.Controllers
{
    [Authorize(Roles = "Admin,Employee")]
    public class BookingController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IConfiguration _config;

        public BookingController(ApplicationDbContext dbContext, IConfiguration config)
        {
            _dbContext = dbContext;
            _config = config;
        }


        // GET: /Booking
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, string searchDate = "")
        {
            int pageSize = 5;
            if (page < 1) page = 1;

            var filterBuilder = Builders<Trip>.Filter;
            var filter = filterBuilder.Empty;

            if (!string.IsNullOrEmpty(searchDate) && DateTime.TryParse(searchDate, out DateTime parsedDate))
            {
                var startOfDay = parsedDate.Date.ToUniversalTime();
                var endOfDay = parsedDate.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

                filter = filterBuilder.And(
                    filterBuilder.Gte(t => t.DepartureTime, startOfDay),
                    filterBuilder.Lte(t => t.DepartureTime, endOfDay)
                );
            }

            long totalTrips = await _dbContext.Trips.CountDocumentsAsync(filter);
            int totalPages = (int)Math.Ceiling((double)totalTrips / pageSize);

            var trips = await _dbContext.Trips.Find(filter)
                .Sort(Builders<Trip>.Sort.Ascending(t => t.DepartureTime))
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            var buses = await _dbContext.Buses.Find(_ => true).ToListAsync();
            var routes = await _dbContext.BusRoutes.Find(_ => true).ToListAsync();

            ViewBag.BusList = buses;
            ViewBag.RouteList = routes;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.SearchDate = searchDate;

            return View(trips);
        }

        // GET: /Booking/GetManifest
        [HttpGet]
        public async Task<IActionResult> GetManifest(string tripId)
        {
            if (string.IsNullOrEmpty(tripId)) return BadRequest("Mã chuyến không hợp lệ.");

            var bookings = await _dbContext.Bookings
                .Find(b => b.TripId == tripId && b.BookingStatus == "Completed")
                .ToListAsync();

            var passengerList = bookings.SelectMany(b => b.Passengers.Select(p => new
            {
                seatNumber = p.SeatNumber,
                passengerName = p.Name,
                phone = p.PhoneNumber,
                email = p.Email,
                bookingCode = b.BookingCode,
                paymentStatus = b.PaymentStatus == "Paid" ? "Đã thanh toán" : "Chưa thanh toán"
            })).OrderBy(p => p.seatNumber).ToList();

            return Json(passengerList);
        }

        // GET: /Booking/Create (ĐÃ SỬA: Xóa bỏ thẻ [HttpGet] trùng lặp gây lỗi 404 ở đây)
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var trips = await _dbContext.Trips.Find(_ => true).ToListAsync();
            var buses = await _dbContext.Buses.Find(_ => true).ToListAsync();
            ViewBag.BusList = buses;

            return View(trips);
        }

        // GET: /Booking/GetLocationSuggestions?keyword=xxx
        [HttpGet]
        public async Task<IActionResult> GetLocationSuggestions(string keyword = "")
        {
            var allRoutes = await _dbContext.BusRoutes.Find(_ => true).ToListAsync();

            if (string.IsNullOrWhiteSpace(keyword))
            {
                var popularLocations = allRoutes.Select(r => r.DeparturePoint)
                    .Concat(allRoutes.Select(r => r.DestinationPoint))
                    .Where(loc => !string.IsNullOrEmpty(loc))
                    .GroupBy(loc => loc)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => new { name = g.Key, type = "Phổ biến" })
                    .ToList();

                return Json(popularLocations);
            }

            var searchKey = keyword.Trim().ToLower();

            var matchedLocations = allRoutes.Select(r => r.DeparturePoint)
                .Concat(allRoutes.Select(r => r.DestinationPoint))
                .Where(loc => !string.IsNullOrEmpty(loc) && loc.ToLower().Contains(searchKey))
                .Distinct()
                .Select(loc => new
                {
                    name = loc,
                    type = (loc.Contains("Quận") || loc.Contains("Huyện") || loc.Contains(","))
                        ? "Quận / Huyện"
                        : "Tỉnh / Thành phố"
                })
                .Take(10)
                .ToList();

            return Json(matchedLocations);
        }

        // GET: /Booking/SearchTrips
        [HttpGet]
        public async Task<IActionResult> SearchTrips(string departure, string destination, string date)
        {
            if (string.IsNullOrEmpty(departure) || string.IsNullOrEmpty(destination) || string.IsNullOrEmpty(date))
            {
                return BadRequest("Thiếu thông tin tìm kiếm.");
            }

            var depKey = departure.Trim().ToLower();
            var destKey = destination.Trim().ToLower();

            var matchedRoute = await _dbContext.BusRoutes
                .Find(r => r.DeparturePoint.ToLower().Contains(depKey) &&
                           r.DestinationPoint.ToLower().Contains(destKey))
                .FirstOrDefaultAsync();

            if (matchedRoute == null) return Json(new List<object>());

            if (!DateTime.TryParse(date, out DateTime parsedDate)) return BadRequest("Định dạng ngày không hợp lệ.");
            var startOfDay = parsedDate.Date.ToUniversalTime();
            var endOfDay = parsedDate.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

            var trips = await _dbContext.Trips
                .Find(t => t.RouteId == matchedRoute.Id && t.DepartureTime >= startOfDay && t.DepartureTime <= endOfDay)
                .ToListAsync();

            var buses = await _dbContext.Buses.Find(_ => true).ToListAsync();
            var tripIds = trips.Select(t => t.Id).ToList();

            var activeBookings = await _dbContext.Bookings
                .Find(b => tripIds.Contains(b.TripId) && b.BookingStatus == "Completed")
                .ToListAsync();

            var result = trips.Select(t =>
            {
                var bus = buses.FirstOrDefault(b => b.Id == t.BusId);
                var bookedSeatsCount = activeBookings
                    .Where(b => b.TripId == t.Id)
                    .Sum(b => b.Passengers?.Count ?? 0);

                // Đồng bộ 45 chỗ thay vì cứng 40 chỗ để khớp hoàn toàn với Ma trận sơ đồ ghế
                int totalSeats = 45;
                int availableSeats = totalSeats - bookedSeatsCount;

                return new
                {
                    id = t.Id,
                    departureTime = t.DepartureTime.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss"),
                    baseFare = t.BaseFare,
                    busCode = bus?.BusCode ?? "Mã xe ẩn",
                    busType = bus?.LegacyBusType ?? "Ghế ngồi",
                    availableSeats = availableSeats < 0 ? 0 : availableSeats
                };
            }).ToList();

            return Json(result);
        }

        // GET: /Booking/GetTripSeatMap?tripId=xxx
        [HttpGet]
        public async Task<IActionResult> GetTripSeatMap(string tripId)
        {
            if (string.IsNullOrEmpty(tripId)) return BadRequest("Mã không hợp lệ.");

            // 1. Lấy Trip
            var trip = await _dbContext.Trips.Find(t => t.Id == tripId).FirstOrDefaultAsync();
            if (trip == null) return NotFound("Không tìm thấy chuyến xe.");

            // 2. Lấy Bus (Xe) từ Trip (Giả sử Trip có thuộc tính BusId)
            var bus = await _dbContext.Buses.Find(b => b.Id == trip.BusId).FirstOrDefaultAsync();
            if (bus == null) return NotFound("Không tìm thấy thông tin xe.");

            // 3. Lấy BusClass (Cấu hình) từ Bus (Giả sử Bus có thuộc tính BusClassId)
            var busClass = await _dbContext.BusClasses.Find(bc => bc.Id == bus.BusClassId).FirstOrDefaultAsync();
            if (busClass == null) return NotFound("Không tìm thấy cấu hình loại xe.");

            // 4. Lấy danh sách ghế đã đặt
            var activeBookings = await _dbContext.Bookings
                .Find(b => b.TripId == tripId && b.BookingStatus == "Completed")
                .ToListAsync();

            var bookedSeats = activeBookings
                .SelectMany(b => b.Passengers ?? new List<PassengerDetail>())
                .Select(p => p.SeatNumber.Trim().ToUpper())
                .ToHashSet();

            // 5. Tạo sơ đồ từ thông tin của busClass
            var seatsList = new List<object>();
            for (int i = 1; i <= busClass.TotalSeats; i++)
            {
                string seatName = $"A{i:D2}";
                seatsList.Add(new
                {
                    seatNumber = seatName,
                    isBooked = bookedSeats.Contains(seatName)
                });
            }

            // 6. Trả về đúng cấu hình số cột từ busClass
            return Json(new
            {
                cols = busClass.TotalColumns,
                seats = seatsList
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookTicket(string tripId, List<string> seatNumbers, string passengerName,
            DateTime dob, string passengerPhone, string passengerEmail,
            string paymentMethod) // Thêm tham số paymentMethod
        {
            if (seatNumbers == null || !seatNumbers.Any() || string.IsNullOrEmpty(tripId))
            {
                return Json(new { success = false, message = "Dữ liệu không hợp lệ!" });
            }

            try
            {
                var trip = await _dbContext.Trips.Find(t => t.Id == tripId).FirstOrDefaultAsync();
                if (trip == null) return Json(new { success = false, message = "Chuyến không tồn tại." });

                var config = await _dbContext.SystemConfigs.Find(s => s.Id == "global_system_configuration")
                    .FirstOrDefaultAsync();
                int age = DateTime.UtcNow.Year - dob.Year;
                if (dob.Date > DateTime.UtcNow.AddYears(-age)) age--;
                decimal discountPercentage = config?.AgeDiscountRules
                    .FirstOrDefault(r => age >= r.MinAge && age <= r.MaxAge)?.DiscountPercentage ?? 0;

                decimal basePrice = trip.BaseFare;
                decimal discountPerSeat = basePrice * (discountPercentage / 100m);
                decimal finalPerSeat = basePrice - discountPerSeat;
                var bookingCode = "BK-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
                decimal totalFinal = finalPerSeat * seatNumbers.Count;
                // XỬ LÝ THANH TOÁN
                if (paymentMethod == "VNPAY")
                {
                    // 1. Lưu tạm dữ liệu vào Session để sau khi thanh toán xong mới dùng
                    var pendingData = new
                        { tripId, seatNumbers, passengerName, dob, passengerPhone, passengerEmail, finalPerSeat };
                    HttpContext.Session.SetString("PendingBooking", JsonConvert.SerializeObject(pendingData));
                    long amount = (long)(totalFinal * 100);
                    // 2. Tạo link VNPay
                    var vnpay = new VnPayLibrary();
                    SortedList<string, string> vnpayData = new SortedList<string, string>(new VnPayCompare());
                    vnpay.AddRequestData("vnp_Amount", amount.ToString());
                    vnpay.AddRequestData("vnp_Command", "pay");
                    vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
                    vnpay.AddRequestData("vnp_CurrCode", "VND");
                    vnpay.AddRequestData("vnp_IpAddr", "127.0.0.1");
                    vnpay.AddRequestData("vnp_Locale", "vn");
                    vnpay.AddRequestData("vnp_OrderInfo", "ThanhToanVeXe" + new string(bookingCode.Where(char.IsLetterOrDigit).ToArray()));
                    vnpay.AddRequestData("vnp_ReturnUrl", _config["VnPay:ReturnUrl"]);
                    vnpay.AddRequestData("vnp_TmnCode", _config["VnPay:TmnCode"]);
                    vnpay.AddRequestData("vnp_TxnRef", new string(bookingCode.Where(char.IsLetterOrDigit).ToArray()));
                    vnpay.AddRequestData("vnp_Version", "2.1.0");

                    string paymentUrl = vnpay.CreateRequestUrl(_config["VnPay:BaseUrl"], _config["VnPay:HashSecret"].Trim());
                    return Json(new { success = true, isRedirect = true, paymentUrl = paymentUrl });
                }

                // MẶC ĐỊNH LÀ CASH (Logic cũ của bạn)
                var newBooking = new Booking
                {
                    BookingCode = bookingCode,
                    TripId = trip.Id,
                    CustomerId = await ResolveCustomerIdAsync(passengerName, dob, passengerPhone, passengerEmail),
                    CustomerPhone = passengerPhone,
                    CustomerEmail = passengerEmail,
                    UserId = User.Identity?.IsAuthenticated == true
                        ? User.FindFirstValue(ClaimTypes.NameIdentifier)
                        : "GUEST",
                    BranchId =
                        (await _dbContext.Users.Find(u => u.Username == User.Identity.Name).FirstOrDefaultAsync())
                        ?.BranchId,
                    BookingTime = DateTime.UtcNow,
                    TotalPrice = basePrice * seatNumbers.Count,
                    DiscountAmount = discountPerSeat * seatNumbers.Count,
                    FinalAmount = totalFinal,
                    BookingStatus = "Completed",
                    PaymentStatus = "Paid",
                    Passengers = seatNumbers.Select(s => new PassengerDetail
                        { SeatNumber = s, Name = passengerName, Dob = dob, FinalSeatPrice = finalPerSeat }).ToList(),
                    Payment = new PaymentInfo { PaymentMethod = "Cash", AmountPaid = totalFinal },
                    CreatedAt = DateTime.UtcNow
                };

                await _dbContext.Bookings.InsertOneAsync(newBooking);
                foreach (var seat in seatNumbers)
                {
                    await _dbContext.Trips.UpdateOneAsync(t => t.Id == tripId,
                        Builders<Trip>.Update.Push(t => t.RealtimeSeats,
                            new RealtimeSeat { SeatNumber = seat, Status = "Booked" }));
                }

                TempData["NewBookingCode"] = newBooking.BookingCode;
                return Json(new { success = true, redirectUrl = Url.Action("Index", "Booking") });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmPayment(IQueryCollection collections)
        {
            var vnpay = new VnPayLibrary();
            var vnpayData = new SortedList<string, string>(new VnPayCompare());
            foreach (var key in collections.Keys)
            {
                if (key != "vnp_SecureHash")
                    vnpayData.Add(key, collections[key]);
            }

            vnpayData.Add("vnp_SecureHash", collections["vnp_SecureHash"]);

            if (vnpay.ValidateSignature(vnpayData, _config["VnPay:HashSecret"]) &&
                collections["vnp_ResponseCode"] == "00")
            {
                var pendingJson = HttpContext.Session.GetString("PendingBooking");
                if (string.IsNullOrEmpty(pendingJson)) return Content("Phiên thanh toán đã hết hạn!");

                // Sửa 1: Đã dùng DTO thì không cần dynamic
                var data = JsonConvert.DeserializeObject<PendingBookingDTO>(pendingJson);

                
                var seatNumbers = data.seatNumbers;
                var finalPerSeat = data.finalPerSeat;

                var newBooking = new Booking
                {
                    BookingCode = collections["vnp_TxnRef"],
                    TripId = data.tripId,
                    CustomerPhone = data.passengerPhone,
                    CustomerEmail = data.passengerEmail,
                    BookingTime = DateTime.UtcNow,
                    FinalAmount = finalPerSeat * seatNumbers.Count,
                    BookingStatus = "Completed",
                    PaymentStatus = "Paid",
                    Passengers = seatNumbers.Select(s => new PassengerDetail
                    {
                        SeatNumber = s,
                        Name = data.passengerName,
                        Dob = data.dob,
                        FinalSeatPrice = finalPerSeat
                    }).ToList(),
                    Payment = new PaymentInfo
                        { PaymentMethod = "VnPay", AmountPaid = finalPerSeat * seatNumbers.Count },
                    CreatedAt = DateTime.UtcNow
                };

                await _dbContext.Bookings.InsertOneAsync(newBooking);

                // Sửa 3: Truy cập data.tripId trực tiếp, không ép kiểu
                foreach (var seat in seatNumbers)
                {
                    await _dbContext.Trips.UpdateOneAsync(
                        t => t.Id == data.tripId,
                        Builders<Trip>.Update.Push(t => t.RealtimeSeats, new RealtimeSeat
                        {
                            SeatNumber = seat,
                            Status = "Booked"
                        })
                    );
                }

                TempData["NewBookingCode"] = newBooking.BookingCode;
                return RedirectToAction("Index", "Booking");
            }

            return Content("Thanh toán thất bại!");
        }

        public class PendingBookingDTO
        {
            public string tripId { get; set; }
            public List<string> seatNumbers { get; set; }
            public string passengerName { get; set; }
            public DateTime dob { get; set; }
            public string passengerPhone { get; set; }
            public string passengerEmail { get; set; }
            public decimal finalPerSeat { get; set; }
            public decimal finalAmount { get; set; }
        }

        private Booking CreateBookingInstance(string bookingCode, Trip trip, List<string> seatNumbers,
            string passengerName, DateTime dob, string passengerPhone, string passengerEmail,
            string paymentMethod, string paymentStatus, decimal finalPerSeat, decimal totalFinal)
        {
            // Lấy UserId và BranchId tương tự logic gốc
            var userId = User.Identity?.IsAuthenticated == true
                ? User.FindFirstValue(ClaimTypes.NameIdentifier)
                : "GUEST";
            // Nếu bạn muốn lấy BranchId giống hệt logic cũ:
            // var branchId = _dbContext.Users.Find(u => u.Username == User.Identity.Name).FirstOrDefault()?.BranchId;

            return new Booking
            {
                BookingCode = bookingCode,
                TripId = trip.Id,
                // Giữ lại logic ResolveCustomerIdAsync nếu cần
                CustomerId =
                    "GUEST_USER", // Bạn có thể thay bằng hàm: ResolveCustomerIdAsync(passengerName, dob, passengerPhone, passengerEmail).GetAwaiter().GetResult()
                CustomerPhone = passengerPhone,
                CustomerEmail = passengerEmail,
                UserId = userId,
                BookingTime = DateTime.UtcNow,
                TotalPrice = trip.BaseFare * seatNumbers.Count,
                DiscountAmount = (trip.BaseFare - finalPerSeat) * seatNumbers.Count,
                FinalAmount = totalFinal,
                BookingStatus = "Completed",
                PaymentStatus = paymentStatus,
                Passengers = seatNumbers.Select(s => new PassengerDetail
                {
                    SeatNumber = s,
                    Name = passengerName,
                    Dob = dob,
                    FinalSeatPrice = finalPerSeat
                }).ToList(),
                Payment = new PaymentInfo { PaymentMethod = paymentMethod, AmountPaid = totalFinal },
                CreatedAt = DateTime.UtcNow
            };
        }

        private async Task SaveBookingToDb(Booking booking, string tripId, List<string> seatNumbers)
        {
            // Lưu booking
            await _dbContext.Bookings.InsertOneAsync(booking);

            // Cập nhật RealtimeSeats cho Trip
            foreach (var seat in seatNumbers)
            {
                var update = Builders<Trip>.Update.Push(t => t.RealtimeSeats, new RealtimeSeat
                {
                    SeatNumber = seat,
                    Status = "Booked"
                });
                await _dbContext.Trips.UpdateOneAsync(t => t.Id == tripId, update);
            }
        }

        public async Task<IActionResult> GetBookingDetails(string code)
        {
            var booking = await _dbContext.Bookings.Find(b => b.BookingCode == code).FirstOrDefaultAsync();
            if (booking == null) return Json(new { success = false });

            var trip = await _dbContext.Trips.Find(t => t.Id == booking.TripId).FirstOrDefaultAsync();
            var bus = await _dbContext.Buses.Find(b => b.Id == trip.BusId).FirstOrDefaultAsync();

            return Json(new
            {
                success = true,
                bookingCode = booking.BookingCode, // Mã vé
                busInfo = bus != null
                    ? $"{bus.BusCode} - {bus.LicensePlate}"
                    : "Xe không xác định", // Đầy đủ BusCode và Biển số
                departureTime = trip.DepartureTime.ToString("HH:mm dd/MM/yyyy"), // Giờ khởi hành
                passengerName = booking.Passengers.FirstOrDefault()?.Name, // Tên khách
                seats = string.Join(", ", booking.Passengers.Select(p => p.SeatNumber)), // Danh sách ghế
                finalTotal = booking.FinalAmount.ToString("N0") + " đ" // Tổng tiền
            });
        }

        private async Task<string> ResolveCustomerIdAsync(string fullName, DateTime dob, string phone, string email)
        {
            Customer? existing = null;

            // Tìm theo số điện thoại trước, sau đó tới email
            if (!string.IsNullOrEmpty(phone))
            {
                existing = await _dbContext.Customers
                    .Find(c => c.PhoneNumber == phone)
                    .FirstOrDefaultAsync();
            }

            if (existing == null && !string.IsNullOrEmpty(email))
            {
                existing = await _dbContext.Customers
                    .Find(c => c.Email == email)
                    .FirstOrDefaultAsync();
            }

            // Nếu đã tồn tại, kiểm tra xem có cần cập nhật thông tin không
            if (existing != null)
            {
                var updates = new List<UpdateDefinition<Customer>>();

                if (!string.IsNullOrEmpty(fullName) && existing.FullName != fullName)
                    updates.Add(Builders<Customer>.Update.Set(c => c.FullName, fullName));

                if (!string.IsNullOrEmpty(email) && existing.Email != email)
                    updates.Add(Builders<Customer>.Update.Set(c => c.Email, email));

                if (!string.IsNullOrEmpty(phone) && existing.PhoneNumber != phone)
                    updates.Add(Builders<Customer>.Update.Set(c => c.PhoneNumber, phone));

                // Cập nhật ngày sinh nếu khác
                if (existing.Dob != dob)
                    updates.Add(Builders<Customer>.Update.Set(c => c.Dob, dob));

                if (updates.Count > 0)
                {
                    updates.Add(Builders<Customer>.Update.Set(c => c.UpdatedAt, DateTime.UtcNow));
                    updates.Add(Builders<Customer>.Update.Set(c => c.UpdatedBy, "Booking-Counter"));

                    await _dbContext.Customers.UpdateOneAsync(
                        c => c.Id == existing.Id,
                        Builders<Customer>.Update.Combine(updates));
                }

                return existing.Id;
            }

            // Nếu chưa tồn tại, tạo mới
            var customer = new Customer
            {
                CustomerCode = "KH-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper(),
                FullName = fullName,
                Dob = dob, // Lưu ngày sinh thật
                Gender = "Khác",
                PhoneNumber = phone,
                Email = email,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Booking-Counter",
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = "Booking-Counter"
            };

            await _dbContext.Customers.InsertOneAsync(customer);
            return customer.Id;
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomerByPhone(string phone)
        {
            var customer = await _dbContext.Customers.AsQueryable()
                .FirstOrDefaultAsync(c => c.PhoneNumber == phone);
            if (customer == null) return NotFound();

            return Json(new
            {
                fullName = customer.FullName,
                email = customer.Email,
                dob = customer.Dob
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetDiscountRules()
        {
            var config = await _dbContext.SystemConfigs.AsQueryable()
                .FirstOrDefaultAsync(c => c.Id == "global_system_configuration");
            if (config == null) return Ok(new { rules = new List<AgeDiscountRule>() });

            return Ok(new { rules = config.AgeDiscountRules });
        }
    }
}