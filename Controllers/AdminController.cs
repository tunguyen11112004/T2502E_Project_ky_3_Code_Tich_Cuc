using Bus_ticket.Data;
using Bus_ticket.Helpers;
using Bus_ticket.Models;
using Bus_ticket.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Bus_ticket.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Net.Http;
using System;
using System.Collections.Generic;
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
        public async Task<IActionResult> PriceConfig(int page = 1, string? searchDate = null, string searchText = "")
        {
            const int pageSize = 10;
            if (page < 1) page = 1;

            if (string.IsNullOrWhiteSpace(searchDate))
            {
                searchDate = DateTime.Now.ToString("yyyy-MM-dd");
            }

            if (!DateTime.TryParse(searchDate, out DateTime parsedDate))
            {
                parsedDate = DateTime.Now.Date;
                searchDate = parsedDate.ToString("yyyy-MM-dd");
            }

            var searchDateOnly = parsedDate.Date;

            var trips = (await _dbContext.Trips
                    .Find(TripFilters.NotDeleted)
                    .SortByDescending(t => t.DepartureTime)
                    .ToListAsync())
                .Where(t => t.DepartureTime.ToLocalTime().Date == searchDateOnly)
                .ToList();

            var routes = await _dbContext.BusRoutes.Find(_ => true).ToListAsync();
            var buses = await _dbContext.Buses.Find(_ => true).ToListAsync();
            var busClasses = await _dbContext.BusClasses.Find(_ => true).ToListAsync();
            var busClassMap = busClasses.ToDictionary(c => c.Id, c => c);
            var routeMap = routes.ToDictionary(r => r.Id, r => r);
            var busMap = buses.ToDictionary(b => b.Id, b => b);

            var tripItems = trips.Select(t =>
            {
                var route = routeMap.GetValueOrDefault(t.RouteId);
                var bus = busMap.GetValueOrDefault(t.BusId);
                var busClass = bus != null && !string.IsNullOrEmpty(bus.BusClassId)
                    ? busClassMap.GetValueOrDefault(bus.BusClassId)
                    : null;

                return new TripListItemViewModel
                {
                    Id = t.Id,
                    TripCode = t.TripCode ?? string.Empty,
                    RouteId = t.RouteId,
                    DeparturePoint = route?.DeparturePoint ?? string.Empty,
                    DestinationPoint = route?.DestinationPoint ?? string.Empty,
                    BusId = t.BusId,
                    LicensePlate = bus?.LicensePlate ?? "—",
                    BusClassName = busClass?.ClassName ?? bus?.LegacyBusType ?? "—",
                    RouteLabel = route != null
                        ? $"{route.DeparturePoint} → {route.DestinationPoint}"
                        : "—",
                    BusLabel = bus != null
                        ? $"{bus.LicensePlate} ({busClass?.ClassName ?? bus.LegacyBusType ?? "N/A"})"
                        : "—",
                    DepartureTime = t.DepartureTime,
                    ArrivalTime = t.ArrivalTime,
                    BaseFare = t.BaseFare,
                    Status = t.Status,
                    TotalSeats = t.RealtimeSeats?.Count ?? 0,
                    BookedSeats = t.RealtimeSeats?.Count(s => s.Status is "Booked" or "Holding") ?? 0
                };
            }).ToList();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var key = searchText.Trim().ToLowerInvariant();
                tripItems = tripItems.Where(t =>
                        t.TripCode.ToLowerInvariant().Contains(key)
                        || t.LicensePlate.ToLowerInvariant().Contains(key)
                        || t.RouteLabel.ToLowerInvariant().Contains(key)
                        || t.BusClassName.ToLowerInvariant().Contains(key))
                    .ToList();
            }

            var totalItems = tripItems.Count;
            var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling((double)totalItems / pageSize);
            if (page > totalPages) page = totalPages;

            var viewModel = new PriceManagementViewModel
            {
                Trips = tripItems.Skip((page - 1) * pageSize).Take(pageSize).ToList()
            };

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.SearchDate = searchDate;
            ViewBag.SearchText = searchText;

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> CreateTrip()
        {
            return View("TripForm", await BuildTripFormViewModelAsync(null));
        }

        [HttpGet]
        public async Task<IActionResult> EditTrip(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return NotFound();
            }

            var trip = await _dbContext.Trips
                .Find(Builders<Trip>.Filter.And(
                    Builders<Trip>.Filter.Eq(t => t.Id, id),
                    TripFilters.NotDeleted))
                .FirstOrDefaultAsync();
            if (trip == null)
            {
                return NotFound();
            }

            return View("TripForm", await BuildTripFormViewModelAsync(trip));
        }

        private async Task<TripFormViewModel> BuildTripFormViewModelAsync(Trip? trip)
        {
            var buses = await _dbContext.Buses.Find(_ => true).SortBy(b => b.LicensePlate).ToListAsync();
            var busClasses = await _dbContext.BusClasses.Find(_ => true).ToListAsync();
            var busClassMap = busClasses.ToDictionary(c => c.Id, c => c);
            var priceList = await _dbContext.PriceConfigs.Find(_ => true).ToListAsync();
            var routes = await _dbContext.BusRoutes.Find(_ => true).ToListAsync();
            var routeOptions = MapRouteOptions(routes);

            var defaultDeparture = DateTime.Now.Date.AddDays(1).AddHours(8);
            var defaultArrival = defaultDeparture.Date.AddHours(20);

            if (trip == null)
            {
                return new TripFormViewModel
                {
                    DepartureTime = defaultDeparture,
                    ArrivalTime = defaultArrival,
                    PriceConfigs = priceList,
                    Buses = MapBusOptions(buses, busClassMap),
                    Routes = routeOptions
                };
            }

            var route = routes.FirstOrDefault(r => r.Id == trip.RouteId);

            return new TripFormViewModel
            {
                TripId = trip.Id,
                RouteId = trip.RouteId,
                DeparturePoint = route?.DeparturePoint ?? string.Empty,
                DestinationPoint = route?.DestinationPoint ?? string.Empty,
                BusId = trip.BusId,
                BaseFare = trip.BaseFare,
                DepartureTime = trip.DepartureTime.ToLocalTime(),
                ArrivalTime = trip.ArrivalTime.ToLocalTime(),
                Status = trip.Status,
                PriceConfigs = priceList,
                Buses = MapBusOptions(buses, busClassMap),
                Routes = routeOptions
            };
        }

        private static List<RouteOptionViewModel> MapRouteOptions(List<BusRoute> routes)
        {
            return routes
                .GroupBy(r =>
                    $"{r.DeparturePoint?.Trim().ToLowerInvariant()}|{r.DestinationPoint?.Trim().ToLowerInvariant()}")
                .Select(group => group
                    .OrderByDescending(r => r.FareConfigs?.Count ?? 0)
                    .ThenBy(r => r.DeparturePoint)
                    .First())
                .OrderBy(r => r.DeparturePoint)
                .ThenBy(r => r.DestinationPoint)
                .Select(r => new RouteOptionViewModel
                {
                    Id = r.Id,
                    DeparturePoint = r.DeparturePoint,
                    DestinationPoint = r.DestinationPoint,
                    DistanceKm = r.DistanceKm,
                    FareConfigs = (r.FareConfigs ?? new List<FareConfig>())
                        .Select(f => new RouteFareOptionViewModel
                        {
                            BusType = f.BusType ?? string.Empty,
                            FlatPrice = f.FlatPrice
                        })
                        .ToList()
                })
                .ToList();
        }

        private IActionResult RedirectTripFormError(string? tripId, string message)
        {
            TempData["ErrorMessage"] = message;
            return string.IsNullOrWhiteSpace(tripId)
                ? RedirectToAction(nameof(CreateTrip))
                : RedirectToAction(nameof(EditTrip), new { id = tripId });
        }

        private static List<BusOptionViewModel> MapBusOptions(
            List<Bus> buses,
            Dictionary<string, BusClass> busClassMap)
        {
            return buses.Select(b =>
            {
                var busClass = !string.IsNullOrEmpty(b.BusClassId)
                    ? busClassMap.GetValueOrDefault(b.BusClassId)
                    : null;
                return new BusOptionViewModel
                {
                    Id = b.Id,
                    LicensePlate = b.LicensePlate,
                    BusCode = b.BusCode,
                    BusClassId = b.BusClassId ?? string.Empty,
                    BusClassName = busClass?.ClassName ?? "—",
                    BusType = busClass?.BusType ?? b.LegacyBusType ?? string.Empty
                };
            }).ToList();
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

                await SyncTripFaresFromPriceConfigAsync(busType, departurePoint, destinationPoint, basePrice);
                
                TempData["SuccessMessage"] = "Cập nhật cấu hình giá vé nền thành công! Giá đã đồng bộ sang các chuyến xe tương ứng.";
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveTrip(
            string? tripId,
            string routeId,
            string busId,
            DateTime departureTime,
            DateTime arrivalTime,
            decimal baseFare,
            string status)
        {
            if (string.IsNullOrWhiteSpace(routeId)
                || string.IsNullOrWhiteSpace(busId)
                || baseFare <= 0)
            {
                return RedirectTripFormError(tripId, "Tuyến đường, biển số xe và giá vé là bắt buộc.");
            }

            if (baseFare > 50_000_000)
            {
                return RedirectTripFormError(tripId, "Giá vé không hợp lệ (tối đa 50.000.000đ).");
            }

            if (arrivalTime <= departureTime)
            {
                return RedirectTripFormError(tripId, "Giờ đến phải sau giờ đi.");
            }

            var route = await _dbContext.BusRoutes.Find(r => r.Id == routeId).FirstOrDefaultAsync();
            if (route == null)
            {
                return RedirectTripFormError(tripId, "Tuyến đường không tồn tại trong hệ thống.");
            }

            var departurePoint = route.DeparturePoint;
            var destinationPoint = route.DestinationPoint;

            var bus = await _dbContext.Buses.Find(b => b.Id == busId).FirstOrDefaultAsync();
            if (bus == null)
            {
                return RedirectTripFormError(tripId, "Xe không tồn tại trong hệ thống.");
            }

            var scheduleError = await ValidateBusScheduleAsync(
                busId, tripId, routeId, departureTime, arrivalTime, route);
            if (scheduleError != null)
            {
                return RedirectTripFormError(tripId, scheduleError);
            }

            var userName = User.Identity?.Name ?? "Admin";

            var busClass = !string.IsNullOrWhiteSpace(bus.BusClassId)
                ? await _dbContext.BusClasses.Find(bc => bc.Id == bus.BusClassId).FirstOrDefaultAsync()
                : null;
            var busTypeLabel = ResolvePriceBusType(busClass, bus);

            await UpsertPriceConfigAsync(busTypeLabel, departurePoint, destinationPoint, baseFare);
            await SyncTripFaresFromPriceConfigAsync(busTypeLabel, departurePoint, destinationPoint, baseFare);

            var tripStatus = string.IsNullOrWhiteSpace(status) ? "Scheduled" : status.Trim();

            if (string.IsNullOrWhiteSpace(tripId))
            {
                var realtimeSeats = await BuildRealtimeSeatsForBusAsync(bus);
                if (realtimeSeats.Count == 0)
                {
                    return RedirectTripFormError(tripId, "Xe chưa có sơ đồ ghế. Cấu hình layout trong BusClasses trước.");
                }

                var trip = new Trip
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    TripCode = GenerateTripCode(departureTime),
                    BusId = busId,
                    RouteId = routeId,
                    BaseFare = baseFare,
                    DepartureTime = departureTime,
                    ArrivalTime = arrivalTime,
                    Status = tripStatus,
                    RealtimeSeats = realtimeSeats,
                    CreatedBy = userName,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedBy = userName,
                    UpdatedAt = DateTime.UtcNow
                };

                await _dbContext.Trips.InsertOneAsync(trip);
                TempData["SuccessMessage"] =
                    $"Đã lưu chuyến {trip.TripCode} — {departurePoint} → {destinationPoint}, xe {bus.LicensePlate}, giá {baseFare:N0}đ. Tra trên Booking cùng tuyến ngày {departureTime.ToLocalTime():dd/MM/yyyy}.";
            }
            else
            {
                var existing = await _dbContext.Trips
                    .Find(Builders<Trip>.Filter.And(
                        Builders<Trip>.Filter.Eq(t => t.Id, tripId),
                        TripFilters.NotDeleted))
                    .FirstOrDefaultAsync();
                if (existing == null)
                {
                    return RedirectTripFormError(tripId, "Chuyến xe không tồn tại hoặc đã bị xóa.");
                }

                var update = Builders<Trip>.Update
                    .Set(t => t.RouteId, routeId)
                    .Set(t => t.BusId, busId)
                    .Set(t => t.BaseFare, baseFare)
                    .Set(t => t.DepartureTime, departureTime)
                    .Set(t => t.ArrivalTime, arrivalTime)
                    .Set(t => t.Status, tripStatus)
                    .Set(t => t.UpdatedBy, userName)
                    .Set(t => t.UpdatedAt, DateTime.UtcNow);

                if (existing.BusId != busId)
                {
                    var hasReservedSeats = existing.RealtimeSeats?.Any(s => s.Status is "Booked" or "Holding") ?? false;
                    if (hasReservedSeats)
                    {
                        return RedirectTripFormError(tripId, "Không thể đổi xe vì chuyến đã có ghế đặt hoặc giữ chỗ.");
                    }

                    update = update.Set(t => t.RealtimeSeats, await BuildRealtimeSeatsForBusAsync(bus));
                }

                await _dbContext.Trips.UpdateOneAsync(t => t.Id == tripId, update);
                TempData["SuccessMessage"] =
                    $"Đã cập nhật chuyến — biển số {bus.LicensePlate}, giá {baseFare:N0}đ. Booking hiển thị ngay khi tra cứu.";
            }

            return RedirectToAction(nameof(PriceConfig));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTrip(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest();
            }

            var trip = await _dbContext.Trips.Find(t => t.Id == id).FirstOrDefaultAsync();
            if (trip == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy chuyến xe để xóa.";
                return RedirectToAction(nameof(PriceConfig));
            }

            if (trip.DeletedAt.HasValue)
            {
                TempData["ErrorMessage"] = "Chuyến xe này đã được xóa trước đó.";
                return RedirectToAction(nameof(PriceConfig));
            }

            var hasBookings = await _dbContext.Bookings
                .Find(b => b.TripId == id && b.BookingStatus == "Completed")
                .AnyAsync();

            if (hasBookings)
            {
                TempData["ErrorMessage"] = "Không thể xóa chuyến xe đã có vé đặt thành công.";
                return RedirectToAction(nameof(PriceConfig));
            }

            var userName = User.Identity?.Name ?? "Admin";
            var update = Builders<Trip>.Update
                .Set(t => t.Status, "Cancelled")
                .Set(t => t.DeletedAt, DateTime.UtcNow)
                .Set(t => t.DeletedBy, userName)
                .Set(t => t.UpdatedAt, DateTime.UtcNow)
                .Set(t => t.UpdatedBy, userName);

            await _dbContext.Trips.UpdateOneAsync(t => t.Id == id, update);

            TempData["SuccessMessage"] =
                "Đã xóa chuyến xe. Dữ liệu đặt vé và lịch sử liên kết vẫn được giữ nguyên.";
            return RedirectToAction(nameof(PriceConfig));
        }

        private async Task<BusRoute> FindOrCreateRouteAsync(
            string departurePoint,
            string destinationPoint,
            string userName)
        {
            var existing = await _dbContext.BusRoutes
                .Find(r => r.DeparturePoint == departurePoint && r.DestinationPoint == destinationPoint)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                return existing;
            }

            var route = new BusRoute
            {
                Id = ObjectId.GenerateNewId().ToString(),
                DeparturePoint = departurePoint,
                DestinationPoint = destinationPoint,
                DistanceKm = 0,
                CreatedBy = userName,
                UpdatedBy = userName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _dbContext.BusRoutes.InsertOneAsync(route);
            return route;
        }

        private static string ResolvePriceBusType(BusClass? busClass, Bus bus)
        {
            if (!string.IsNullOrWhiteSpace(busClass?.ClassName))
            {
                return busClass.ClassName;
            }

            if (!string.IsNullOrWhiteSpace(bus.LegacyBusType))
            {
                return bus.LegacyBusType;
            }

            return "Standard (Seat)";
        }

        private async Task UpsertPriceConfigAsync(
            string busType,
            string departurePoint,
            string destinationPoint,
            decimal basePrice)
        {
            var filter = Builders<PriceConfig>.Filter.And(
                Builders<PriceConfig>.Filter.Eq(p => p.BusType, busType),
                Builders<PriceConfig>.Filter.Eq(p => p.DeparturePoint, departurePoint),
                Builders<PriceConfig>.Filter.Eq(p => p.DestinationPoint, destinationPoint));

            var update = Builders<PriceConfig>.Update
                .Set(p => p.BasePrice, basePrice)
                .Set(p => p.UpdatedAt, DateTime.UtcNow);

            await _dbContext.PriceConfigs.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
        }

        private async Task SyncTripFaresFromPriceConfigAsync(
            string busType,
            string departurePoint,
            string destinationPoint,
            decimal basePrice)
        {
            var routes = await _dbContext.BusRoutes
                .Find(r => r.DeparturePoint == departurePoint && r.DestinationPoint == destinationPoint)
                .ToListAsync();

            if (routes.Count == 0)
            {
                return;
            }

            var routeIds = routes.Select(r => r.Id).ToList();
            var busClasses = await _dbContext.BusClasses.Find(_ => true).ToListAsync();
            var matchingClassIds = busClasses
                .Where(bc => BusTypeMatcher.Matches(busType, bc))
                .Select(bc => bc.Id)
                .ToHashSet();

            var buses = await _dbContext.Buses.Find(_ => true).ToListAsync();
            var matchingBusIds = buses
                .Where(b =>
                    (!string.IsNullOrEmpty(b.BusClassId) && matchingClassIds.Contains(b.BusClassId))
                    || BusTypeMatcher.Matches(busType, null, b))
                .Select(b => b.Id)
                .ToList();

            if (matchingBusIds.Count == 0)
            {
                return;
            }

            var filter = Builders<Trip>.Filter.And(
                TripFilters.NotDeleted,
                Builders<Trip>.Filter.In(t => t.RouteId, routeIds),
                Builders<Trip>.Filter.In(t => t.BusId, matchingBusIds));

            await _dbContext.Trips.UpdateManyAsync(
                filter,
                Builders<Trip>.Update
                    .Set(t => t.BaseFare, basePrice)
                    .Set(t => t.UpdatedAt, DateTime.UtcNow));
        }

        [HttpGet]
        public async Task<IActionResult> CheckBusSchedule(
            string busId,
            string routeId,
            DateTime departureTime,
            DateTime arrivalTime,
            string? tripId = null)
        {
            if (string.IsNullOrWhiteSpace(busId) || string.IsNullOrWhiteSpace(routeId))
            {
                return Json(new { valid = true });
            }

            var route = await _dbContext.BusRoutes.Find(r => r.Id == routeId).FirstOrDefaultAsync();
            if (route == null)
            {
                return Json(new { valid = true });
            }

            if (arrivalTime <= departureTime)
            {
                return Json(new { valid = false, message = "Giờ đến phải sau giờ đi." });
            }

            var error = await ValidateBusScheduleAsync(busId, tripId, routeId, departureTime, arrivalTime, route);
            return Json(new { valid = error == null, message = error ?? string.Empty });
        }

        private async Task<string?> ValidateBusScheduleAsync(
            string busId,
            string? excludeTripId,
            string routeId,
            DateTime departureTime,
            DateTime arrivalTime,
            BusRoute newRoute)
        {
            var filter = Builders<Trip>.Filter.And(
                TripFilters.NotDeleted,
                Builders<Trip>.Filter.Eq(t => t.BusId, busId),
                Builders<Trip>.Filter.Ne(t => t.Status, "Cancelled"));

            if (!string.IsNullOrWhiteSpace(excludeTripId))
            {
                filter = Builders<Trip>.Filter.And(
                    filter,
                    Builders<Trip>.Filter.Ne(t => t.Id, excludeTripId));
            }

            var busTrips = await _dbContext.Trips.Find(filter).ToListAsync();
            if (busTrips.Count == 0)
            {
                return null;
            }

            var routeIds = busTrips.Select(t => t.RouteId).Append(routeId).Distinct().ToList();
            var routes = await _dbContext.BusRoutes
                .Find(r => routeIds.Contains(r.Id))
                .ToListAsync();
            var routeMap = routes.ToDictionary(r => r.Id, r => r);

            var newDeparturePoint = newRoute.DeparturePoint;
            var newReturnDuration = GetReturnDuration(newRoute, routes);

            foreach (var existing in busTrips)
            {
                var existingRoute = routeMap.GetValueOrDefault(existing.RouteId);
                var tripCode = string.IsNullOrWhiteSpace(existing.TripCode) ? "—" : existing.TripCode;

                // 1. Trùng khung giờ — xe không thể chạy 2 chuyến cùng lúc
                if (departureTime < existing.ArrivalTime && arrivalTime > existing.DepartureTime)
                {
                    var routeLabel = existingRoute != null
                        ? $"{existingRoute.DeparturePoint} → {existingRoute.DestinationPoint}"
                        : "khác";
                    return
                        $"Xe đã có chuyến {tripCode} ({routeLabel}) trong khoảng {existing.DepartureTime:dd/MM/yyyy HH:mm} – {existing.ArrivalTime:dd/MM/yyyy HH:mm}. Không thể trùng lịch cùng xe.";
                }

                // 2. Chuyến cũ đang về đúng điểm đi mới (chỉ khi chuyến cũ đã bắt đầu)
                if (existingRoute != null
                    && PointsMatch(existingRoute.DestinationPoint, newDeparturePoint)
                    && departureTime >= existing.DepartureTime
                    && departureTime < existing.ArrivalTime)
                {
                    return
                        $"Xe đang trên đường về {newDeparturePoint} (chuyến {tripCode}, đến lúc {existing.ArrivalTime:dd/MM/yyyy HH:mm}). Chưa thể xuất phát từ {newDeparturePoint} lúc {departureTime:dd/MM/yyyy HH:mm}.";
                }

                // 3. Chuyến cũ xuất phát TRƯỚC từ cùng điểm đi — phải đủ thời gian khứ hồi
                if (existingRoute != null
                    && PointsMatch(existingRoute.DeparturePoint, newDeparturePoint)
                    && existing.DepartureTime < departureTime)
                {
                    var returnDuration = GetReturnDuration(existingRoute, routes);
                    var availableAfter = existing.ArrivalTime + returnDuration;

                    if (departureTime < availableAfter)
                    {
                        return
                            $"Xe chưa kịp khứ hồi về {newDeparturePoint}. Chuyến {tripCode} kết thúc lúc {existing.ArrivalTime:dd/MM/yyyy HH:mm}, cần thêm ~{FormatTravelDuration(returnDuration)} để về. Chỉ có thể đặt chuyến mới từ {newDeparturePoint} sau {availableAfter:dd/MM/yyyy HH:mm}.";
                    }
                }

                // 4. Chuyến SAU từ cùng điểm đi — chuyến mới phải kịp khứ hồi trước khi chuyến sau xuất phát
                if (existingRoute != null
                    && PointsMatch(existingRoute.DeparturePoint, newDeparturePoint)
                    && existing.DepartureTime > departureTime)
                {
                    var newRoundTripEnds = arrivalTime + newReturnDuration;

                    if (newRoundTripEnds > existing.DepartureTime)
                    {
                        var latestArrival = existing.DepartureTime - newReturnDuration;
                        return
                            $"Chuyến này chưa kịp khứ hồi về {newDeparturePoint} trước chuyến {tripCode} ({existing.DepartureTime:dd/MM/yyyy HH:mm}). Giờ đến tối đa nên trước {latestArrival:dd/MM/yyyy HH:mm} (cần ~{FormatTravelDuration(newReturnDuration)} để về).";
                    }
                }
            }

            return null;
        }

        private static bool PointsMatch(string? a, string? b) =>
            string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        private static TimeSpan GetReturnDuration(BusRoute outboundRoute, IEnumerable<BusRoute> allRoutes)
        {
            var reverse = allRoutes.FirstOrDefault(r =>
                PointsMatch(r.DeparturePoint, outboundRoute.DestinationPoint)
                && PointsMatch(r.DestinationPoint, outboundRoute.DeparturePoint));

            if (reverse != null)
            {
                return EstimateTravelDuration(reverse.DistanceKm);
            }

            return EstimateTravelDuration(outboundRoute.DistanceKm);
        }

        private static TimeSpan EstimateTravelDuration(double distanceKm)
        {
            if (distanceKm <= 0)
            {
                return TimeSpan.FromHours(8);
            }

            var travelHours = distanceKm / 60.0 + (distanceKm / 200.0) * (10.0 / 60.0);
            return TimeSpan.FromHours(Math.Max(travelHours, 1));
        }

        private static string FormatTravelDuration(TimeSpan duration)
        {
            var totalHours = (int)duration.TotalHours;
            return duration.Minutes > 0 ? $"{totalHours}g{duration.Minutes}p" : $"{totalHours} giờ";
        }

        private static string GenerateTripCode(DateTime departureTime)
        {
            return $"TRP-{departureTime:yyyyMMdd}-{Random.Shared.Next(100, 999)}";
        }

        private async Task<List<RealtimeSeat>> BuildRealtimeSeatsForBusAsync(Bus bus)
        {
            if (!string.IsNullOrWhiteSpace(bus.BusClassId))
            {
                var busClass = await _dbContext.BusClasses
                    .Find(bc => bc.Id == bus.BusClassId)
                    .FirstOrDefaultAsync();

                if (busClass?.DefaultLayout is { Count: > 0 })
                {
                    return busClass.DefaultLayout
                        .Select(s => new RealtimeSeat { SeatNumber = s.SeatNumber, Status = "Available" })
                        .ToList();
                }

                if (busClass != null && busClass.TotalRows > 0 && busClass.TotalColumns > 0)
                {
                    return BusSeatLayoutGenerator
                        .Generate(busClass.TotalRows, busClass.TotalColumns, busClass.TotalFloors, busClass.BusType)
                        .Select(s => new RealtimeSeat { SeatNumber = s.SeatNumber, Status = "Available" })
                        .ToList();
                }
            }

            if (bus.SeatsLayout is { Count: > 0 })
            {
                return bus.SeatsLayout
                    .Select(s => new RealtimeSeat { SeatNumber = s.SeatNumber, Status = "Available" })
                    .ToList();
            }

            return new List<RealtimeSeat>();
        }
    }
}