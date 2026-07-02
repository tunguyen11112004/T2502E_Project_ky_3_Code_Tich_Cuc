using Bus_ticket.Data;
using Bus_ticket.Helpers;
using Bus_ticket.Models;
using Bus_ticket.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Net.Http;
using System;
using System.Collections.Generic;
using System.Linq;

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
            return View();
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }
        
        [HttpPost]
        public async Task<IActionResult> CrawlNews()
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "vi-VN,vi;q=0.9,en-US;q=0.8,en;q=0.7");

            string categoryUrl = "https://sonhailimousine.com/tin-tuc/";
            var articleUrls = new List<string>();

            Console.WriteLine("=== BẮT ĐẦU TẢI TRANG DANH MỤC SƠN HẢI TỪ CONTROLLER ===");

            try
            {
                string htmlContent = await httpClient.GetStringAsync(categoryUrl);
                var doc = new HtmlAgilityPack.HtmlDocument();
                doc.LoadHtml(htmlContent);
                var aNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'newsnb_gr_title')]/a");
                if (aNodes != null)
                {
                    foreach (var node in aNodes)
                    {
                        string href = node.GetAttributeValue("href", "");
                        if (!string.IsNullOrEmpty(href))
                        {
                            articleUrls.Add(href);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Không thể kết nối hoặc tải trang Sơn Hải: {ex.Message}");
                TempData["InfoMessage"] = $"Kiểm tra lại kết nối hoặc dịch vụ mạng: {ex.Message}";
                return RedirectToAction("ManageNews");
            }
            if (articleUrls.Count == 0)
            {
                TempData["InfoMessage"] = "Không tìm thấy link bài viết nào. Hãy kiểm tra lại cấu trúc HTML trang gốc hoặc tab Terminal.";
                return RedirectToAction("ManageNews");
            }
            var cleanUrls = articleUrls
                .Where(url => !string.IsNullOrEmpty(url))
                .Select(url => url.StartsWith("http") ? url : $"https://sonhailimousine.com/{url.TrimStart('/')}")
                .Where(url => !url.Contains("/page/") 
                           && !url.Contains("#")
                           && url.TrimEnd('/') != "https://sonhailimousine.com/tin-tuc"
                           && url.TrimEnd('/') != "https://sonhailimousine.com")
                .Distinct()
                .ToList();

            Console.WriteLine($"[DEBUG] Tìm thấy {cleanUrls.Count} link chi tiết bài viết thực thụ sau khi lọc rác.");

            if (cleanUrls.Count == 0)
            {
                TempData["InfoMessage"] = "Hệ thống tìm thấy liên kết chung nhưng không lọc được bài viết cụ thể nào mới.";
                return RedirectToAction("ManageNews");
            }
            int count = 0;
            var newsCollection = _dbContext.News;
            var scraper = new NewsScraperService();
            foreach (var url in cleanUrls.Take(5))
            {
                var isExist = newsCollection.Find(n => n.OriginalUrl == url).Any();
                if (isExist) continue;
                var news = await scraper.ScrapePostDetailAsync(
                    postUrl: url,
                    titleXpath: "//h1[contains(@class,'post-title')] | //h1[contains(@class,'entry-title')] | //h1 | //div[contains(@class,'title-main')]", 
                    descXpath: "//div[contains(@class,'post-excerpt')] | //div[contains(@class,'entry-content')]/p[1] | //div[contains(@class,'content-main')]/p[1]",
                    contentXpath: "//div[contains(@class,'entry-content')] | //div[contains(@class,'post-content')] | //div[contains(@class,'content-main')]", 
                    thumbXpath: "//div[contains(@class,'entry-content')]//img | //div[contains(@class,'content-main')]//img"
                );
                if (news != null && !string.IsNullOrEmpty(news.Title) && !news.Title.Contains("Tin tức"))
                {
                    news.SourceSite = "Sơn Hải Limousine";
                    news.CreatedDate = DateTime.Now;
                    news.OriginalUrl = url;
                    if (!string.IsNullOrEmpty(news.ThumbnailUrl) && !news.ThumbnailUrl.StartsWith("http"))
                    {
                        news.ThumbnailUrl = $"https://sonhailimousine.com/{news.ThumbnailUrl.TrimStart('/')}";
                    }
                    else if (string.IsNullOrEmpty(news.ThumbnailUrl))
                    {
                        news.ThumbnailUrl = "https://images.unsplash.com/photo-1544620347-c4fd4a3d5957?q=80&w=600&auto=format&fit=crop";
                    }
                    news.Content += $"<p class='text-right text-xs italic mt-6 text-gray-400'>Theo {news.SourceSite} / Nguồn gốc: <a class='text-blue-500 hover:underline' href='{url}' target='_blank' rel='nofollow'>Xem bài viết gốc</a></p>";
                    await newsCollection.InsertOneAsync(news);
                    count++;
                }
            }
            if (count > 0)
            {
                TempData["SuccessMessage"] = $"Đồng bộ thành công! Hệ thống đã cập nhật thêm {count} bài viết chi tiết từ Sơn Hải Limousine vào MongoDB.";
            }
            else
            {
                TempData["InfoMessage"] = "Quá trình hoàn tất. Không có tin tức mới nào được lưu (Dữ liệu đã tồn tại đầy đủ hoặc lỗi bóc tách trang con).";
            }

            return RedirectToAction("ManageNews");
        }
        
        public IActionResult ManageNews()
        {
            var newsList = _dbContext.News.Find(_ => true).ToList()
                                          .OrderByDescending(n => n.CreatedDate)
                                          .ToList();
            return View(newsList);
        }
        
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
        public async Task<IActionResult> PriceConfig(int page = 1, string searchDate = "", string searchText = "")
        {
            const int pageSize = 10;
            if (page < 1) page = 1;

            var filter = Builders<Trip>.Filter.Empty;
            if (!string.IsNullOrEmpty(searchDate) && DateTime.TryParse(searchDate, out DateTime parsedDate))
            {
                var startOfDay = parsedDate.Date.ToUniversalTime();
                var endOfDay = parsedDate.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
                filter = Builders<Trip>.Filter.And(
                    Builders<Trip>.Filter.Gte(t => t.DepartureTime, startOfDay),
                    Builders<Trip>.Filter.Lte(t => t.DepartureTime, endOfDay));
            }

            var trips = await _dbContext.Trips
                .Find(filter)
                .SortByDescending(t => t.DepartureTime)
                .ToListAsync();

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

            var trip = await _dbContext.Trips.Find(t => t.Id == id).FirstOrDefaultAsync();
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
            var routes = await _dbContext.BusRoutes
                .Find(_ => true)
                .SortBy(r => r.DeparturePoint)
                .ThenBy(r => r.DestinationPoint)
                .ToListAsync();
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
            return routes.Select(r => new RouteOptionViewModel
            {
                Id = r.Id,
                DeparturePoint = r.DeparturePoint,
                DestinationPoint = r.DestinationPoint,
                DistanceKm = r.DistanceKm
            }).ToList();
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
                TempData["ErrorMessage"] = "Tuyến đường, biển số xe và giá vé là bắt buộc.";
                return RedirectToAction(nameof(PriceConfig));
            }

            if (baseFare > 50_000_000)
            {
                TempData["ErrorMessage"] = "Giá vé không hợp lệ (tối đa 50.000.000đ).";
                return RedirectToAction(nameof(PriceConfig));
            }

            if (arrivalTime <= departureTime)
            {
                TempData["ErrorMessage"] = "Giờ đến phải sau giờ đi.";
                return RedirectToAction(nameof(PriceConfig));
            }

            var route = await _dbContext.BusRoutes.Find(r => r.Id == routeId).FirstOrDefaultAsync();
            if (route == null)
            {
                TempData["ErrorMessage"] = "Tuyến đường không tồn tại trong hệ thống.";
                return RedirectToAction(nameof(PriceConfig));
            }

            var departurePoint = route.DeparturePoint;
            var destinationPoint = route.DestinationPoint;

            var bus = await _dbContext.Buses.Find(b => b.Id == busId).FirstOrDefaultAsync();
            if (bus == null)
            {
                TempData["ErrorMessage"] = "Xe không tồn tại trong hệ thống.";
                return RedirectToAction(nameof(PriceConfig));
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
                    TempData["ErrorMessage"] = "Xe chưa có sơ đồ ghế. Cấu hình layout trong BusClasses trước.";
                    return RedirectToAction(nameof(PriceConfig));
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
                var existing = await _dbContext.Trips.Find(t => t.Id == tripId).FirstOrDefaultAsync();
                if (existing == null)
                {
                    TempData["ErrorMessage"] = "Chuyến xe không tồn tại.";
                    return RedirectToAction(nameof(PriceConfig));
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
                        TempData["ErrorMessage"] = "Không thể đổi xe vì chuyến đã có ghế đặt hoặc giữ chỗ.";
                        return RedirectToAction(nameof(PriceConfig));
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

            var hasBookings = await _dbContext.Bookings
                .Find(b => b.TripId == id && b.BookingStatus == "Completed")
                .AnyAsync();

            if (hasBookings)
            {
                TempData["ErrorMessage"] = "Không thể xóa chuyến xe đã có vé đặt thành công.";
                return RedirectToAction(nameof(PriceConfig));
            }

            var result = await _dbContext.Trips.DeleteOneAsync(t => t.Id == id);
            TempData[result.DeletedCount > 0 ? "SuccessMessage" : "ErrorMessage"] =
                result.DeletedCount > 0 ? "Xóa chuyến xe thành công!" : "Không tìm thấy chuyến xe để xóa.";

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
                Builders<Trip>.Filter.In(t => t.RouteId, routeIds),
                Builders<Trip>.Filter.In(t => t.BusId, matchingBusIds));

            await _dbContext.Trips.UpdateManyAsync(
                filter,
                Builders<Trip>.Update
                    .Set(t => t.BaseFare, basePrice)
                    .Set(t => t.UpdatedAt, DateTime.UtcNow));
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
