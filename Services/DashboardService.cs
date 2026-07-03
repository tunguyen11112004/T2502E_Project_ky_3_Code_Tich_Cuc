using Bus_ticket.Data;
using Bus_ticket.Models;
using Bus_ticket.ViewModels;
using MongoDB.Driver;

namespace Bus_ticket.Services;

public class DashboardService
{
    private readonly ApplicationDbContext _dbContext;

    public DashboardService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RouteRevenueReportViewModel> GetRouteRevenueReportAsync(DateTime fromDate, DateTime toDate)
    {
        var from = fromDate.Date;
        var to = toDate.Date.AddDays(1).AddTicks(-1);

        var fromUtc = from.ToUniversalTime();
        var toUtc = to.ToUniversalTime();

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

        var reportItems = paidCompletedBookings
            .GroupBy(b => b.TripId)
            .Select(group =>
            {
                var trip = trips.FirstOrDefault(t => t.Id == group.Key);
                var route = routes.FirstOrDefault(r => r.Id == trip?.RouteId);

                var routeId = route?.Id ?? "unknown";
                var routeName = route == null
                    ? "Không xác định"
                    : $"{route.DeparturePoint} - {route.DestinationPoint}";

                return new
                {
                    RouteId = routeId,
                    RouteName = routeName,
                    Bookings = group.ToList()
                };
            })
            .GroupBy(x => x.RouteId)
            .Select(group =>
            {
                var first = group.First();

                var allBookingsOfRoute = group
                    .SelectMany(x => x.Bookings)
                    .ToList();

                return new RouteRevenueItemViewModel
                {
                    RouteId = first.RouteId,
                    RouteName = first.RouteName,
                    TotalBookings = allBookingsOfRoute.Count,
                    TotalTickets = allBookingsOfRoute.Sum(b => b.Passengers?.Count ?? 0),
                    TotalRevenue = allBookingsOfRoute.Sum(b => b.FinalAmount)
                };
            })
            .OrderByDescending(x => x.TotalRevenue)
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

    private static bool IsCompletedBooking(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        var value = status.Trim().ToLowerInvariant();

        return value == "completed"
               || value == "complete"
               || value == "success"
               || value == "successful"
               || value == "confirmed";
    }

    private static bool IsPaidBooking(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        var value = status.Trim().ToLowerInvariant();

        return value == "paid"
               || value == "success"
               || value == "successful"
               || value == "completed";
    }

    // Thêm class bổ trợ để đóng gói dữ liệu phân trang
    public class PagedSeatAnalyticsResult
    {
        public List<SeatAnalyticsViewModel> Items { get; set; } = new();
        public int TotalItems { get; set; }
    }

    public async Task<PagedSeatAnalyticsResult> GetSeatAnalyticsReportAsync(DateTime fromDate, DateTime toDate,
        int pageNumber, int pageSize)
    {
        var fromUtc = fromDate.Date.ToUniversalTime();
        var toUtc = toDate.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

        // 1. Lấy tất cả trips trong khoảng thời gian để phân trang ở bộ nhớ (Memory) 
        // hoặc có thể tối ưu Skip/Take trực tiếp trên Mongo Driver nếu muốn.
        var trips = await _dbContext.Trips
            .Find(t => t.DepartureTime >= fromUtc && t.DepartureTime <= toUtc)
            .ToListAsync();

        var routes = await _dbContext.BusRoutes.Find(_ => true).ToListAsync();
        var buses = await _dbContext.Buses.Find(_ => true).ToListAsync();

        var allItems = trips.Select(t =>
            {
                var route = routes.FirstOrDefault(r => r.Id == t.RouteId);
                var bus = buses.FirstOrDefault(b => b.Id == t.BusId);

                int totalSeats = t.RealtimeSeats?.Count ?? 0;
                int bookedSeats = t.RealtimeSeats?.Count(s => s.Status == "Booked") ?? 0;

                double occupancyRate = totalSeats > 0
                    ? Math.Round(((double)bookedSeats / totalSeats) * 100, 2)
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
            .OrderByDescending(x => x.DepartureTime)
            .ToList();

        // 2. Thực hiện phân trang (Pagination) bằng LINQ
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

    public async Task<List<BranchCancellationViewModel>> GetBranchCancellationReportAsync(DateTime fromDate,
        DateTime toDate)
    {
        var from = fromDate.Date.ToUniversalTime();
        var to = toDate.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

        // 1. Lấy toàn bộ dữ liệu cần thiết
        var operators = await _dbContext.BusOperators.Find(_ => true).ToListAsync();
        var allBuses = await _dbContext.Buses.Find(_ => true).ToListAsync();

        // Lấy các chuyến đi trong khoảng thời gian, lọc luôn trạng thái (ví dụ: 'Cancelled' hoặc 'Canceled')
        var allTrips = await _dbContext.Trips
            .Find(t => t.DepartureTime >= from && t.DepartureTime <= to)
            .ToListAsync();

        // 2. Nhóm dữ liệu theo Nhà xe
        return operators.Select(op =>
            {
                // Lấy danh sách ID xe thuộc nhà xe này
                var opBusIds = allBuses.Where(b => b.OperatorId == op.Id).Select(b => b.Id).ToList();

                // Lọc các chuyến đi thuộc nhà xe này
                var opTrips = allTrips.Where(t => opBusIds.Contains(t.BusId)).ToList();

                return new BranchCancellationViewModel
                {
                    BranchId = op.Id,
                    BranchName = op.OperatorName,
                    TotalTrips = opTrips.Count,
                    // Đếm các chuyến có status là "Cancelled" (Hãy kiểm tra chính xác chữ trong DB của bạn)
                    CanceledTrips = opTrips.Count(t => t.Status == "Cancelled")
                };
            })
            .Where(r => r.TotalTrips > 0)
            .ToList();
    }


    public async Task<List<OperatorRevenueViewModel>> GetOperatorRevenueReportAsync(DateTime fromDate, DateTime toDate, string? currentOperatorId = null)
{
    var fromUtc = fromDate.Date.ToUniversalTime();
    var toUtc = toDate.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

    // 1. Lấy danh sách Nhà xe cần tính toán
    var operatorQuery = _dbContext.BusOperators.Find(_ => true);
    if (!string.IsNullOrEmpty(currentOperatorId))
    {
        operatorQuery = _dbContext.BusOperators.Find(o => o.Id == currentOperatorId);
    }
    var operators = await operatorQuery.ToListAsync();
    var operatorIds = operators.Select(o => o.Id).ToList();

    // 2. Lấy danh sách Xe thuộc các Nhà xe này (Giả định trường liên kết trong class Bus của bạn tên là OperatorId)
    // Bạn hãy check lại nếu class Bus đặt tên trường này khác (ví dụ: BusOperatorId) thì sửa lại nhé!
    var buses = await _dbContext.Buses
        .Find(b => operatorIds.Contains(b.OperatorId))
        .ToListAsync();
    var busIds = buses.Select(b => b.Id).ToList();

    // 3. Lấy danh sách đăng ký chi nhánh ứng với các xe trên
    var busBranches = await _dbContext.BusBranches
        .Find(bb => busIds.Contains(bb.BusId))
        .ToListAsync();
    var validBranchIds = busBranches.Select(bb => bb.BranchId).Distinct().ToList();

    // 4. Tải danh sách Booking hợp lệ dựa trên chi nhánh và thời gian
    var bookings = await _dbContext.Bookings
        .Find(b => b.BookingTime >= fromUtc && 
                   b.BookingTime <= toUtc && 
                   b.PaymentStatus == "Paid" && 
                   validBranchIds.Contains(b.BranchId))
        .ToListAsync();

    // 5. Tính tổng doanh thu nhóm theo từng Nhà xe
    return operators.Select(op =>
        {
            // Tìm các xe thuộc về nhà xe này
            var opBusIds = buses.Where(b => b.OperatorId == op.Id).Select(b => b.Id).ToList();

            // Tìm các chi nhánh liên kết với các xe đó
            var opBranchIds = busBranches.Where(bb => opBusIds.Contains(bb.BusId)).Select(bb => bb.BranchId).ToList();
    
            // Lọc ra các booking thuộc về các chi nhánh của nhà xe này
            var opBookings = bookings.Where(b => opBranchIds.Contains(b.BranchId)).ToList();

            return new OperatorRevenueViewModel
            {
                OperatorId = op.Id,
                OperatorName = op.OperatorName,
                TotalRevenue = opBookings.Sum(b => b.FinalAmount),
                TotalBookings = opBookings.Count
            };
        })
        .OrderByDescending(r => r.TotalRevenue)
        .ToList();
}
}