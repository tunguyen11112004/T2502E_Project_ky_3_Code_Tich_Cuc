using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Bus_ticket.Data;
using Bus_ticket.Models;
using Bus_ticket.ViewModels;

namespace Bus_ticket.Controllers
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
            var trips = await SearchTripsAsync(null, null, null);
            var routeIds = trips.Select(t => t.Trip.RouteId).Distinct().ToList();
            var routeMap = await LoadRouteMapAsync(routeIds);

            var viewModel = new BookingIndexViewModel
            {
                Trips = MapTripItems(trips, routeMap, false)
                    .OrderBy(t => t.DepartureTime)
                    .ToList()
            };

            return View(viewModel);
        }

        private async Task<List<(Trip Trip, BusRoute? Route)>> SearchTripsAsync(
            string? from,
            string? to,
            DateTime? date)
        {
            var hasRouteFilter = !string.IsNullOrWhiteSpace(from) || !string.IsNullOrWhiteSpace(to);
            List<string>? routeIds = null;

            if (hasRouteFilter)
            {
                var routeFilter = BuildRouteFilter(from, to);
                var routes = await _dbContext.BusRoutes.Find(routeFilter).ToListAsync();
                if (routes.Count == 0)
                {
                    return new List<(Trip, BusRoute?)>();
                }

                routeIds = routes.Select(r => r.Id).ToList();
            }

            var tripFilter = Builders<Trip>.Filter.In(t => t.Status, new[] { "Scheduled", "Active", "Completed" });

            if (hasRouteFilter && routeIds != null)
            {
                tripFilter &= Builders<Trip>.Filter.In(t => t.RouteId, routeIds);
            }

            if (date.HasValue)
            {
                var start = date.Value.Date;
                var end = start.AddDays(1);
                tripFilter &= Builders<Trip>.Filter.Gte(t => t.DepartureTime, start)
                             & Builders<Trip>.Filter.Lt(t => t.DepartureTime, end);
            }

            var trips = await _dbContext.Trips
                .Find(tripFilter)
                .SortBy(t => t.DepartureTime)
                .ToListAsync();

            var matchedRouteIds = trips.Select(t => t.RouteId).Distinct().ToList();
            var routeMap = await LoadRouteMapAsync(matchedRouteIds);

            return trips
                .Select(t => (t, routeMap.GetValueOrDefault(t.RouteId)))
                .ToList();
        }

        private static FilterDefinition<BusRoute> BuildRouteFilter(string? from, string? to)
        {
            var filter = Builders<BusRoute>.Filter.Empty;

            if (!string.IsNullOrWhiteSpace(from))
            {
                filter &= Builders<BusRoute>.Filter.Regex(
                    r => r.DeparturePoint,
                    new BsonRegularExpression(Regex.Escape(from.Trim()), "i"));
            }

            if (!string.IsNullOrWhiteSpace(to))
            {
                filter &= Builders<BusRoute>.Filter.Regex(
                    r => r.DestinationPoint,
                    new BsonRegularExpression(Regex.Escape(to.Trim()), "i"));
            }

            return filter;
        }

        private async Task<Dictionary<string, BusRoute>> LoadRouteMapAsync(List<string> routeIds)
        {
            if (routeIds.Count == 0)
            {
                return new Dictionary<string, BusRoute>();
            }

            var routes = await _dbContext.BusRoutes
                .Find(Builders<BusRoute>.Filter.In(r => r.Id, routeIds))
                .ToListAsync();

            return routes.ToDictionary(r => r.Id, r => r);
        }

        private static List<BookingTripItemViewModel> MapTripItems(
            List<(Trip Trip, BusRoute? Route)> trips,
            Dictionary<string, BusRoute> routeMap,
            bool isReturnLeg)
        {
            return trips.Select(item =>
            {
                var route = item.Route ?? routeMap.GetValueOrDefault(item.Trip.RouteId);
                var tripCode = item.Trip.TripCode
                    ?? "#" + item.Trip.Id.Substring(item.Trip.Id.Length - 6).ToUpper();

                return new BookingTripItemViewModel
                {
                    TripId = item.Trip.Id,
                    TripCode = tripCode,
                    DeparturePoint = route?.DeparturePoint ?? "—",
                    DestinationPoint = route?.DestinationPoint ?? "—",
                    DepartureTime = item.Trip.DepartureTime,
                    BaseFare = item.Trip.BaseFare,
                    Status = item.Trip.Status,
                    IsReturnLeg = isReturnLeg
                };
            }).ToList();
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

            Bus? bus = null;
            BusClass? busClass = null;

            if (!string.IsNullOrEmpty(trip.BusId))
            {
                bus = await _dbContext.Buses.Find(b => b.Id == trip.BusId).FirstOrDefaultAsync();

                if (bus != null && !string.IsNullOrEmpty(bus.BusClassId))
                {
                    busClass = await _dbContext.BusClasses
                        .Find(bc => bc.Id == bus.BusClassId)
                        .FirstOrDefaultAsync();
                }
            }

            var layout = busClass?.DefaultLayout ?? bus?.SeatsLayout;
            var realtimeSeatsList = trip.RealtimeSeats ?? new List<RealtimeSeat>();
            var statusBySeat = realtimeSeatsList
                .Where(s => !string.IsNullOrEmpty(s.SeatNumber))
                .ToDictionary(s => s.SeatNumber, s => s.Status ?? "Available");

            var detailedSeats = new List<object>();

            if (layout?.Any() == true)
            {
                foreach (var seat in layout)
                {
                    detailedSeats.Add(new
                    {
                        seatNumber = seat.SeatNumber,
                        row = seat.Row,
                        column = seat.Column,
                        floor = seat.Floor,
                        seatType = seat.SeatType ?? "Standard",
                        status = statusBySeat.GetValueOrDefault(seat.SeatNumber, "Available")
                    });
                }
            }
            else if (realtimeSeatsList.Any())
            {
                int columns = busClass?.TotalColumns > 0 ? busClass.TotalColumns : 4;
                int index = 1;
                foreach (var rSeat in realtimeSeatsList)
                {
                    detailedSeats.Add(new
                    {
                        seatNumber = rSeat.SeatNumber,
                        row = (index - 1) / columns + 1,
                        column = (index - 1) % columns + 1,
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
                    string seatName = i < 10 ? $"A0{i}" : $"A{i}";
                    detailedSeats.Add(new
                    {
                        seatNumber = seatName,
                        row = (i - 1) / 4 + 1,
                        column = (i - 1) % 4 + 1,
                        floor = 1,
                        seatType = "Standard",
                        status = "Available"
                    });
                }
            }

            var busTypeLabel = busClass?.ClassName
                ?? bus?.LegacyBusType
                ?? "Standard Layout (40 Seats)";

            var totalColumns = busClass?.TotalColumns > 0
                ? busClass.TotalColumns
                : layout?.Any() == true
                    ? layout.Max(s => s.Column)
                    : 4;

            return Json(new
            {
                baseFare = trip.BaseFare,
                busType = busTypeLabel,
                totalFloors = busClass?.TotalFloors ?? 1,
                totalColumns = totalColumns,
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
                string finalPhone = passengerPhone?.Trim() ?? string.Empty;
                string finalEmail = passengerEmail?.Trim().ToLower() ?? string.Empty;

                var customerId = await ResolveCustomerIdAsync(finalCustomerName, passengerAge, finalPhone, finalEmail);
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
                            PhoneNumber = finalPhone,
                            Email = finalEmail,
                            Dob = DateTime.UtcNow.AddYears(-passengerAge),
                            FinalSeatPrice = pricePerSeat - seatDiscount
                        }
                    };

                    // Khởi tạo một document Booking riêng biệt cho từng ghế
                    var newBooking = new Booking
                    {
                        BookingCode = "BK-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper(), // Sinh mã đơn ngẫu nhiên không trùng lặp
                        TripId = trip.Id, 
                        CustomerId = customerId,
                        CustomerPhone = finalPhone,
                        CustomerEmail = finalEmail,
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
                
                TempData["SuccessMessage"] = $"Đặt thành công {seatNumbers.Count} đơn độc lập cho {finalCustomerName} ({finalPhone}) — Ghế: {string.Join(", ", seatNumbers)}!";
                return RedirectToAction("Index");
            }
            catch (Exception ex) {
                TempData["ErrorMessage"] = "Lỗi hệ thống MongoDB: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        private async Task<string> ResolveCustomerIdAsync(string fullName, int age, string phone, string email)
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
                    updates.Add(Builders<Customer>.Update.Set(c => c.FullName, fullName));

                if (!string.IsNullOrEmpty(email) && existing.Email != email)
                    updates.Add(Builders<Customer>.Update.Set(c => c.Email, email));

                if (!string.IsNullOrEmpty(phone) && existing.PhoneNumber != phone)
                    updates.Add(Builders<Customer>.Update.Set(c => c.PhoneNumber, phone));

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
                CustomerCode = "KH-" + Guid.NewGuid().ToString().Substring(0, 6).ToUpper(),
                FullName = fullName,
                Dob = DateTime.UtcNow.AddYears(-age),
                Gender = "Khác",
                PhoneNumber = phone,
                Email = email,
                CreatedBy = "Booking-Counter",
                UpdatedBy = "Booking-Counter"
            };

            await _dbContext.Customers.InsertOneAsync(customer);
            return customer.Id;
        }
    }
}