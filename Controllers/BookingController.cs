using Bus_ticket.Data;
using Bus_ticket.Helpers;
using Bus_ticket.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Bus_ticket.Interfaces;
using PayOS;
using PayOS.Models;
using PayOS.Models.V2.PaymentRequests;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace Bus_ticket.Controllers
{
    [Authorize(Roles = "Admin,Employee")]
    public class BookingController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IConfiguration _config;
        private readonly IMomoService _momoService;
        private readonly IRabbitMQService _rabbitMqService;

        public BookingController(ApplicationDbContext dbContext, IConfiguration config, IMomoService momoService,
            IRabbitMQService rabbitMqService)
        {
            _dbContext = dbContext;
            _config = config;
            _momoService = momoService;
            _rabbitMqService = rabbitMqService;
        }

        // GET: /Booking
        [HttpGet]
        public async Task<IActionResult> Index(int page = 1, string searchDate = "")
        {
            // Nếu chưa chọn ngày (lần đầu load trang), tự động lấy ngày hiện tại (giờ Việt Nam GMT+7)
            if (string.IsNullOrEmpty(searchDate))
            {
                var localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                    TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));
                searchDate = localTime.ToString("yyyy-MM-dd");
            }

            // Gửi lại searchDate ra View để điền vào thẻ <input type="date">
            ViewBag.SearchDate = searchDate;

            int pageSize = 5;
            if (page < 1) page = 1;

            var filterBuilder = Builders<Trip>.Filter;
            var filter = TripFilters.NotDeleted;

            if (!string.IsNullOrEmpty(searchDate) && DateTime.TryParse(searchDate, out DateTime parsedDate))
            {
                var startOfDay = parsedDate.Date.ToUniversalTime();
                var endOfDay = parsedDate.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

                filter = filterBuilder.And(
                    TripFilters.NotDeleted,
                    filterBuilder.Gte(t => t.DepartureTime, startOfDay),
                    filterBuilder.Lte(t => t.DepartureTime, endOfDay));
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

            var busList = await _dbContext.Buses.Find(_ => true).ToListAsync();

            var operatorList = await _dbContext.BusOperators.Find(o => o.Status == "Active").ToListAsync();

            var busClassList = await _dbContext.BusClasses.Find(c => c.Status == "Active").ToListAsync();
            ViewBag.BusList = busList;
            ViewBag.RouteList = routes;
            ViewBag.operatorList = operatorList;
            ViewBag.busClassList = busClassList;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.SearchDate = searchDate;

            return View(trips);
        }

        // GET: /Booking/GetManifest
        [HttpGet]
        public async Task<IActionResult> GetManifest(string tripId)
        {
            if (string.IsNullOrEmpty(tripId))
            {
                return BadRequest("Mã chuyến không hợp lệ.");
            }

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

        // GET: /Booking/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var trips = await _dbContext.Trips.Find(TripFilters.NotDeleted).ToListAsync();
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
            if (string.IsNullOrWhiteSpace(departure)
                || string.IsNullOrWhiteSpace(destination)
                || string.IsNullOrWhiteSpace(date))
            {
                return BadRequest("Thiếu thông tin tìm kiếm.");
            }

            var depKey = departure.Trim().ToLower();
            var destKey = destination.Trim().ToLower();

            var allRoutes = await _dbContext.BusRoutes.Find(_ => true).ToListAsync();
            var busOperators = await _dbContext.BusOperators.Find(_ => true).ToListAsync();

            var matchedRoute = allRoutes.FirstOrDefault(r =>
                !string.IsNullOrWhiteSpace(r.DeparturePoint)
                && !string.IsNullOrWhiteSpace(r.DestinationPoint)
                && r.DeparturePoint.Trim().ToLower().Contains(depKey)
                && r.DestinationPoint.Trim().ToLower().Contains(destKey)
            );

            if (matchedRoute == null)
            {
                return Json(new List<object>());
            }

            if (!DateTime.TryParse(date, out DateTime parsedDate))
            {
                return BadRequest("Định dạng ngày không hợp lệ.");
            }

            var startOfDay = parsedDate.Date.ToUniversalTime();
            var endOfDay = parsedDate.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

            var trips = await _dbContext.Trips
                .Find(Builders<Trip>.Filter.And(
                    TripFilters.NotDeleted,
                    Builders<Trip>.Filter.Eq(t => t.RouteId, matchedRoute.Id),
                    Builders<Trip>.Filter.Gte(t => t.DepartureTime, startOfDay),
                    Builders<Trip>.Filter.Lte(t => t.DepartureTime, endOfDay),
                    Builders<Trip>.Filter.In(t => t.Status, new[] { "Scheduled", "Active" })))
                .ToListAsync();

            var buses = await _dbContext.Buses.Find(_ => true).ToListAsync();
            var busClasses = await _dbContext.BusClasses.Find(_ => true).ToListAsync();
            var priceConfigs = await _dbContext.PriceConfigs.Find(_ => true).ToListAsync();
            var routeMap = allRoutes.ToDictionary(r => r.Id, r => r);

            var tripIds = trips.Select(t => t.Id).ToList();

            var activeBookings = tripIds.Any()
                ? await _dbContext.Bookings
                    .Find(b => tripIds.Contains(b.TripId) && b.BookingStatus == "Completed")
                    .ToListAsync()
                : new List<Booking>();

            // GIẢI QUYẾT BẤT ĐỒNG BỘ (ASYNC): 
            // Do ReleaseExpiredHoldsAsync là tác vụ Async, ta giải phóng hàng giữ chỗ hết hạn cho toàn bộ các trip trước khi ánh xạ kết quả đồng bộ.
            foreach (var tripId in tripIds)
            {
                await ReleaseExpiredHoldsAsync(tripId);
            }

            var result = trips
                .OrderBy(t => t.DepartureTime)
                .Select(t =>
                {
                    var bus = buses.FirstOrDefault(b => b.Id == t.BusId);
                    var busClass = busClasses.FirstOrDefault(c => c.Id == bus?.BusClassId);
                    var route = routeMap.GetValueOrDefault(t.RouteId);
                    var busOperator = busOperators.FirstOrDefault(o => o.Id == bus?.OperatorId);
                    var operatorName = busOperator?.OperatorName ?? "Chưa xác định";

                    // Số lượng ghế đã được mua từ danh sách Booking hoàn thành
                    var bookedSeatsCount = activeBookings
                        .Where(b => b.TripId == t.Id)
                        .Sum(b => b.Passengers?.Count ?? 0);

                    int totalSeats = 45;
                    if (busClass != null && busClass.TotalSeats > 0)
                    {
                        totalSeats = busClass.TotalSeats;
                    }
                    else if (bus?.LegacyTotalSeats != null && bus.LegacyTotalSeats.Value > 0)
                    {
                        totalSeats = bus.LegacyTotalSeats.Value;
                    }
                    else if (t.RealtimeSeats?.Count > 0)
                    {
                        totalSeats = t.RealtimeSeats.Count;
                    }

                    // Tính số ghế còn trống (Có sẵn)
                    int availableSeats = totalSeats - bookedSeatsCount;
                    if (availableSeats < 0) availableSeats = 0;

                    decimal baseFare = t.BaseFare;
                    if (baseFare <= 0 && route != null)
                    {
                        var matchedPrice = priceConfigs.FirstOrDefault(p =>
                            p.DeparturePoint == route.DeparturePoint
                            && p.DestinationPoint == route.DestinationPoint
                            && BusTypeMatcher.Matches(p.BusType, busClass, bus));
                        if (matchedPrice != null)
                        {
                            baseFare = matchedPrice.BasePrice;
                        }
                    }

                    return new
                    {
                        id = t.Id,
                        tripCode = t.TripCode ?? string.Empty,
                        departureTime = t.DepartureTime.ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss"),
                        baseFare,
                        busCode = bus?.BusCode ?? "—",
                        licensePlate = bus?.LicensePlate ?? "—",
                        busType = busClass?.ClassName ?? bus?.LegacyBusType ?? "Ghế ngồi",
                        operatorName = operatorName,
                        totalSeats,
                        availableSeats, // Biến này bây giờ đã được khai báo và gán trị hợp lệ
                        departurePoint = route?.DeparturePoint ?? departure.Trim(),
                        destinationPoint = route?.DestinationPoint ?? destination.Trim()
                    };
                })
                .ToList();

            return Json(result);
        }

        // GET: /Booking/GetTripSeatMap?tripId=xxx
        [HttpGet]
        public async Task<IActionResult> GetTripSeatMap(string tripId)
        {
            if (string.IsNullOrEmpty(tripId))
            {
                return BadRequest(new { success = false, message = "Mã chuyến không hợp lệ." });
            }

            // 1. Lấy thông tin chuyến xe
            var trip = await _dbContext.Trips.Find(t => t.Id == tripId).FirstOrDefaultAsync();
            if (trip == null || trip.DeletedAt.HasValue)
            {
                return NotFound(new { success = false, message = "Không tìm thấy chuyến xe." });
            }

            // Giải phóng các ghế giữ chỗ quá hạn trước khi kiểm tra trạng thái sơ đồ
            await ReleaseExpiredHoldsAsync(tripId);
            // Lấy lại dữ liệu trip mới nhất sau khi giải phóng
            trip = await _dbContext.Trips.Find(t => t.Id == tripId).FirstOrDefaultAsync();

            // 2. Tìm thông tin Xe và Hạng Xe để lấy sơ đồ mẫu (Layout gốc)
            var bus = await _dbContext.Buses.Find(b => b.Id == trip.BusId).FirstOrDefaultAsync();
            var busClass = bus != null
                ? await _dbContext.BusClasses.Find(c => c.Id == bus.BusClassId).FirstOrDefaultAsync()
                : null;

            // Lấy danh sách layout ghế mẫu (ưu tiên từ BusClass, sau đó tới Legacy)
            List<SeatTemplate> defaultLayout = new List<SeatTemplate>();
            int totalCols = 4; // Mặc định hiển thị 4 cột giống phía View cấu hình

            if (busClass != null && busClass.DefaultLayout != null && busClass.DefaultLayout.Any())
            {
                defaultLayout = busClass.DefaultLayout;
                totalCols = busClass.TotalColumns > 0 ? busClass.TotalColumns : 4;
            }
            else if (bus?.SeatsLayout != null && bus.SeatsLayout.Any())
            {
                defaultLayout = bus.SeatsLayout;
            }

            // Nếu hệ thống chưa thiết lập layout mẫu, tự động gen danh sách ghế dựa theo số lượng ghế
            if (!defaultLayout.Any())
            {
                int totalSeats = busClass?.TotalSeats ?? bus?.LegacyTotalSeats ?? 45;
                for (int i = 1; i <= totalSeats; i++)
                {
                    defaultLayout.Add(new SeatTemplate { SeatNumber = $"A{i:00}" });
                }
            }

            // 3. Lấy danh sách trạng thái ghế thời gian thực (RealtimeSeats) trong Trip
            var realtimeSeats = trip.RealtimeSeats ?? new List<RealtimeSeat>();
            var now = DateTime.UtcNow;

            // 4. Ánh xạ trạng thái realtime vào layout ghế mẫu
            var seatMapResult = defaultLayout.Select(template =>
            {
                // Kiểm tra xem ghế mẫu này hiện tại trong Trip có trạng thái như thế nào
                var realtimeInfo = realtimeSeats.FirstOrDefault(s =>
                    s.SeatNumber != null &&
                    s.SeatNumber.Trim().Equals(template.SeatNumber.Trim(), StringComparison.OrdinalIgnoreCase));

                bool isBooked = realtimeInfo?.Status == "Booked";
                bool isHolding = realtimeInfo?.Status == "Holding" && realtimeInfo.HeldUntil.HasValue &&
                                 realtimeInfo.HeldUntil.Value > now;

                return new
                {
                    seatNumber = template.SeatNumber,
                    isBooked = isBooked,
                    isHolding = isHolding,
                    isLocked = isBooked || isHolding // Khoá ghế nếu đã thanh toán hoặc đang bị giữ chỗ
                };
            }).ToList();

            return Json(new
            {
                success = true,
                cols = totalCols,
                seats = seatMapResult
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BookTicket(
            string tripId,
            List<string> seatNumbers,
            string passengerName,
            DateTime dob,
            string passengerPhone,
            string passengerEmail,
            string paymentMethod)
        {
            if (seatNumbers == null || !seatNumbers.Any() || string.IsNullOrEmpty(tripId))
            {
                return Json(new { success = false, message = "Dữ liệu không hợp lệ!" });
            }

            var blockedCustomer = await _dbContext.Customers
                .Find(c => c.PhoneNumber == passengerPhone && c.IsBlocked)
                .FirstOrDefaultAsync();

            if (blockedCustomer != null)
            {
                return Json(new
                {
                    success = false,
                    message =
                        "Số điện thoại này đã bị khóa do quá 3 lần đặt vé nhưng không thanh toán. Vui lòng liên hệ quầy vé để được hỗ trợ."
                });
            }

            try
            {
                seatNumbers = NormalizeSeatNumbers(seatNumbers);

                if (!seatNumbers.Any())
                {
                    return Json(new { success = false, message = "Vui lòng chọn ghế hợp lệ." });
                }

                var trip = await _dbContext.Trips
                    .Find(t => t.Id == tripId)
                    .FirstOrDefaultAsync();

                if (trip == null || trip.DeletedAt.HasValue)
                {
                    return Json(new { success = false, message = "Chuyến không tồn tại hoặc đã bị xóa." });
                }

                var config = await _dbContext.SystemConfigs
                    .Find(s => s.Id == "global_system_configuration")
                    .FirstOrDefaultAsync();

                int age = DateTime.UtcNow.Year - dob.Year;
                if (dob.Date > DateTime.UtcNow.AddYears(-age))
                {
                    age--;
                }

                decimal discountPercentage = config?.AgeDiscountRules
                    .FirstOrDefault(r => age >= r.MinAge && age <= r.MaxAge)?.DiscountPercentage ?? 0;

                decimal basePrice = trip.BaseFare;
                decimal discountPerSeat = basePrice * (discountPercentage / 100m);
                decimal finalPerSeat = basePrice - discountPerSeat;
                decimal totalFinal = finalPerSeat * seatNumbers.Count;

                var bookingCode = "BK-" + Guid.NewGuid().ToString("N")[..8].ToUpper();

                if (paymentMethod == "VNPAY")
                {
                    var cleanBookingCode = new string(bookingCode.Where(char.IsLetterOrDigit).ToArray());

                    var holdResult = await TryHoldSeatsAsync(tripId, seatNumbers, cleanBookingCode);

                    if (!holdResult.Success)
                    {
                        return Json(new
                        {
                            success = false,
                            message = holdResult.Message
                        });
                    }

                    var pendingData = new PendingBookingDTO
                    {
                        tripId = tripId,
                        seatNumbers = seatNumbers,
                        passengerName = passengerName,
                        dob = dob,
                        passengerPhone = passengerPhone,
                        passengerEmail = passengerEmail,
                        finalPerSeat = finalPerSeat,
                        finalAmount = totalFinal
                    };

                    HttpContext.Session.SetString("PendingBooking", JsonConvert.SerializeObject(pendingData));

                    var amount = ((long)(totalFinal * 100m)).ToString();

                    var vnpBaseUrl = _config["VnPay:BaseUrl"]?.Trim();
                    var vnpReturnUrl = _config["VnPay:ReturnUrl"]?.Trim();
                    var vnpTmnCode = _config["VnPay:TmnCode"]?.Trim();
                    var vnpHashSecret = _config["VnPay:HashSecret"]?.Trim();

                    if (string.IsNullOrWhiteSpace(vnpBaseUrl)
                        || string.IsNullOrWhiteSpace(vnpReturnUrl)
                        || string.IsNullOrWhiteSpace(vnpTmnCode)
                        || string.IsNullOrWhiteSpace(vnpHashSecret))
                    {
                        await ReleaseSeatsByHoldCodeAsync(tripId, cleanBookingCode);

                        return Json(new
                        {
                            success = false,
                            message = "Thiếu cấu hình VNPAY trong appsettings.json."
                        });
                    }

                    var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                    if (ipAddress == "::1")
                    {
                        ipAddress = "127.0.0.1";
                    }

                    var now = DateTime.Now;
                    var expireDate = now.AddMinutes(3);

                    var vnpay = new VnPayLibrary();

                    vnpay.AddRequestData("vnp_Version", "2.1.0");
                    vnpay.AddRequestData("vnp_Command", "pay");
                    vnpay.AddRequestData("vnp_TmnCode", vnpTmnCode);
                    vnpay.AddRequestData("vnp_Amount", amount);
                    vnpay.AddRequestData("vnp_CreateDate", now.ToString("yyyyMMddHHmmss"));
                    vnpay.AddRequestData("vnp_CurrCode", "VND");
                    vnpay.AddRequestData("vnp_IpAddr", ipAddress ?? "127.0.0.1");
                    vnpay.AddRequestData("vnp_Locale", "vn");
                    vnpay.AddRequestData("vnp_OrderInfo", $"Thanh toan ve xe {cleanBookingCode}");
                    vnpay.AddRequestData("vnp_OrderType", "other");
                    vnpay.AddRequestData("vnp_ReturnUrl", vnpReturnUrl);
                    vnpay.AddRequestData("vnp_TxnRef", cleanBookingCode);
                    vnpay.AddRequestData("vnp_ExpireDate", expireDate.ToString("yyyyMMddHHmmss"));

                    var paymentUrl = vnpay.CreateRequestUrl(vnpBaseUrl, vnpHashSecret);
                    // Cộng 1 lần đặt VNPAY nhưng chưa thanh toán thành công
                    await IncreaseUnpaidCountAsync(
                        passengerName,
                        dob,
                        passengerPhone,
                        passengerEmail
                    );

                    return Json(new
                    {
                        success = true,
                        isRedirect = true,
                        paymentUrl
                    });
                }

                if (paymentMethod == "MOMO")
                {
                    // MoMo chỉ chấp nhận chữ và số cho orderId nên ta clean tương tự VNPAY
                    var cleanBookingCode = new string(bookingCode.Where(char.IsLetterOrDigit).ToArray());

                    // 1. Giữ ghế tạm thời để tránh người khác đặt trùng trong lúc đang quét mã
                    var holdResult = await TryHoldSeatsAsync(tripId, seatNumbers, cleanBookingCode);
                    if (!holdResult.Success)
                    {
                        return Json(new { success = false, message = holdResult.Message });
                    }

                    // 2. Gom toàn bộ thông tin gốc lưu vào Session chờ xử lý khi quay lại (Return URL)
                    var pendingData = new PendingBookingDTO
                    {
                        tripId = tripId,
                        seatNumbers = seatNumbers,
                        passengerName = passengerName,
                        dob = dob,
                        passengerPhone = passengerPhone,
                        passengerEmail = passengerEmail,
                        finalPerSeat = finalPerSeat,
                        finalAmount = totalFinal
                    };
                    HttpContext.Session.SetString("PendingBooking", JsonConvert.SerializeObject(pendingData));

                    // 3. Gọi sang Service MoMo để khởi tạo link thanh toán (Số tiền đổi sang kiểu double)
                    var orderInfo = $"Thanh toan ve xe {cleanBookingCode}";
                    var momoResponse =
                        await _momoService.CreatePaymentAsync(cleanBookingCode, orderInfo, (long)totalFinal);

                    if (momoResponse != null && !string.IsNullOrEmpty(momoResponse.PayUrl))
                    {
                        // Tăng 1 lần đếm chưa thanh toán tương tự VNPAY để chống spam huỷ đơn
                        await IncreaseUnpaidCountAsync(passengerName, dob, passengerPhone, passengerEmail);

                        return Json(new
                        {
                            success = true,
                            isRedirect = true,
                            paymentUrl = momoResponse.PayUrl
                        });
                    }
                    else
                    {
                        // Nếu MoMo lỗi, lập tức nhả ghế ra để khách khác mua
                        await ReleaseSeatsByHoldCodeAsync(tripId, cleanBookingCode);
                        return Json(new
                            { success = false, message = "Không thể khởi tạo giao dịch với MoMo. Vui lòng thử lại!" });
                    }
                }

                if (paymentMethod == "PAYOS")
                {
                    // Bỏ các ký tự đặc biệt để làm mã giữ chỗ
                    var cleanBookingCode = new string(bookingCode.Where(char.IsLetterOrDigit).ToArray());

                    var holdResult = await TryHoldSeatsAsync(tripId, seatNumbers, cleanBookingCode);
                    if (!holdResult.Success)
                    {
                        return Json(new { success = false, message = holdResult.Message });
                    }

                    // TẠO MÃ SỐ DUY NHẤT CHO PAYOS (Kiểu long - Bắt buộc)
                    string timeStamp = DateTimeOffset.Now.ToString("yyMMddHHmmss");
                    string randomStr = new Random().Next(10, 99).ToString();
                    long orderCode = long.Parse(timeStamp + randomStr);

                    var pendingData = new PendingBookingDTO
                    {
                        tripId = tripId,
                        seatNumbers = seatNumbers,
                        passengerName = passengerName,
                        dob = dob,
                        passengerPhone = passengerPhone,
                        passengerEmail = passengerEmail,
                        finalPerSeat = finalPerSeat,
                        finalAmount = totalFinal,
                        orderCodePayOS = orderCode,
                        holdCode = cleanBookingCode
                    };
                    HttpContext.Session.SetString("PendingBooking", JsonConvert.SerializeObject(pendingData));

                    try
                    {
                        // 1. Lấy Key từ appsettings.json
                        var clientId = _config["PayOS:ClientId"];
                        var apiKey = _config["PayOS:ApiKey"];
                        var checksumKey = _config["PayOS:ChecksumKey"];

                        // 2. Khởi tạo Client theo chuẩn thư viện 'payOS' mới
                        PayOSClient payOSClient = new PayOSClient(clientId, apiKey, checksumKey);

                        var domain = $"{Request.Scheme}://{Request.Host}";
                        var returnUrl = $"{domain}/Booking/PayOSReturn";
                        var cancelUrl = $"{domain}/Booking/PayOSReturn?cancel=true";

                        // LƯU Ý: Description của payOS chỉ cho phép tối đa 25 ký tự, không chứa ký tự đặc biệt
                        string desc = $"Thanh toan ve {cleanBookingCode}";
                        if (desc.Length > 25) desc = desc.Substring(0, 25);

                        // 3. Tạo Request thanh toán
                        var paymentRequest = new CreatePaymentLinkRequest
                        {
                            OrderCode = orderCode,
                            Amount = (int)totalFinal,
                            Description = desc,
                            ReturnUrl = returnUrl,
                            CancelUrl = cancelUrl,
                            ExpiredAt = (long)DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeSeconds()
                        };

                        // 4. Gọi API tạo link
                        var paymentLink = await payOSClient.PaymentRequests.CreateAsync(paymentRequest);

                        await IncreaseUnpaidCountAsync(passengerName, dob, passengerPhone, passengerEmail);

                        return Json(new
                        {
                            success = true,
                            isRedirect = true,
                            paymentUrl = paymentLink.CheckoutUrl // Trả về URL để popup mở
                        });
                    }
                    catch (Exception ex)
                    {
                        await ReleaseSeatsByHoldCodeAsync(tripId, cleanBookingCode);
                        return Json(new { success = false, message = "Lỗi khởi tạo PayOS: " + ex.Message });
                    }
                }

                var cashHoldResult = await TryHoldSeatsAsync(tripId, seatNumbers, bookingCode);

                if (!cashHoldResult.Success)
                {
                    return Json(new
                    {
                        success = false,
                        message = cashHoldResult.Message
                    });
                }

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
                    BranchId = (await _dbContext.Users
                            .Find(u => u.Username == User.Identity.Name)
                            .FirstOrDefaultAsync())
                        ?.BranchId,
                    BookingTime = DateTime.UtcNow,
                    TotalPrice = basePrice * seatNumbers.Count,
                    DiscountAmount = discountPerSeat * seatNumbers.Count,
                    FinalAmount = totalFinal,
                    BookingStatus = "Completed",
                    PaymentStatus = "Paid",
                    Passengers = seatNumbers.Select(s => new PassengerDetail
                    {
                        SeatNumber = s,
                        Name = passengerName,
                        Dob = dob,
                        FinalSeatPrice = finalPerSeat
                    }).ToList(),
                    Payment = new PaymentInfo
                    {
                        PaymentMethod = "Cash",
                        AmountPaid = totalFinal,
                        TransactionCode = "CASH-" + Guid.NewGuid().ToString("N")[..6].ToUpper()
                    },
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = User.Identity?.Name ?? "Booking-Counter"
                };

                await _dbContext.Bookings.InsertOneAsync(newBooking);

                await ResetUnpaidCountAsync(passengerPhone);

                await MarkHeldSeatsAsBookedAsync(tripId, seatNumbers, bookingCode);

                await _rabbitMqService.PublishOrderAsync(newBooking.BookingCode);

                TempData["NewBookingCode"] = newBooking.BookingCode;
                return Json(new
                {
                    success = true,
                    redirectUrl = Url.Action("Index", "Booking")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }


        [HttpGet]
        public async Task<IActionResult> PayOSReturn(bool cancel = false)
        {
            var sessionData = HttpContext.Session.GetString("PendingBooking");
            if (string.IsNullOrEmpty(sessionData))
            {
                TempData["Error"] = "Không tìm thấy thông tin phiên đặt vé hoặc phiên đã hết hạn.";
                return RedirectToAction("Index", "Booking");
            }

            var pending = JsonConvert.DeserializeObject<PendingBookingDTO>(sessionData);

            if (cancel || Request.Query["cancel"].ToString() == "true")
            {
                await ReleaseSeatsByHoldCodeAsync(pending.tripId, pending.holdCode);
                HttpContext.Session.Remove("PendingBooking");
                TempData["Error"] = "Thanh toán PayOS đã bị hủy.";
                return RedirectToAction("Index", "Booking");
            }

            var code = Request.Query["code"].ToString();
            var status = Request.Query["status"].ToString();

            // code = "00" hoặc status = "PAID" là thành công
            if (code == "00" || status == "PAID")
            {
                try
                {
                    var trip = await _dbContext.Trips.Find(t => t.Id == pending.tripId).FirstOrDefaultAsync();
                    if (trip == null)
                    {
                        TempData["Error"] = "Chuyến xe không tồn tại.";
                        return RedirectToAction("Index", "Booking");
                    }

                    decimal basePrice = trip.BaseFare;

                    var newBooking = new Booking
                    {
                        BookingCode = pending.holdCode,
                        TripId = pending.tripId,
                        CustomerId = await ResolveCustomerIdAsync(pending.passengerName, pending.dob,
                            pending.passengerPhone, pending.passengerEmail),
                        CustomerPhone = pending.passengerPhone,
                        CustomerEmail = pending.passengerEmail,
                        UserId = User.Identity?.IsAuthenticated == true
                            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
                            : "GUEST",
                        BookingTime = DateTime.UtcNow,
                        TotalPrice = basePrice * pending.seatNumbers.Count,
                        DiscountAmount = (basePrice - pending.finalPerSeat) * pending.seatNumbers.Count,
                        FinalAmount = pending.finalAmount,
                        BookingStatus = "Completed",
                        PaymentStatus = "Paid",
                        Passengers = pending.seatNumbers.Select(s => new PassengerDetail
                        {
                            SeatNumber = s,
                            Name = pending.passengerName,
                            Dob = pending.dob,
                            FinalSeatPrice = pending.finalPerSeat
                        }).ToList(),
                        Payment = new PaymentInfo
                        {
                            PaymentMethod = "PAYOS",
                            AmountPaid = pending.finalAmount,
                            TransactionCode = "PAYOS-" + pending.orderCodePayOS
                        },
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = User.Identity?.Name ?? "Online-Booking"
                    };

                    await _dbContext.Bookings.InsertOneAsync(newBooking);
                    await ResetUnpaidCountAsync(pending.passengerPhone);
                    await MarkHeldSeatsAsBookedAsync(pending.tripId, pending.seatNumbers, pending.holdCode);
                    await _rabbitMqService.PublishOrderAsync(newBooking.BookingCode);

                    HttpContext.Session.Remove("PendingBooking");
                    TempData["NewBookingCode"] = newBooking.BookingCode;

                    return RedirectToAction("Index", "Booking");
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Lỗi khi lưu đơn hàng: " + ex.Message;
                    return RedirectToAction("Index", "Booking");
                }
            }

            await ReleaseSeatsByHoldCodeAsync(pending.tripId, pending.holdCode);
            HttpContext.Session.Remove("PendingBooking");
            TempData["Error"] = "Giao dịch PayOS không thành công.";
            return RedirectToAction("Index", "Booking");
        }

        [HttpGet]
        public async Task<IActionResult> MomoReturn()
        {
            // 1. Đọc các tham số MoMo trả về trên URL
            var resultCode = Request.Query["resultCode"].ToString();
            var orderId = Request.Query["orderId"].ToString(); // Đây chính là cleanBookingCode
            var message = Request.Query["message"].ToString();
            var transId = Request.Query["transId"].ToString(); // Mã giao dịch của MoMo

            // 2. Đọc dữ liệu khách hàng đang lưu tạm trong Session ra
            var sessionData = HttpContext.Session.GetString("PendingBooking");
            if (string.IsNullOrEmpty(sessionData))
            {
                TempData["Error"] = "Không tìm thấy thông tin phiên đặt vé hoặc phiên đã hết hạn.";
                return RedirectToAction("Index", "Booking");
            }

            var pending = JsonConvert.DeserializeObject<PendingBookingDTO>(sessionData);

            // 3. Kiểm tra nếu thanh toán thành công (resultCode == "0")
            if (resultCode == "0")
            {
                try
                {
                    var trip = await _dbContext.Trips.Find(t => t.Id == pending.tripId).FirstOrDefaultAsync();
                    if (trip == null)
                    {
                        TempData["Error"] = "Chuyến xe không tồn tại hoặc đã bị hủy.";
                        return RedirectToAction("Index", "Booking");
                    }

                    // Tính lại Base Price tổng để lưu DB đúng cấu trúc của bạn
                    decimal basePrice = trip.BaseFare;

                    // Tạo đối tượng Booking chính thức để lưu vào MongoDB giống hệt bên Cash
                    var newBooking = new Booking
                    {
                        BookingCode = orderId, // Sử dụng lại mã đã sinh lúc tạo link MoMo
                        TripId = pending.tripId,
                        CustomerId = await ResolveCustomerIdAsync(pending.passengerName, pending.dob,
                            pending.passengerPhone, pending.passengerEmail),
                        CustomerPhone = pending.passengerPhone,
                        CustomerEmail = pending.passengerEmail,
                        UserId = User.Identity?.IsAuthenticated == true
                            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
                            : "GUEST",
                        BookingTime = DateTime.UtcNow,
                        TotalPrice = basePrice * pending.seatNumbers.Count,
                        DiscountAmount = (basePrice - pending.finalPerSeat) * pending.seatNumbers.Count,
                        FinalAmount = pending.finalAmount,
                        BookingStatus = "Completed",
                        PaymentStatus = "Paid", // Đã thanh toán thành công
                        Passengers = pending.seatNumbers.Select(s => new PassengerDetail
                        {
                            SeatNumber = s,
                            Name = pending.passengerName,
                            Dob = pending.dob,
                            FinalSeatPrice = pending.finalPerSeat
                        }).ToList(),
                        Payment = new PaymentInfo
                        {
                            PaymentMethod = "MOMO",
                            AmountPaid = pending.finalAmount,
                            TransactionCode = "MOMO-" + transId // Lưu lại mã giao dịch thật từ MoMo
                        },
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = User.Identity?.Name ?? "Online-Booking"
                    };

                    // Lưu chính thức vào database
                    await _dbContext.Bookings.InsertOneAsync(newBooking);

                    // Reset số lần chưa thanh toán (vì khách đã thanh toán thành công)
                    await ResetUnpaidCountAsync(pending.passengerPhone);

                    // Chuyển trạng thái ghế từ "Đang giữ" sang "Đã đặt chính thức"
                    await MarkHeldSeatsAsBookedAsync(pending.tripId, pending.seatNumbers, orderId);

                    await _rabbitMqService.PublishOrderAsync(newBooking.BookingCode);

                    // Xóa session tạm sau khi đã lưu DB thành công
                    HttpContext.Session.Remove("PendingBooking");

                    TempData["NewBookingCode"] = newBooking.BookingCode;

                    // Chuyển hướng về trang Index hoặc trang hiển thị vé thành công
                    return RedirectToAction("Index", "Booking");
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Lỗi khi lưu đơn hàng: " + ex.Message;
                    return RedirectToAction("Index", "Booking");
                }
            }
            else
            {
                // 4. Nếu thanh toán thất bại hoặc hủy: Nhả ghế ra cho người khác đặt
                await ReleaseSeatsByHoldCodeAsync(pending.tripId, orderId);
                HttpContext.Session.Remove("PendingBooking");

                TempData["Error"] = $"Thanh toán MoMo thất bại hoặc đã bị hủy. (Mã lỗi: {message})";
                return RedirectToAction("Index", "Booking");
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmPayment()
        {
            var collections = Request.Query;

            var vnpayData = new SortedList<string, string>(new VnPayCompare());

            foreach (var key in collections.Keys)
            {
                var value = collections[key].ToString();

                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                {
                    vnpayData.Add(key, value);
                }
            }

            var hashSecret = _config["VnPay:HashSecret"]?.Trim();

            if (string.IsNullOrWhiteSpace(hashSecret))
            {
                return Content("Thiếu cấu hình VNPAY HashSecret.");
            }

            var vnpay = new VnPayLibrary();
            var isValidSignature = vnpay.ValidateSignature(vnpayData, hashSecret);
            var responseCode = collections["vnp_ResponseCode"].ToString();
            var holdCode = collections["vnp_TxnRef"].ToString();

            if (isValidSignature && responseCode == "00")
            {
                var pendingJson = HttpContext.Session.GetString("PendingBooking");

                if (string.IsNullOrEmpty(pendingJson))
                {
                    return Content("Phiên thanh toán đã hết hạn!");
                }

                var data = JsonConvert.DeserializeObject<PendingBookingDTO>(pendingJson);

                if (data == null || data.seatNumbers == null || !data.seatNumbers.Any())
                {
                    return Content("Dữ liệu đặt vé không hợp lệ!");
                }

                var seatNumbers = NormalizeSeatNumbers(data.seatNumbers);
                var finalPerSeat = data.finalPerSeat;
                var finalAmount = finalPerSeat * seatNumbers.Count;

                var isStillHolding = await HasValidHoldAsync(
                    data.tripId,
                    seatNumbers,
                    holdCode
                );

                if (!isStillHolding)
                {
                    return Content(
                        "Ghế giữ chỗ đã hết hạn hoặc đã được người khác đặt. Vui lòng liên hệ quầy vé để xử lý hoàn tiền.");
                }

                var newBooking = new Booking
                {
                    BookingCode = holdCode,
                    TripId = data.tripId,
                    CustomerId = await ResolveCustomerIdAsync(
                        data.passengerName,
                        data.dob,
                        data.passengerPhone,
                        data.passengerEmail
                    ),
                    CustomerPhone = data.passengerPhone,
                    CustomerEmail = data.passengerEmail,
                    UserId = User.Identity?.IsAuthenticated == true
                        ? User.FindFirstValue(ClaimTypes.NameIdentifier)
                        : "VNPAY",
                    BookingTime = DateTime.UtcNow,
                    TotalPrice = finalAmount,
                    DiscountAmount = 0,
                    FinalAmount = finalAmount,
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
                    {
                        PaymentMethod = "VnPay",
                        AmountPaid = finalAmount,
                        TransactionCode = collections["vnp_TransactionNo"].ToString()
                    },
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "VNPAY"
                };

                await _dbContext.Bookings.InsertOneAsync(newBooking);

                await ResetUnpaidCountAsync(data.passengerPhone);

                await MarkHeldSeatsAsBookedAsync(data.tripId, seatNumbers, holdCode);

                await _rabbitMqService.PublishOrderAsync(newBooking.BookingCode);

                HttpContext.Session.Remove("PendingBooking");

                TempData["NewBookingCode"] = newBooking.BookingCode;

                return RedirectToAction("Index", "Booking");
            }

            if (!string.IsNullOrWhiteSpace(holdCode))
            {
                var pendingJson = HttpContext.Session.GetString("PendingBooking");

                if (!string.IsNullOrEmpty(pendingJson))
                {
                    var data = JsonConvert.DeserializeObject<PendingBookingDTO>(pendingJson);

                    if (data != null)
                    {
                        await ReleaseSeatsByHoldCodeAsync(data.tripId, holdCode);
                    }
                }

                HttpContext.Session.Remove("PendingBooking");
            }

            return Content($"Thanh toán thất bại! ResponseCode: {responseCode}");
        }

        public class PendingBookingDTO
        {
            public string tripId { get; set; } = string.Empty;
            public List<string> seatNumbers { get; set; } = new();
            public string passengerName { get; set; } = string.Empty;
            public DateTime dob { get; set; }
            public string passengerPhone { get; set; } = string.Empty;
            public string passengerEmail { get; set; } = string.Empty;
            public decimal finalPerSeat { get; set; }
            public decimal finalAmount { get; set; }
            public long orderCodePayOS { get; set; }
            public string holdCode { get; set; } = string.Empty;
        }

        public async Task<IActionResult> GetBookingDetails(string code)
        {
            var booking = await _dbContext.Bookings
                .Find(b => b.BookingCode == code)
                .FirstOrDefaultAsync();

            if (booking == null)
            {
                return Json(new { success = false });
            }

            var trip = await _dbContext.Trips
                .Find(t => t.Id == booking.TripId)
                .FirstOrDefaultAsync();

            if (trip == null)
            {
                return Json(new { success = false });
            }

            var bus = await _dbContext.Buses
                .Find(b => b.Id == trip.BusId)
                .FirstOrDefaultAsync();
            return Json(new
            {
                success = true,
                bookingCode = booking.BookingCode,
                busInfo = bus != null
                    ? $"{bus.BusCode} - Biển: {bus.LicensePlate}"
                    : "Xe không xác định",
                departureTime = trip.DepartureTime.ToString("HH:mm dd/MM/yyyy"),
                passengerName = booking.Passengers.FirstOrDefault()?.Name,
                seats = string.Join(", ", booking.Passengers.Select(p => p.SeatNumber)),
                finalTotal = booking.FinalAmount.ToString("N0") + " đ"
            });
        }

        private async Task<string> ResolveCustomerIdAsync(string fullName, DateTime dob, string phone, string email)
        {
            Customer? existing = null;

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

            if (existing != null)
            {
                var updates = new List<UpdateDefinition<Customer>>();

                if (!string.IsNullOrEmpty(fullName) && existing.FullName != fullName)
                {
                    updates.Add(Builders<Customer>.Update.Set(c => c.FullName, fullName));
                }

                if (!string.IsNullOrEmpty(email) && existing.Email != email)
                {
                    updates.Add(Builders<Customer>.Update.Set(c => c.Email, email));
                }

                if (!string.IsNullOrEmpty(phone) && existing.PhoneNumber != phone)
                {
                    updates.Add(Builders<Customer>.Update.Set(c => c.PhoneNumber, phone));
                }

                if (existing.Dob != dob)
                {
                    updates.Add(Builders<Customer>.Update.Set(c => c.Dob, dob));
                }

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

            var customer = new Customer
            {
                CustomerCode = "KH-" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
                FullName = fullName,
                Dob = dob,
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

            if (customer == null)
            {
                return NotFound();
            }

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

            if (config == null)
            {
                return Ok(new { rules = new List<AgeDiscountRule>() });
            }

            return Ok(new { rules = config.AgeDiscountRules });
        }

        private async Task IncreaseUnpaidCountAsync(string fullName, DateTime dob, string phone, string email)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return;
            }

            var customer = await _dbContext.Customers
                .Find(c => c.PhoneNumber == phone)
                .FirstOrDefaultAsync();

            if (customer == null)
            {
                customer = new Customer
                {
                    CustomerCode = "KH-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper(),
                    FullName = fullName,
                    Dob = dob,
                    Gender = "Khác",
                    PhoneNumber = phone,
                    Email = email,
                    ConsecutiveUnpaidCount = 1,
                    IsBlocked = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "Booking-Counter",
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = "Booking-Counter"
                };

                await _dbContext.Customers.InsertOneAsync(customer);
                return;
            }

            var newCount = customer.ConsecutiveUnpaidCount + 1;
            var shouldBlock = newCount >= 3;

            var updates = new List<UpdateDefinition<Customer>>
            {
                Builders<Customer>.Update.Set(c => c.ConsecutiveUnpaidCount, newCount),
                Builders<Customer>.Update.Set(c => c.UpdatedAt, DateTime.UtcNow),
                Builders<Customer>.Update.Set(c => c.UpdatedBy, "Booking-Counter")
            };

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                updates.Add(Builders<Customer>.Update.Set(c => c.FullName, fullName));
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                updates.Add(Builders<Customer>.Update.Set(c => c.Email, email));
            }

            if (dob != default)
            {
                updates.Add(Builders<Customer>.Update.Set(c => c.Dob, dob));
            }

            if (shouldBlock)
            {
                updates.Add(Builders<Customer>.Update.Set(c => c.IsBlocked, true));
                updates.Add(Builders<Customer>.Update.Set(c => c.BlockReason,
                    "Quá 3 lần đặt vé nhưng không thanh toán."));
                updates.Add(Builders<Customer>.Update.Set(c => c.BlockedAt, DateTime.UtcNow));
            }

            await _dbContext.Customers.UpdateOneAsync(
                c => c.Id == customer.Id,
                Builders<Customer>.Update.Combine(updates)
            );
        }

        private async Task ResetUnpaidCountAsync(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return;
            }

            await _dbContext.Customers.UpdateOneAsync(
                c => c.PhoneNumber == phone,
                Builders<Customer>.Update.Combine(
                    Builders<Customer>.Update.Set(c => c.ConsecutiveUnpaidCount, 0),
                    Builders<Customer>.Update.Set(c => c.IsBlocked, false),
                    Builders<Customer>.Update.Set(c => c.BlockReason, null),
                    Builders<Customer>.Update.Set(c => c.BlockedAt, null),
                    Builders<Customer>.Update.Set(c => c.UpdatedAt, DateTime.UtcNow),
                    Builders<Customer>.Update.Set(c => c.UpdatedBy, "Payment-Success")
                )
            );
        }

        private static List<string> NormalizeSeatNumbers(IEnumerable<string> seatNumbers)
        {
            return seatNumbers
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim().ToUpper())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task ReleaseExpiredHoldsAsync(string tripId)
        {
            var now = DateTime.UtcNow;

            var expiredHoldFilter = Builders<RealtimeSeat>.Filter.And(
                Builders<RealtimeSeat>.Filter.Eq(s => s.Status, "Holding"),
                Builders<RealtimeSeat>.Filter.Lte(s => s.HeldUntil, now)
            );

            var update = Builders<Trip>.Update.PullFilter(
                t => t.RealtimeSeats,
                expiredHoldFilter
            );

            await _dbContext.Trips.UpdateOneAsync(
                t => t.Id == tripId,
                update
            );
        }

        private async Task ReleaseSeatsByHoldCodeAsync(string tripId, string holdCode)
        {
            var holdFilter = Builders<RealtimeSeat>.Filter.And(
                Builders<RealtimeSeat>.Filter.Eq(s => s.Status, "Holding"),
                Builders<RealtimeSeat>.Filter.Eq(s => s.HeldByCustomerId, holdCode)
            );

            var update = Builders<Trip>.Update.PullFilter(
                t => t.RealtimeSeats,
                holdFilter
            );

            await _dbContext.Trips.UpdateOneAsync(
                t => t.Id == tripId,
                update
            );
        }

        private async Task<(bool Success, string Message)> TryHoldSeatsAsync(
            string tripId,
            List<string> seatNumbers,
            string holdCode)
        {
            await ReleaseExpiredHoldsAsync(tripId);

            var normalizedSeats = NormalizeSeatNumbers(seatNumbers);
            var now = DateTime.UtcNow;
            var heldUntil = now.AddMinutes(3);

            var activeBookings = await _dbContext.Bookings
                .Find(b => b.TripId == tripId && b.BookingStatus == "Completed")
                .ToListAsync();

            var bookedSeats = activeBookings
                .SelectMany(b => b.Passengers ?? new List<PassengerDetail>())
                .Select(p => p.SeatNumber.Trim().ToUpper())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var alreadyBookedSeat = normalizedSeats.FirstOrDefault(bookedSeats.Contains);

            if (!string.IsNullOrWhiteSpace(alreadyBookedSeat))
            {
                return (false, $"Ghế {alreadyBookedSeat} đã được đặt.");
            }

            foreach (var seat in normalizedSeats)
            {
                var activeSeatFilter = Builders<RealtimeSeat>.Filter.And(
                    Builders<RealtimeSeat>.Filter.Eq(s => s.SeatNumber, seat),
                    Builders<RealtimeSeat>.Filter.Or(
                        Builders<RealtimeSeat>.Filter.Eq(s => s.Status, "Booked"),
                        Builders<RealtimeSeat>.Filter.And(
                            Builders<RealtimeSeat>.Filter.Eq(s => s.Status, "Holding"),
                            Builders<RealtimeSeat>.Filter.Gt(s => s.HeldUntil, now)
                        )
                    )
                );

                var tripFilter = Builders<Trip>.Filter.And(
                    Builders<Trip>.Filter.Eq(t => t.Id, tripId),
                    Builders<Trip>.Filter.Not(
                        Builders<Trip>.Filter.ElemMatch(t => t.RealtimeSeats, activeSeatFilter)
                    )
                );

                var update = Builders<Trip>.Update.Push(
                    t => t.RealtimeSeats,
                    new RealtimeSeat
                    {
                        SeatNumber = seat,
                        Status = "Holding",
                        HeldUntil = heldUntil,
                        HeldByCustomerId = holdCode
                    }
                );

                var result = await _dbContext.Trips.UpdateOneAsync(tripFilter, update);

                if (result.ModifiedCount != 1)
                {
                    await ReleaseSeatsByHoldCodeAsync(tripId, holdCode);

                    return (false, $"Ghế {seat} đang được người khác giữ hoặc đã đặt.");
                }
            }

            return (true, "Giữ chỗ thành công.");
        }

        private async Task<bool> HasValidHoldAsync(string tripId, List<string> seatNumbers, string holdCode)
        {
            await ReleaseExpiredHoldsAsync(tripId);

            var trip = await _dbContext.Trips
                .Find(t => t.Id == tripId)
                .FirstOrDefaultAsync();

            if (trip == null || trip.DeletedAt.HasValue)
            {
                return false;
            }

            var now = DateTime.UtcNow;
            var realtimeSeats = trip.RealtimeSeats ?? new List<RealtimeSeat>();
            var normalizedSeats = NormalizeSeatNumbers(seatNumbers);

            return normalizedSeats.All(seat =>
                realtimeSeats.Any(s =>
                    s.SeatNumber != null
                    && s.SeatNumber.Trim().Equals(seat, StringComparison.OrdinalIgnoreCase)
                    && s.Status == "Holding"
                    && s.HeldByCustomerId == holdCode
                    && s.HeldUntil.HasValue
                    && s.HeldUntil.Value > now
                )
            );
        }

        private async Task MarkHeldSeatsAsBookedAsync(string tripId, List<string> seatNumbers, string holdCode)
        {
            var normalizedSeats = NormalizeSeatNumbers(seatNumbers);

            var pullFilter = Builders<RealtimeSeat>.Filter.And(
                Builders<RealtimeSeat>.Filter.Eq(s => s.HeldByCustomerId, holdCode),
                Builders<RealtimeSeat>.Filter.In(s => s.SeatNumber, normalizedSeats)
            );

            await _dbContext.Trips.UpdateOneAsync(
                t => t.Id == tripId,
                Builders<Trip>.Update.PullFilter(t => t.RealtimeSeats, pullFilter)
            );

            var bookedSeats = normalizedSeats.Select(seat => new RealtimeSeat
            {
                SeatNumber = seat,
                Status = "Booked",
                HeldUntil = null,
                HeldByCustomerId = holdCode
            }).ToList();

            await _dbContext.Trips.UpdateOneAsync(
                t => t.Id == tripId,
                Builders<Trip>.Update.PushEach(t => t.RealtimeSeats, bookedSeats)
            );
        }
    }
}