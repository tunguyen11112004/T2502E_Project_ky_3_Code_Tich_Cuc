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
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> RouteRevenue(DateTime? fromDate, DateTime? toDate)
    {
        var (from, to) = NormalizeDateRange(fromDate, toDate, 30);
        var model = await _dashboardService.GetRouteRevenueReportAsync(from, to);
        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> ExportRouteRevenue(DateTime? fromDate, DateTime? toDate)
    {
        var (from, to) = NormalizeDateRange(fromDate, toDate, 30);
        var model = await _dashboardService.GetRouteRevenueReportAsync(from, to);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Route Revenue");
        worksheet.Style.Font.FontName = "Arial";

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

        StyleHeader(worksheet.Range(headerRow, 1, headerRow, 8));

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
        StyleTotal(worksheet.Range(row, 1, row, 8));

        worksheet.Column(5).Style.NumberFormat.Format = "#,##0";
        worksheet.Column(6).Style.NumberFormat.Format = "0.00%";
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var fileName = $"RouteRevenue_{model.FromDate:yyyyMMdd}_{model.ToDate:yyyyMMdd}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> SeatAnalyticsPartial(DateTime? fromDate, DateTime? toDate, int page = 1)
    {
        var (from, to) = NormalizeDateRange(fromDate, toDate, 30);
        const int pageSize = 10;

        var result = await _dashboardService.GetSeatAnalyticsReportAsync(from, to, page, pageSize);
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)result.TotalItems / pageSize));

        ViewBag.FromDateValue = from;
        ViewBag.ToDateValue = to;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = result.TotalItems;
        ViewBag.PageSize = pageSize;

        return PartialView("_SeatAnalyticsPartial", result.Items);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> ExportSeatAnalytics(DateTime? fromDate, DateTime? toDate)
    {
        var (from, to) = NormalizeDateRange(fromDate, toDate, 30);
        var result = await _dashboardService.GetSeatAnalyticsReportAsync(from, to, 1, int.MaxValue);
        var model = result.Items;

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Seat Analytics");
        worksheet.Style.Font.FontName = "Arial";

        worksheet.Cell(1, 1).Value = "BÁO CÁO TỶ LỆ LẤP ĐẦY GHẾ THEO CHUYẾN XE";
        worksheet.Range(1, 1, 1, 7).Merge();
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
        worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        worksheet.Cell(3, 1).Value = "Từ ngày:";
        worksheet.Cell(3, 2).Value = from.ToString("dd/MM/yyyy");
        worksheet.Cell(4, 1).Value = "Đến ngày:";
        worksheet.Cell(4, 2).Value = to.ToString("dd/MM/yyyy");

        var headerRow = 6;
        worksheet.Cell(headerRow, 1).Value = "STT";
        worksheet.Cell(headerRow, 2).Value = "Mã Chuyến";
        worksheet.Cell(headerRow, 3).Value = "Tuyến Đường";
        worksheet.Cell(headerRow, 4).Value = "Biển Số Xe";
        worksheet.Cell(headerRow, 5).Value = "Giờ Khởi Hành";
        worksheet.Cell(headerRow, 6).Value = "Ghế Đã Bán / Tổng Ghế";
        worksheet.Cell(headerRow, 7).Value = "Tỷ Lệ Lấp Đầy";
        StyleHeader(worksheet.Range(headerRow, 1, headerRow, 7));

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
            worksheet.Cell(row, 7).Value = item.OccupancyRate / 100;
            row++;
            index++;
        }

        worksheet.Column(7).Style.NumberFormat.Format = "0.00%";
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var fileName = $"TyleLapDayGhe_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> BranchCancellationPartial(DateTime? fromDate, DateTime? toDate)
    {
        var (from, to) = NormalizeDateRange(fromDate, toDate, 30);
        var model = await _dashboardService.GetBranchCancellationReportAsync(from, to);

        ViewBag.FromDateValue = from;
        ViewBag.ToDateValue = to;
        ViewBag.HighestCancellation = model.OrderByDescending(m => m.CancellationRate).FirstOrDefault();
        ViewBag.LowestCancellation = model.OrderBy(m => m.CancellationRate).FirstOrDefault();

        return PartialView("_BranchCancellationPartial", model);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> ExportBranchCancellation(DateTime? fromDate, DateTime? toDate)
    {
        var (from, to) = NormalizeDateRange(fromDate, toDate, 30);
        var model = await _dashboardService.GetBranchCancellationReportAsync(from, to);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Hủy Chuyến");
        worksheet.Style.Font.FontName = "Arial";

        worksheet.Cell(1, 1).Value = "BÁO CÁO THỐNG KÊ TỶ LỆ HỦY CHUYẾN THEO NHÀ XE";
        worksheet.Range(1, 1, 1, 5).Merge();
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
        worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        worksheet.Cell(3, 1).Value = "Từ ngày:";
        worksheet.Cell(3, 2).Value = from.ToString("dd/MM/yyyy");
        worksheet.Cell(4, 1).Value = "Đến ngày:";
        worksheet.Cell(4, 2).Value = to.ToString("dd/MM/yyyy");

        var headerRow = 6;
        worksheet.Cell(headerRow, 1).Value = "STT";
        worksheet.Cell(headerRow, 2).Value = "Tên Nhà Xe / Đối Tác";
        worksheet.Cell(headerRow, 3).Value = "Tổng Số Chuyến";
        worksheet.Cell(headerRow, 4).Value = "Số Chuyến Bị Hủy";
        worksheet.Cell(headerRow, 5).Value = "Tỷ Lệ Hủy Chuyến";
        StyleHeader(worksheet.Range(headerRow, 1, headerRow, 5));

        var row = headerRow + 1;
        var index = 1;
        foreach (var item in model)
        {
            worksheet.Cell(row, 1).Value = index;
            worksheet.Cell(row, 2).Value = item.BranchName;
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
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ThongKeHuyChuyen_NhaXe_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx");
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee,Operator")]
    public async Task<IActionResult> OperatorRevenuePartial(DateTime? fromDate, DateTime? toDate)
    {
        var (from, to) = NormalizeDateRange(fromDate, toDate, 30);

        string? operatorId = null;
        if (User.IsInRole("Operator"))
        {
            operatorId = User.FindFirst("OperatorId")?.Value;
        }

        var model = await _dashboardService.GetOperatorRevenueReportAsync(from, to, operatorId);
        ViewBag.FromDateValue = from;
        ViewBag.ToDateValue = to;
        return PartialView("_OperatorRevenuePartial", model);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee,Operator")]
    public async Task<IActionResult> ExportOperatorRevenue(DateTime? fromDate, DateTime? toDate)
    {
        var (from, to) = NormalizeDateRange(fromDate, toDate, 30);
        var model = await _dashboardService.GetOperatorRevenueReportAsync(from, to);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("DoanhThuNhaXe");
        ws.Style.Font.FontName = "Arial";

        ws.Cell(1, 1).Value = "BÁO CÁO DOANH THU NHÀ XE";
        ws.Range(1, 1, 1, 4).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;
        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(3, 1).Value = "Từ ngày:";
        ws.Cell(3, 2).Value = from.ToString("dd/MM/yyyy");
        ws.Cell(4, 1).Value = "Đến ngày:";
        ws.Cell(4, 2).Value = to.ToString("dd/MM/yyyy");

        var headerRow = 6;
        ws.Cell(headerRow, 1).Value = "STT";
        ws.Cell(headerRow, 2).Value = "Tên Nhà Xe";
        ws.Cell(headerRow, 3).Value = "Tổng Đơn";
        ws.Cell(headerRow, 4).Value = "Doanh Thu";
        StyleHeader(ws.Range(headerRow, 1, headerRow, 4));

        var row = headerRow + 1;
        var index = 1;
        foreach (var item in model)
        {
            ws.Cell(row, 1).Value = index;
            ws.Cell(row, 2).Value = item.OperatorName;
            ws.Cell(row, 3).Value = item.TotalBookings;
            ws.Cell(row, 4).Value = item.TotalRevenue;
            row++;
            index++;
        }

        ws.Column(4).Style.NumberFormat.Format = "#,##0";
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"DoanhThuNhaXe_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx");
    }
    
    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> RouteRevenuePartial(
        DateTime? fromDate,
        DateTime? toDate,
        int page = 1)
    {
        var from = fromDate ?? DateTime.Today.AddDays(-30);
        var to = toDate ?? DateTime.Today;

        if (from > to)
        {
            var temp = from;
            from = to;
            to = temp;
        }

        var model = await _dashboardService.GetRouteRevenueReportAsync(from, to);

        ViewBag.FromDateValue = from;
        ViewBag.ToDateValue = to;

        return PartialView("_RouteRevenuePartial", model);
    }

    private static (DateTime From, DateTime To) NormalizeDateRange(DateTime? fromDate, DateTime? toDate, int defaultDaysBack)
    {
        var from = fromDate ?? DateTime.Today.AddDays(-defaultDaysBack);
        var to = toDate ?? DateTime.Today;

        from = from.Date;
        to = to.Date;

        if (from > to)
        {
            (from, to) = (to, from);
        }

        return (from, to);
    }

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.LightGray;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    }

    private static void StyleTotal(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.LightYellow;
    }
}
