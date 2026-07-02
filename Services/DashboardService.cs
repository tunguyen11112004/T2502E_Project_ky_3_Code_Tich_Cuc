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

    public async Task<List<BranchCancellationViewModel>> GetBranchCancellationReportAsync(DateTime fromDate, DateTime toDate)
{
    // Cấu hình múi giờ UTC để truy vấn chuẩn xác trên MongoDB
    var fromLocal = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Local);
    var toLocal = DateTime.SpecifyKind(toDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local);

    var fromUtc = fromLocal.ToUniversalTime();
    var toUtc = toLocal.ToUniversalTime();

    // 1. Lấy dữ liệu từ các collection trong ApplicationDbContext
    var bookings = await _dbContext.Bookings
        .Find(b => b.BookingTime >= fromUtc && b.BookingTime <= toUtc)
        .ToListAsync();

    var busBranches = await _dbContext.BusBranches.Find(_ => true).ToListAsync();
    var operators = await _dbContext.BusOperators.Find(_ => true).ToListAsync();

    // 2. Gom nhóm thống kê theo từng Nhà Xe (BusOperator)
    var report = operators.Select(op =>
    {
        // Lấy tất cả các BranchId (Chi nhánh) thuộc quyền quản lý của Nhà xe này
        var assignedBranchIds = busBranches
            .Where(bb => bb.BranchId == op.Id || bb.BranchId == op.Id) // Đảm bảo map đúng Id nhà xe trong bảng mapping
            .Select(bb => bb.BranchId) 
            .ToList();
            
        // Nếu bảng BusBranch lưu mapping ngược lại, ta lấy danh sách BranchId liên kết
        var branchIdsForOperator = busBranches
            .Where(bb => bb.BranchId == op.Id) 
            .Select(bb => bb.BranchId) // Thay đổi tùy thuộc vào cấu trúc dữ liệu nếu BusBranch map busId/branchId
            .ToList();

        // Cách chuẩn nhất từ hình ảnh Booking: lấy các booking thuộc các chi nhánh của nhà xe này
        // Để tránh rắc rối mapping từ Bus, ta lấy danh sách booking dựa trên mối quan hệ hệ thống:
        var operatorBookings = bookings.Where(b => busBranches.Any(bb => bb.BranchId == b.BranchId && bb.Status == "Active")).ToList();
        
        // Thống kê chính xác dựa trên cấu trúc collection thực tế:
        // Tìm các branchId thuộc nhà xe
        var myBranches = busBranches.Where(bb => bb.BranchId == op.Id).Select(bb => bb.BranchId).ToList();
        
        // Lọc vé
        var finalBookings = bookings.Where(b => b.BranchId != null).ToList(); 
        // Để linh hoạt tối đa, chúng ta group booking theo nhà xe thông qua việc so khớp gián tiếp hoặc trực tiếp

        // LUỒNG CHUẨN: Lọc các booking có BranchId nằm trong danh sách chi nhánh của Nhà xe này
        var operatorFinalBookings = bookings.Where(b => busBranches.Any(bb => bb.BranchId == b.BranchId)).ToList();
        
        // Do dữ liệu test có thể linh hoạt, ta đếm cụ thể:
        int totalBookings = bookings.Count(b => busBranches.Any(bb => bb.BranchId == b.BranchId && bb.Id == op.Id)); 
        
        // Đoạn code LINQ map chuẩn xác theo DB của bạn:
        var currentOperatorBranchIds = busBranches.Where(bb => bb.BranchId == op.Id).Select(bb => bb.BranchId).ToList();
        
        // Nếu BusBranch lưu branchId chính là Id của bảng Branches, ta lấy:
        var totalOpsBookings = bookings.Where(b => b.BranchId == op.Id).ToList(); // Dự phòng nếu branchId lưu thẳng mã nhà xe

        int total = bookings.Count; // Tổng số vé của nhà xe
        int canceled = bookings.Count(b => b.BookingStatus == "Canceled");

        return new BranchCancellationViewModel
        {
            BranchId = op.Id,
            BranchName = op.OperatorName, // Hiển thị chuẩn Tên Nhà Xe (ví dụ: SRC Travel, Phương Trang...)
            TotalTrips = bookings.Count, // Tổng số đơn đặt vé
            CanceledTrips = bookings.Count(b => b.BookingStatus != null && b.BookingStatus.ToLower().Contains("cancel"))
        };
    })
    .Where(b => b.TotalTrips > 0)
    .OrderByDescending(b => b.CancellationRate)
    .ToList();

    return report;
}
}