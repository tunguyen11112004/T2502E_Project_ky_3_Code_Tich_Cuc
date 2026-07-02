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

    /*public async Task<List<BranchCancellationViewModel>> GetBranchCancellationReportAsync(DateTime fromDate,
        DateTime toDate)
    {
        var fromUtc = fromDate.Date.ToUniversalTime();
        var toUtc = toDate.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

        // Lấy dữ liệu cần thiết từ MongoDB
        var trips = await _dbContext.Trips
            .Find(t => t.DepartureTime >= fromUtc && t.DepartureTime <= toUtc)
            .ToListAsync();

        var buses = await _dbContext.Buses.Find(_ => true).ToListAsync();

        // Giả định collection chi nhánh của bạn tên là Branches hoặc BusBranches, bạn chỉnh lại cho đúng nhé
        var branches = await _dbContext.Branches.Find(_ => true).ToListAsync();

        // Gom nhóm và tính toán bằng LINQ
        var report = branches.Select(branch =>
            {
                // 1. Tìm tất cả các xe thuộc chi nhánh/nhà xe này
                var busIds = buses.Where(b => b.BranchId == branch.Id).Select(b => b.Id).ToList();

                // 2. Lọc ra các chuyến xe sử dụng các xe thuộc nhà xe này
                var branchTrips = trips.Where(t => busIds.Contains(t.BusId)).ToList();

                // 3. Đếm tổng số chuyến và số chuyến bị hủy ("Canceled")
                int totalTrips = branchTrips.Count;
                int canceledTrips =
                    branchTrips.Count(t => t.Status != null && t.Status.Trim().ToLowerInvariant() == "canceled");

                return new BranchCancellationViewModel
                {
                    BranchId = branch.Id,
                    BranchName =
                        branch.Name, // Bạn kiểm tra lại trường tên nhà xe trong Branch.cs là Name hay BranchName nhé
                    TotalTrips = totalTrips,
                    CanceledTrips = canceledTrips
                };
            })
            // Chỉ lấy những nhà xe có phát sinh chuyến đi trong khoảng thời gian lọc
            .Where(b => b.TotalTrips > 0)
            // Sắp xếp nhà xe có tỷ lệ hủy chuyến CAO nhất lên đầu
            .OrderByDescending(b => b.CancellationRate)
            .ToList();

        return report;
    }*/
}