using Bus_ticket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;

namespace Bus_ticket.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly DashboardService _dashboardService;

    public DashboardController(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> RouteRevenue(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.Today.AddDays(-7);
        var to = toDate ?? DateTime.Today;

        if (from > to)
        {
            TempData["ErrorMessage"] = "Ngày bắt đầu không được lớn hơn ngày kết thúc.";
            var temp = from;
            from = to;
            to = temp;
        }

        var model = await _dashboardService.GetRouteRevenueReportAsync(from, to);

        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> ExportRouteRevenue(DateTime fromDate, DateTime toDate)
    {
        if (fromDate > toDate)
        {
            var temp = fromDate;
            fromDate = toDate;
            toDate = temp;
        }

        var model = await _dashboardService.GetRouteRevenueReportAsync(fromDate, toDate);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Route Revenue");

        worksheet.Cell(1, 1).Value = "ROUTE REVENUE REPORT";
        worksheet.Range(1, 1, 1, 8).Merge();
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
        worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        worksheet.Cell(2, 1).Value = "From Date";
        worksheet.Cell(2, 2).Value = model.FromDate.ToString("yyyy-MM-dd");

        worksheet.Cell(3, 1).Value = "To Date";
        worksheet.Cell(3, 2).Value = model.ToDate.ToString("yyyy-MM-dd");

        worksheet.Cell(4, 1).Value = "Grand Total Revenue";
        worksheet.Cell(4, 2).Value = model.GrandTotalRevenue;
        worksheet.Cell(4, 2).Style.NumberFormat.Format = "#,##0";

        worksheet.Cell(5, 1).Value = "Grand Total Bookings";
        worksheet.Cell(5, 2).Value = model.GrandTotalBookings;

        worksheet.Cell(6, 1).Value = "Grand Total Tickets";
        worksheet.Cell(6, 2).Value = model.GrandTotalTickets;

        var headerRow = 8;

        worksheet.Cell(headerRow, 1).Value = "No.";
        worksheet.Cell(headerRow, 2).Value = "Route";
        worksheet.Cell(headerRow, 3).Value = "Successful Bookings";
        worksheet.Cell(headerRow, 4).Value = "Tickets Sold";
        worksheet.Cell(headerRow, 5).Value = "Revenue";
        worksheet.Cell(headerRow, 6).Value = "Percentage";
        worksheet.Cell(headerRow, 7).Value = "From Date";
        worksheet.Cell(headerRow, 8).Value = "To Date";

        var headerRange = worksheet.Range(headerRow, 1, headerRow, 8);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        var row = headerRow + 1;
        var index = 1;

        foreach (var item in model.Items)
        {
            worksheet.Cell(row, 1).Value = index;
            worksheet.Cell(row, 2).Value = item.RouteName;
            worksheet.Cell(row, 3).Value = item.TotalBookings;
            worksheet.Cell(row, 4).Value = item.TotalTickets;
            worksheet.Cell(row, 5).Value = item.TotalRevenue;
            worksheet.Cell(row, 6).Value = item.Percentage / 100;
            worksheet.Cell(row, 7).Value = model.FromDate.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 8).Value = model.ToDate.ToString("yyyy-MM-dd");

            row++;
            index++;
        }

        worksheet.Cell(row, 2).Value = "TOTAL";
        worksheet.Cell(row, 3).Value = model.GrandTotalBookings;
        worksheet.Cell(row, 4).Value = model.GrandTotalTickets;
        worksheet.Cell(row, 5).Value = model.GrandTotalRevenue;
        worksheet.Cell(row, 6).Value = model.GrandTotalRevenue > 0 ? 1 : 0;

        var totalRange = worksheet.Range(row, 1, row, 8);
        totalRange.Style.Font.Bold = true;
        totalRange.Style.Fill.BackgroundColor = XLColor.LightYellow;

        worksheet.Column(5).Style.NumberFormat.Format = "#,##0";
        worksheet.Column(6).Style.NumberFormat.Format = "0.00%";

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = stream.ToArray();

        var fileName = $"RouteRevenue_{model.FromDate:yyyyMMdd}_{model.ToDate:yyyyMMdd}.xlsx";

        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName
        );
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> SeatAnalytics(DateTime? fromDate, DateTime? toDate, int page = 1)
    {
        var from = fromDate ?? DateTime.Today.AddDays(-7);
        var to = toDate ?? DateTime.Today;

        if (from > to)
        {
            TempData["ErrorMessage"] = "Ngày bắt đầu không được lớn hơn ngày kết thúc.";
            var temp = from;
            from = to;
            to = temp;
        }

        int pageSize = 10; // Quy định 10 dòng trên 1 trang theo yêu cầu của bạn

        // Gọi hàm dịch vụ đã sửa đổi
        var result = await _dashboardService.GetSeatAnalyticsReportAsync(from, to, page, pageSize);

        // Tính toán thông tin phân trang chuyển qua View
        int totalPages = (int)Math.Ceiling((double)result.TotalItems / pageSize);

        ViewBag.FromDateValue = from;
        ViewBag.ToDateValue = to;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = result.TotalItems;

        return View(result.Items); // Model truyền xuống View bây giờ là List<SeatAnalyticsViewModel> đã cắt 10 phần tử
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> ExportSeatAnalytics(DateTime fromDate, DateTime toDate)
    {
        if (fromDate > toDate)
        {
            var temp = fromDate;
            fromDate = toDate;
            toDate = temp;
        }

        // 1. Truyền trang 1 và Int32.MaxValue (Kích thước tối đa) để lấy TUYỆT ĐỐI TOÀN BỘ dữ liệu xuất Excel
        var result = await _dashboardService.GetSeatAnalyticsReportAsync(fromDate, toDate, 1, int.MaxValue);

        // Lấy danh sách Items từ trong kết quả phân trang ra
        var model = result.Items;

        // 2. Khởi tạo file Excel bằng ClosedXML
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Seat Analytics");

        // Tiêu đề lớn
        worksheet.Cell(1, 1).Value = "BÁO CÁO TỶ LỆ LẤP ĐẦY GHẾ THEO CHUYẾN XE";
        worksheet.Range(1, 1, 1, 7).Merge();
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
        worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Thông tin khoảng thời gian lọc
        worksheet.Cell(3, 1).Value = "Từ ngày:";
        worksheet.Cell(3, 2).Value = fromDate.ToString("dd/MM/yyyy");
        worksheet.Cell(4, 1).Value = "Đến ngày:";
        worksheet.Cell(4, 2).Value = toDate.ToString("dd/MM/yyyy");

        // Header của bảng dữ liệu
        var headerRow = 6;
        worksheet.Cell(headerRow, 1).Value = "STT";
        worksheet.Cell(headerRow, 2).Value = "Mã Chuyến";
        worksheet.Cell(headerRow, 3).Value = "Tuyến Đường";
        worksheet.Cell(headerRow, 4).Value = "Biển Số Xe";
        worksheet.Cell(headerRow, 5).Value = "Giờ Khởi Hành";
        worksheet.Cell(headerRow, 6).Value = "Ghế Đã Bán / Tổng Ghế";
        worksheet.Cell(headerRow, 7).Value = "Tỷ Lệ Lấp Đầy";

        // Style cho Header bảng
        var headerRange = worksheet.Range(headerRow, 1, headerRow, 7);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        // Đổ dữ liệu vòng lặp vào từng dòng
        var row = headerRow + 1;
        var index = 1;

        foreach (var item in model)
        {
            worksheet.Cell(row, 1).Value = index;
            worksheet.Cell(row, 2).Value = item.TripCode;
            worksheet.Cell(row, 3).Value = item.RouteName;
            worksheet.Cell(row, 4).Value = item.LicensePlate;
            worksheet.Cell(row, 5).Value = item.DepartureTime.ToString("dd/MM/yyyy HH:mm");
            worksheet.Cell(row, 6).Value = $"{item.BookedSeats} / {item.TotalSeats}";

            // Lưu giá trị số thập phân để định dạng % trong Excel cho chuẩn
            worksheet.Cell(row, 7).Value = item.OccupancyRate / 100;

            row++;
            index++;
        }

        // Định dạng cột Tỷ lệ lấp đầy theo kiểu hiển thị % của Excel
        worksheet.Column(7).Style.NumberFormat.Format = "0.00%";

        // Tự động căn chỉnh độ rộng các cột cho vừa chữ
        worksheet.Columns().AdjustToContents();

        // Xuất file về trình duyệt Client
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = stream.ToArray();

        var fileName = $"TyleLapDayGhe_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";

        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName
        );
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> BranchCancellation(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.Today.AddDays(-30);
        var to = toDate ?? DateTime.Today;

        if (from > to)
        {
            TempData["ErrorMessage"] = "Ngày bắt đầu không được lớn hơn ngày kết thúc.";
            var temp = from;
            from = to;
            to = temp;
        }

        var model = await _dashboardService.GetBranchCancellationReportAsync(from, to);

        ViewBag.FromDateValue = from;
        ViewBag.ToDateValue = to;
        ViewBag.HighestCancellation = model.OrderByDescending(m => m.CancellationRate).FirstOrDefault();
        ViewBag.LowestCancellation = model.OrderBy(m => m.CancellationRate).FirstOrDefault();

        return View(model);
    }


    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> ExportBranchCancellation(DateTime fromDate, DateTime toDate)
    {
        if (fromDate > toDate)
        {
            var temp = fromDate;
            fromDate = toDate;
            toDate = temp;
        }

        var model = await _dashboardService.GetBranchCancellationReportAsync(fromDate, toDate);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Hủy Vé");

        worksheet.Cell(1, 1).Value = "BÁO CÁO THỐNG KÊ TỶ LỆ HỦY VÉ THEO NHÀ XE";
        worksheet.Range(1, 1, 1, 5).Merge();
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
        worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        worksheet.Cell(3, 1).Value = "Từ ngày:";
        worksheet.Cell(3, 2).Value = fromDate.ToString("dd/MM/yyyy");
        worksheet.Cell(4, 1).Value = "Đến ngày:";
        worksheet.Cell(4, 2).Value = toDate.ToString("dd/MM/yyyy");

        var headerRow = 6;
        worksheet.Cell(headerRow, 1).Value = "STT";
        worksheet.Cell(headerRow, 2).Value = "Tên Nhà Xe (Đối Tác Vận Hành)"; // Đã đổi tên cột
        worksheet.Cell(headerRow, 3).Value = "Tổng Số Vé Đặt";
        worksheet.Cell(headerRow, 4).Value = "Số Vé Bị Hủy";
        worksheet.Cell(headerRow, 5).Value = "Tỷ Lệ Hủy Vé";

        var headerRange = worksheet.Range(headerRow, 1, headerRow, 5);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        var row = headerRow + 1;
        var index = 1;

        foreach (var item in model)
        {
            worksheet.Cell(row, 1).Value = index;
            worksheet.Cell(row, 2).Value = item.BranchName; // Tên nhà xe lấy từ BusOperator
            worksheet.Cell(row, 3).Value = item.TotalTrips;
            worksheet.Cell(row, 4).Value = item.CanceledTrips;
            worksheet.Cell(row, 5).Value = item.CancellationRate / 100;

            row++;
            index++;
        }

        worksheet.Column(5).Style.NumberFormat.Format = "0.00%";
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = stream.ToArray();
        var debugData = model.Select(m => $"{m.BranchName}: {m.TotalTrips} - {m.CanceledTrips}");
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"ThongKeHuyVe_NhaXe_{fromDate:yyyyMMdd}.xlsx");
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee,Operator")]
    public async Task<IActionResult> OperatorRevenue(DateTime? fromDate, DateTime? toDate)
    {
        // Xác định ID nếu người dùng là Nhà xe
        string? operatorId = null;
        if (User.IsInRole("Operator"))
        {
            // Giả sử bạn lưu OperatorId trong Claim của User khi đăng nhập
            operatorId = User.FindFirst("OperatorId")?.Value;
        }

        var from = fromDate ?? DateTime.Today.AddMonths(-1);
        var to = toDate ?? DateTime.Today;

        var model = await _dashboardService.GetOperatorRevenueReportAsync(from, to, operatorId);

        ViewBag.From = from;
        ViewBag.To = to;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportOperatorRevenue(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.Today.AddMonths(-1);
        var to = toDate ?? DateTime.Today;
        var model = await _dashboardService.GetOperatorRevenueReportAsync(from, to);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("DoanhThuNhaXe");

        ws.Cell(1, 1).Value = "BÁO CÁO DOANH THU NHÀ XE";
        ws.Range(1, 1, 1, 4).Merge().Style.Font.Bold = true;

        ws.Cell(2, 1).Value = "STT";
        ws.Cell(2, 2).Value = "Tên Nhà Xe";
        ws.Cell(2, 3).Value = "Tổng Đơn";
        ws.Cell(2, 4).Value = "Doanh Thu";

        int row = 3;
        foreach (var item in model)
        {
            ws.Cell(row, 1).Value = row - 2;
            ws.Cell(row, 2).Value = item.OperatorName;
            ws.Cell(row, 3).Value = item.TotalBookings;
            ws.Cell(row, 4).Value = item.TotalRevenue;
            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "DoanhThuNhaXe.xlsx");
    }
}