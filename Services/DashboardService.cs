using Bus_ticket.Data;
using Bus_ticket.Models;
using Bus_ticket.ViewModels;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bus_ticket.Services
{
    public class DashboardService
    {
        private readonly ApplicationDbContext _dbContext;

        public DashboardService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public class PagedSeatAnalyticsResult
        {
            public List<SeatAnalyticsViewModel> Items { get; set; } = new();
            public int TotalItems { get; set; }
        }

        public async Task<RouteRevenueReportViewModel> GetRouteRevenueReportAsync(DateTime fromDate, DateTime toDate)
        {
            var (fromUtc, toUtc) = ToUtcDateRange(fromDate, toDate);

            var bookings = await _dbContext.Bookings
                .Find(b => b.BookingTime >= fromUtc && b.BookingTime <= toUtc)
                .ToListAsync();

            var paidCompletedBookings = bookings
                .Where(b => IsCompletedBooking(b.BookingStatus) && IsPaidBooking(b.PaymentStatus))
                .ToList();

            var trips = await _dbContext.Trips
                .Find(_ => true)
                .ToListAsync();

            var routes = await _dbContext.BusRoutes
                .Find(_ => true)
                .ToListAsync();

            var tripDictionary = trips
                .Where(t => !string.IsNullOrWhiteSpace(t.Id))
                .GroupBy(t => t.Id)
                .ToDictionary(g => g.Key, g => g.First());

            var routeDictionary = routes
                .Where(r => !string.IsNullOrWhiteSpace(r.Id))
                .GroupBy(r => r.Id)
                .ToDictionary(g => g.Key, g => g.First());

            var reportItems = paidCompletedBookings
                .Select(booking =>
                {
                    tripDictionary.TryGetValue(booking.TripId, out var trip);

                    var routeId = trip?.RouteId ?? "unknown";

                    routeDictionary.TryGetValue(routeId, out var route);

                    var routeName = route == null
                        ? "Không xác định"
                        : $"{route.DeparturePoint} - {route.DestinationPoint}";

                    return new
                    {
                        RouteId = routeId,
                        RouteName = routeName,
                        Booking = booking
                    };
                })
                .GroupBy(x => new
                {
                    x.RouteId,
                    x.RouteName
                })
                .Select(group =>
                {
                    var routeBookings = group
                        .Select(x => x.Booking)
                        .ToList();

                    return new RouteRevenueItemViewModel
                    {
                        RouteId = group.Key.RouteId,
                        RouteName = group.Key.RouteName,
                        TotalBookings = routeBookings.Count,
                        TotalTickets = routeBookings.Sum(GetTicketCount),
                        TotalRevenue = routeBookings.Sum(b => b.FinalAmount)
                    };
                })
                .OrderByDescending(x => x.TotalRevenue)
                .ThenByDescending(x => x.TotalTickets)
                .ThenByDescending(x => x.TotalBookings)
                .ThenBy(x => x.RouteName)
                .ToList();

            var grandTotalRevenue = reportItems.Sum(x => x.TotalRevenue);
            var grandTotalBookings = reportItems.Sum(x => x.TotalBookings);
            var grandTotalTickets = reportItems.Sum(x => x.TotalTickets);

            foreach (var item in reportItems)
            {
                item.Percentage = grandTotalRevenue > 0
                    ? Math.Round((double)(item.TotalRevenue / grandTotalRevenue * 100), 2)
                    : 0;
            }

            return new RouteRevenueReportViewModel
            {
                FromDate = fromDate.Date,
                ToDate = toDate.Date,
                GrandTotalRevenue = grandTotalRevenue,
                GrandTotalBookings = grandTotalBookings,
                GrandTotalTickets = grandTotalTickets,
                Items = reportItems
            };
        }

        public async Task<PagedSeatAnalyticsResult> GetSeatAnalyticsReportAsync(
            DateTime fromDate,
            DateTime toDate,
            int pageNumber,
            int pageSize)
        {
            var (fromUtc, toUtc) = ToUtcDateRange(fromDate, toDate);
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Max(1, pageSize);

            var trips = await _dbContext.Trips
                .Find(t => t.DepartureTime >= fromUtc && t.DepartureTime <= toUtc)
                .ToListAsync();

            var routes = await _dbContext.BusRoutes.Find(_ => true).ToListAsync();
            var buses = await _dbContext.Buses.Find(_ => true).ToListAsync();

            var allItems = trips.Select(t =>
                {
                    var route = routes.FirstOrDefault(r => r.Id == t.RouteId);
                    var bus = buses.FirstOrDefault(b => b.Id == t.BusId);

                    var totalSeats = t.RealtimeSeats?.Count ?? 0;
                    var bookedSeats = t.RealtimeSeats?.Count(s => IsBookedSeat(s.Status)) ?? 0;

                    var occupancyRate = totalSeats > 0
                        ? Math.Round((double)bookedSeats / totalSeats * 100, 2)
                        : 0;

                    return new SeatAnalyticsViewModel
                    {
                        TripCode = t.TripCode ?? "N/A",
                        RouteName = route != null ? $"{route.DeparturePoint} - {route.DestinationPoint}" : "Không xác định",
                        LicensePlate = bus?.LicensePlate ?? "Chưa gán xe",
                        DepartureTime = t.DepartureTime.ToLocalTime(),
                        TotalSeats = totalSeats,
                        BookedSeats = bookedSeats,
                        OccupancyRate = occupancyRate,
                        Status = t.Status
                    };
                })
                .OrderByDescending(x => x.OccupancyRate)
                .ThenByDescending(x => x.BookedSeats)
                .ThenBy(x => x.DepartureTime)
                .ToList();

            var totalItems = allItems.Count;
            var pagedItems = allItems
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedSeatAnalyticsResult
            {
                Items = pagedItems,
                TotalItems = totalItems
            };
        }

        public async Task<List<BranchCancellationViewModel>> GetBranchCancellationReportAsync(DateTime fromDate, DateTime toDate)
        {
            var (fromUtc, toUtc) = ToUtcDateRange(fromDate, toDate);

            var operators = await _dbContext.BusOperators.Find(_ => true).ToListAsync();
            var buses = await _dbContext.Buses.Find(_ => true).ToListAsync();
            var trips = await _dbContext.Trips
                .Find(t => t.DepartureTime >= fromUtc && t.DepartureTime <= toUtc)
                .ToListAsync();

            return operators.Select(op =>
                {
                    var opBusIds = buses
                        .Where(b => b.OperatorId == op.Id)
                        .Select(b => b.Id)
                        .ToHashSet();

                    var opTrips = trips
                        .Where(t => opBusIds.Contains(t.BusId))
                        .ToList();

                    return new BranchCancellationViewModel
                    {
                        BranchId = op.Id,
                        BranchName = op.OperatorName,
                        TotalTrips = opTrips.Count,
                        CanceledTrips = opTrips.Count(t => IsCancelled(t.Status))
                    };
                })
                .Where(r => r.TotalTrips > 0)
                .OrderByDescending(r => r.CancellationRate)
                .ThenByDescending(r => r.CanceledTrips)
                .ThenByDescending(r => r.TotalTrips)
                .ThenBy(r => r.BranchName)
                .ToList();
        }

        public async Task<List<OperatorRevenueViewModel>> GetOperatorRevenueReportAsync(
            DateTime fromDate,
            DateTime toDate,
            string? currentOperatorId = null)
        {
            var (fromUtc, toUtc) = ToUtcDateRange(fromDate, toDate);

            var operators = await _dbContext.BusOperators.Find(_ => true).ToListAsync();
            if (!string.IsNullOrWhiteSpace(currentOperatorId))
            {
                operators = operators
                    .Where(o => o.Id == currentOperatorId)
                    .ToList();
            }

            var operatorIds = operators.Select(o => o.Id).ToHashSet();
            var buses = await _dbContext.Buses
                .Find(b => b.OperatorId != null && operatorIds.Contains(b.OperatorId))
                .ToListAsync();
            var busIds = buses.Select(b => b.Id).ToHashSet();

            var trips = await _dbContext.Trips
                .Find(t => busIds.Contains(t.BusId))
                .ToListAsync();
            var tripIds = trips.Select(t => t.Id).ToHashSet();

            var bookings = await _dbContext.Bookings
                .Find(b => b.BookingTime >= fromUtc
                           && b.BookingTime <= toUtc
                           && tripIds.Contains(b.TripId))
                .ToListAsync();

            var validBookings = bookings
                .Where(b => IsCompletedBooking(b.BookingStatus) && IsPaidBooking(b.PaymentStatus))
                .ToList();

            return operators.Select(op =>
                {
                    var opBusIds = buses
                        .Where(b => b.OperatorId == op.Id)
                        .Select(b => b.Id)
                        .ToHashSet();

                    var opTripIds = trips
                        .Where(t => opBusIds.Contains(t.BusId))
                        .Select(t => t.Id)
                        .ToHashSet();

                    var opBookings = validBookings
                        .Where(b => opTripIds.Contains(b.TripId))
                        .ToList();

                    return new OperatorRevenueViewModel
                    {
                        OperatorId = op.Id,
                        OperatorName = op.OperatorName,
                        TotalRevenue = opBookings.Sum(b => b.FinalAmount),
                        TotalBookings = opBookings.Count
                    };
                })
                .Where(r => r.TotalRevenue > 0 || r.TotalBookings > 0)
                .OrderByDescending(r => r.TotalRevenue)
                .ThenByDescending(r => r.TotalBookings)
                .ThenBy(r => r.OperatorName)
                .ToList();
        }


        public async Task<DashboardRevenueViewModel> GetSystemTotalRevenueAsync(DateTime fromDate, DateTime toDate)
        {
            var (fromUtc, toUtc) = ToUtcDateRange(fromDate, toDate);

            var bookings = await _dbContext.Bookings
                .Find(b => b.BookingTime >= fromUtc && b.BookingTime <= toUtc)
                .ToListAsync();

            var validBookings = bookings
                .Where(b => IsCompletedBooking(b.BookingStatus) && IsPaidBooking(b.PaymentStatus))
                .ToList();

            var tripIds = validBookings.Select(b => b.TripId).Distinct().ToList();
            var trips = await _dbContext.Trips.Find(t => tripIds.Contains(t.Id)).ToListAsync();
            
            var busIds = trips.Select(t => t.BusId).Distinct().ToList();
            var buses = await _dbContext.Buses.Find(b => busIds.Contains(b.Id)).ToListAsync();

            var tableData = validBookings.Select(b => 
            {
                var trip = trips.FirstOrDefault(t => t.Id == b.TripId);
                
                // Sử dụng số ghế thực tế từ Trip để tránh lỗi thiếu thuộc tính TotalSeats trong Model Bus
                var totalSeats = trip?.RealtimeSeats?.Count ?? 0;
                
                var busClass = "Tiêu chuẩn"; 
                if (totalSeats > 0) 
                {
                    busClass = totalSeats > 30 ? "Giường Nằm" : "Limousine"; 
                }

                return new TransactionDetailDto
                {
                    BookingCode = b.Id, 
                    CustomerName = "Khách Hàng", // Tuỳ chỉnh lấy tên nếu có trong model booking
                    BusClass = busClass,
                    PaymentDate = b.BookingTime.ToLocalTime(),
                    Amount = b.FinalAmount
                };
            })
            .OrderByDescending(x => x.PaymentDate)
            .ToList();

            var chartData = tableData
                .GroupBy(x => x.BusClass)
                .Select(g => new RevenueByCategoryDto
                {
                    Category = g.Key,
                    TotalRevenue = g.Sum(x => x.Amount)
                }).ToList();

            return new DashboardRevenueViewModel
            {
                TableData = tableData,
                ChartData = chartData
            };
        }


        public async Task<List<SeatAnalyticsViewModel>> GetSoldOutTripsAsync(DateTime fromDate, DateTime toDate)
        {
            var (fromUtc, toUtc) = ToUtcDateRange(fromDate, toDate);

            var trips = await _dbContext.Trips
                .Find(t => t.DepartureTime >= fromUtc && t.DepartureTime <= toUtc)
                .ToListAsync();

            var routes = await _dbContext.BusRoutes
                .Find(_ => true)
                .ToListAsync();

            var buses = await _dbContext.Buses
                .Find(_ => true)
                .ToListAsync();

            var busClasses = await _dbContext.BusClasses
                .Find(_ => true)
                .ToListAsync();

            var operators = await _dbContext.BusOperators
                .Find(_ => true)
                .ToListAsync();

            var routeDictionary = routes
                .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First());

            var busDictionary = buses
                .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First());

            var busClassDictionary = busClasses
                .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First());

            var operatorDictionary = operators
                .Where(x => !string.IsNullOrWhiteSpace(x.Id))
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First());

            var soldOutTrips = trips
                .Select(t =>
                {
                    routeDictionary.TryGetValue(t.RouteId, out var route);
                    busDictionary.TryGetValue(t.BusId, out var bus);

                    BusClass? busClass = null;
                    if (!string.IsNullOrWhiteSpace(bus?.BusClassId))
                    {
                        busClassDictionary.TryGetValue(bus.BusClassId, out busClass);
                    }

                    BusOperator? busOperator = null;
                    if (!string.IsNullOrWhiteSpace(bus?.OperatorId))
                    {
                        operatorDictionary.TryGetValue(bus.OperatorId, out busOperator);
                    }

                    var totalSeats = t.RealtimeSeats?.Count ?? 0;
                    var bookedSeats = t.RealtimeSeats?.Count(s => IsBookedSeat(s.Status)) ?? 0;

                    var occupancyRate = totalSeats > 0
                        ? Math.Round((double)bookedSeats / totalSeats * 100, 2)
                        : 0;

                    return new SeatAnalyticsViewModel
                    {
                        TripCode = t.TripCode ?? "N/A",
                        RouteName = route != null
                            ? $"{route.DeparturePoint} - {route.DestinationPoint}"
                            : "Không xác định",
                        BusType = busClass?.BusType ?? "Không xác định",
                        OperatorName = busOperator?.OperatorName ?? "Không xác định",
                        LicensePlate = bus?.LicensePlate ?? "Chưa gán xe",
                        DepartureTime = t.DepartureTime.ToLocalTime(),
                        TotalSeats = totalSeats,
                        BookedSeats = bookedSeats,
                        OccupancyRate = occupancyRate,
                        Status = t.Status
                    };
                })
                .Where(x => x.TotalSeats > 0 && x.OccupancyRate >= 100)
                .OrderByDescending(x => x.DepartureTime)
                .ThenBy(x => x.RouteName)
                .ToList();

            return soldOutTrips;
        }

        // ========================================================
        // TASK 14: HỦY CHUYẾN XE VÀ CẬP NHẬT VÉ (CASCADE UPDATE)
        // ========================================================
        public async Task<bool> CancelTripAsync(string tripId)
        {
            // 1. Tìm và cập nhật trạng thái chuyến xe (Trips) sử dụng toán tử $set
            var tripFilter = Builders<Trip>.Filter.Eq(t => t.Id, tripId);
            var tripUpdate = Builders<Trip>.Update.Set(t => t.Status, "Cancelled");
            
            var tripResult = await _dbContext.Trips.UpdateOneAsync(tripFilter, tripUpdate);
            
            // Nếu không tìm thấy chuyến để update -> Hủy thất bại
            if (tripResult.MatchedCount == 0) return false;

            // 2. Cập nhật liên đới (Cascade Update) hàng loạt vé (Booking) sang Cancelled 
            // và lưu vết chờ hoàn tiền (RefundPending)
            var bookingFilter = Builders<Booking>.Filter.Eq(b => b.TripId, tripId);
            var bookingUpdate = Builders<Booking>.Update
                .Set(b => b.BookingStatus, "Cancelled") 
                .Set(b => b.IsRefundPending, true); // Đẩy dữ liệu vào danh sách chờ đối soát

            await _dbContext.Bookings.UpdateManyAsync(bookingFilter, bookingUpdate);

            return true;
        }
        
        private static (DateTime FromUtc, DateTime ToUtc) ToUtcDateRange(DateTime fromDate, DateTime toDate)
        {
            var from = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc);
            var to = DateTime.SpecifyKind(toDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            return (from, to);
        }

        private static int GetTicketCount(Booking booking)
        {
            return booking.Passengers?.Count > 0 ? booking.Passengers.Count : 1;
        }

        private static bool IsCompletedBooking(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;

            var value = status.Trim().ToLowerInvariant();
            return value == "completed"
                   || value == "complete"
                   || value == "success"
                   || value == "successful"
                   || value == "confirmed";
        }

        private static bool IsPaidBooking(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;

            var value = status.Trim().ToLowerInvariant();
            return value == "paid"
                   || value == "success"
                   || value == "successful"
                   || value == "completed";
        }

        private static bool IsCancelled(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;

            var value = status.Trim().ToLowerInvariant();
            return value == "cancelled"
                   || value == "canceled"
                   || value == "cancel"
                   || value == "refunded"
                   || value == "refund";
        }

        private static bool IsBookedSeat(string? status)
        {
            return string.Equals(status?.Trim(), "Booked", StringComparison.OrdinalIgnoreCase);
        }
    }
}