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

    var result = trips
        .OrderBy(t => t.DepartureTime)
        .Select(t =>
        {
            var bus = buses.FirstOrDefault(b => b.Id == t.BusId);
            var busClass = busClasses.FirstOrDefault(c => c.Id == bus?.BusClassId);
            var route = routeMap.GetValueOrDefault(t.RouteId);

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
                totalSeats,
                availableSeats,
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
        return BadRequest("Mã không hợp lệ.");
    }

    await ReleaseExpiredHoldsAsync(tripId);

    var trip = await _dbContext.Trips
        .Find(t => t.Id == tripId)
        .FirstOrDefaultAsync();

    if (trip == null || trip.DeletedAt.HasValue)
    {
        return NotFound("Không tìm thấy chuyến xe.");
    }

    var bus = await _dbContext.Buses
        .Find(b => b.Id == trip.BusId)
        .FirstOrDefaultAsync();

    if (bus == null)
    {
        return NotFound("Không tìm thấy thông tin xe.");
    }

    var busClass = await _dbContext.BusClasses
        .Find(bc => bc.Id == bus.BusClassId)
        .FirstOrDefaultAsync();

    if (busClass == null)
    {
        return NotFound("Không tìm thấy cấu hình loại xe.");
    }

    var layout = busClass.DefaultLayout != null && busClass.DefaultLayout.Any()
        ? busClass.DefaultLayout
        : BusSeatLayoutGenerator.Generate(
            busClass.TotalRows,
            busClass.TotalColumns,
            busClass.TotalFloors,
            busClass.BusType
        );

    var activeBookings = await _dbContext.Bookings
        .Find(b => b.TripId == tripId && b.BookingStatus == "Completed")
        .ToListAsync();

    var bookedSeatsFromBookings = activeBookings
        .SelectMany(b => b.Passengers ?? new List<PassengerDetail>())
        .Where(p => !string.IsNullOrWhiteSpace(p.SeatNumber))
        .Select(p => p.SeatNumber.Trim().ToUpper())
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var now = DateTime.UtcNow;
    var realtimeSeats = trip.RealtimeSeats ?? new List<RealtimeSeat>();

    var seatsList = layout
        .Where(s => !string.IsNullOrWhiteSpace(s.SeatNumber))
        .OrderBy(s => s.Floor)
        .ThenBy(s => s.Row)
        .ThenBy(s => s.Column)
        .Select(seat =>
        {
            var seatName = seat.SeatNumber.Trim().ToUpper();

            var isBookedByTrip = realtimeSeats.Any(s =>
                s.SeatNumber != null
                && s.SeatNumber.Trim().Equals(seatName, StringComparison.OrdinalIgnoreCase)
                && s.Status == "Booked"
            );

            var activeHold = realtimeSeats.FirstOrDefault(s =>
                s.SeatNumber != null
                && s.SeatNumber.Trim().Equals(seatName, StringComparison.OrdinalIgnoreCase)
                && s.Status == "Holding"
                && s.HeldUntil.HasValue
                && s.HeldUntil.Value > now
            );

            var isBooked = bookedSeatsFromBookings.Contains(seatName) || isBookedByTrip;
            var isHolding = !isBooked && activeHold != null;

            return new
            {
                seatNumber = seatName,
                row = seat.Row,
                column = seat.Column,
                floor = seat.Floor,
                seatType = seat.SeatType,
                status = isBooked ? "Booked" : isHolding ? "Holding" : "Available",
                isBooked,
                isHolding,
                isLocked = isBooked || isHolding,
                heldUntil = activeHold?.HeldUntil
            };
        })
        .ToList();

    return Json(new
    {
        cols = busClass.TotalColumns > 0 ? busClass.TotalColumns : 4,
        floors = busClass.TotalFloors > 0 ? busClass.TotalFloors : 1,
        seats = seatsList
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
                    message = "Số điện thoại này đã bị khóa do quá 3 lần đặt vé nhưng không thanh toán. Vui lòng liên hệ quầy vé để được hỗ trợ."
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
                    return Content("Ghế giữ chỗ đã hết hạn hoặc đã được người khác đặt. Vui lòng liên hệ quầy vé để xử lý hoàn tiền.");
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
                    ? $"{bus.BusCode} - {bus.LicensePlate}"
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
        updates.Add(Builders<Customer>.Update.Set(c => c.BlockReason, "Quá 3 lần đặt vé nhưng không thanh toán."));
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