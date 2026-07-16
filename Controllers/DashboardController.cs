using Bus_ticket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace Bus_ticket.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly DashboardService _dashboardService;
    private readonly TicketStatisticsService _ticketStatisticsService;
    private readonly VehicleRevenueStatisticsService _vehicleRevenueStatisticsService;
    private readonly LowOccupancyTripsService _lowOccupancyTripsService;

    public DashboardController(
        DashboardService dashboardService,
        TicketStatisticsService ticketStatisticsService,
        VehicleRevenueStatisticsService vehicleRevenueStatisticsService,
        LowOccupancyTripsService lowOccupancyTripsService)
    {
        _dashboardService = dashboardService;
        _ticketStatisticsService = ticketStatisticsService;
        _vehicleRevenueStatisticsService = vehicleRevenueStatisticsService; 
        _lowOccupancyTripsService = lowOccupancyTripsService;
    }


    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public IActionResult Index()
    {
        return View();
    }


    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> TotalRevenuePartial(DateTime? fromDate, DateTime? toDate)
    {
        var (from, to) = NormalizeDateRange(fromDate, toDate, 30);
        
        var model = await _dashboardService.GetSystemTotalRevenueAsync(from, to);
        
        ViewBag.FromDateValue = from.ToString("yyyy-MM-dd");
        ViewBag.ToDateValue = to.ToString("yyyy-MM-dd");
        
        return PartialView("_TotalRevenuePartial", model);
    }


    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> SoldOutStatsPartial(DateTime? fromDate, DateTime? toDate)
    {
        var (from, to) = NormalizeDateRange(fromDate, toDate, 30);

        var model = await _dashboardService.GetSoldOutTripsAsync(from, to);
        
        ViewBag.FromDateValue = from.ToString("yyyy-MM-dd");
        ViewBag.ToDateValue = to.ToString("yyyy-MM-dd");

        return PartialView("_SoldOutStatsPartial", model);
    }


    // ========================================================
    // XUẤT EXCEL: DANH SÁCH CHÁY GHẾ (MỚI THÊM)
    // ========================================================
    [HttpGet]
[Authorize(Roles = "Admin,Employee")]
public async Task<IActionResult> ExportSoldOutStats(DateTime? startDate, DateTime? endDate)
{
    var (from, to) = NormalizeDateRange(startDate, endDate, 30);
    var model = await _dashboardService.GetSoldOutTripsAsync(from, to);

    using var workbook = new XLWorkbook();
    var worksheet = workbook.Worksheets.Add("Chay Ghe");
    worksheet.Style.Font.FontName = "Arial";

    worksheet.Cell(1, 1).Value = "BÁO CÁO DANH SÁCH CHUYẾN XE CHÁY GHẾ";
    worksheet.Range(1, 1, 1, 8).Merge();
    worksheet.Cell(1, 1).Style.Font.Bold = true;
    worksheet.Cell(1, 1).Style.Font.FontSize = 16;
    worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

    worksheet.Cell(3, 1).Value = "Từ ngày:";
    worksheet.Cell(3, 2).Value = from.ToString("dd/MM/yyyy");
    worksheet.Cell(4, 1).Value = "Đến ngày:";
    worksheet.Cell(4, 2).Value = to.ToString("dd/MM/yyyy");

    var headerRow = 6;
    worksheet.Cell(headerRow, 1).Value = "STT";
    worksheet.Cell(headerRow, 2).Value = "MÃ CHUYẾN";
    worksheet.Cell(headerRow, 3).Value = "TUYẾN ĐƯỜNG";
    worksheet.Cell(headerRow, 4).Value = "LOẠI XE";
    worksheet.Cell(headerRow, 5).Value = "NHÀ XE";
    worksheet.Cell(headerRow, 6).Value = "BIỂN SỐ XE";
    worksheet.Cell(headerRow, 7).Value = "THỜI GIAN ĐI";
    worksheet.Cell(headerRow, 8).Value = "TỶ LỆ LẤP ĐẦY";

    StyleHeader(worksheet.Range(headerRow, 1, headerRow, 8));

    var row = headerRow + 1;
    var index = 1;

    foreach (var item in model)
    {
        worksheet.Cell(row, 1).Value = index;
        worksheet.Cell(row, 2).Value = item.TripCode;
        worksheet.Cell(row, 3).Value = item.RouteName;
        worksheet.Cell(row, 4).Value = item.BusType;
        worksheet.Cell(row, 5).Value = item.OperatorName;
        worksheet.Cell(row, 6).Value = item.LicensePlate;
        worksheet.Cell(row, 7).Value = item.DepartureTime.ToString("dd/MM/yyyy HH:mm");
        worksheet.Cell(row, 8).Value = $"{item.OccupancyRate}% ({item.BookedSeats}/{item.TotalSeats})";

        row++;
        index++;
    }

    worksheet.Columns().AdjustToContents();

    using var stream = new MemoryStream();
    workbook.SaveAs(stream);

    var fileName = $"BaoCao_ChayGhe_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx";

    return File(
        stream.ToArray(),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        fileName
    );
}


    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> ExportTotalRevenue(DateTime? startDate, DateTime? endDate)
    {
        var (from, to) = NormalizeDateRange(startDate, endDate, 30);
        var data = await _dashboardService.GetSystemTotalRevenueAsync(from, to);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Tong Doanh Thu");
        worksheet.Style.Font.FontName = "Arial";

        worksheet.Cell(1, 1).Value = "BÁO CÁO TỔNG DOANH THU HỆ THỐNG";
        worksheet.Range(1, 1, 1, 6).Merge();
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
        worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        worksheet.Cell(3, 1).Value = "Từ ngày:";
        worksheet.Cell(3, 2).Value = from.ToString("dd/MM/yyyy");
        worksheet.Cell(4, 1).Value = "Đến ngày:";
        worksheet.Cell(4, 2).Value = to.ToString("dd/MM/yyyy");

        var headerRow = 6;
        worksheet.Cell(headerRow, 1).Value = "STT";
        worksheet.Cell(headerRow, 2).Value = "MÃ VÉ";
        worksheet.Cell(headerRow, 3).Value = "KHÁCH HÀNG";
        worksheet.Cell(headerRow, 4).Value = "HẠNG XE";
        worksheet.Cell(headerRow, 5).Value = "NGÀY THANH TOÁN";
        worksheet.Cell(headerRow, 6).Value = "SỐ TIỀN";
        
        StyleHeader(worksheet.Range(headerRow, 1, headerRow, 6));


        var row = headerRow + 1;
        var index = 1;
        foreach (var item in data.TableData)
        {
            worksheet.Cell(row, 1).Value = index;
            worksheet.Cell(row, 2).Value = item.BookingCode;
            worksheet.Cell(row, 3).Value = item.CustomerName;
            worksheet.Cell(row, 4).Value = item.BusClass;
            worksheet.Cell(row, 5).Value = item.PaymentDate.ToString("dd/MM/yyyy HH:mm");
            worksheet.Cell(row, 6).Value = item.Amount;
            row++;
            index++;
        }

        worksheet.Column(6).Style.NumberFormat.Format = "#,##0";
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var fileName = $"BaoCao_TongDoanhThu_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
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
    public async Task<IActionResult> SeatAnalyticsPartial(DateTime? fromDate, DateTime? toDate)
    {
        var (from, to) = NormalizeDateRange(fromDate, toDate, 30);

        var result = await _dashboardService.GetSeatAnalyticsReportAsync(from, to, 1, int.MaxValue);

        ViewBag.FromDateValue = from;
        ViewBag.ToDateValue = to;

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

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> TicketStatusStatistics(DateTime? fromDate, DateTime? toDate)
    {
        var (from, to) = NormalizeDateRange(fromDate, toDate, 30);
        var model = await _ticketStatisticsService.GetTicketStatusStatisticsAsync(from, to);

        return View(model);
    }
    
    [HttpGet]
[Authorize(Roles = "Admin,Employee")]
public async Task<IActionResult> VehicleRevenueStatisticsPartial(
    DateTime? fromDate,
    DateTime? toDate,
    int page = 1)
{
    var (from, to) = NormalizeDateRange(fromDate, toDate, 30);

    var model = await _vehicleRevenueStatisticsService
        .GetVehicleRevenueStatisticsAsync(from, to);

    ViewBag.FromDateValue = from;
    ViewBag.ToDateValue = to;

    return PartialView("_VehicleRevenueStatisticsPartial", model);
}

[HttpGet]
[Authorize(Roles = "Admin,Employee")]
public async Task<IActionResult> ExportVehicleRevenueStatistics(DateTime? fromDate, DateTime? toDate)
{
    var (from, to) = NormalizeDateRange(fromDate, toDate, 30);

    var model = await _vehicleRevenueStatisticsService
        .GetVehicleRevenueStatisticsAsync(from, to);

    using var workbook = new XLWorkbook();

    var summarySheet = workbook.Worksheets.Add("Vehicle Revenue Summary");
    summarySheet.Style.Font.FontName = "Arial";

    summarySheet.Cell(1, 1).Value = "BÁO CÁO DOANH THU PHƯƠNG TIỆN";
    summarySheet.Range(1, 1, 1, 7).Merge();
    summarySheet.Cell(1, 1).Style.Font.Bold = true;
    summarySheet.Cell(1, 1).Style.Font.FontSize = 16;
    summarySheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

    summarySheet.Cell(3, 1).Value = "Từ ngày:";
    summarySheet.Cell(3, 2).Value = model.FromDate.ToString("dd/MM/yyyy");
    summarySheet.Cell(4, 1).Value = "Đến ngày:";
    summarySheet.Cell(4, 2).Value = model.ToDate.ToString("dd/MM/yyyy");
    summarySheet.Cell(5, 1).Value = "Tổng doanh thu:";
    summarySheet.Cell(5, 2).Value = model.GrandTotalRevenue;
    summarySheet.Cell(5, 2).Style.NumberFormat.Format = "#,##0";

    var summaryHeaderRow = 7;
    summarySheet.Cell(summaryHeaderRow, 1).Value = "STT";
    summarySheet.Cell(summaryHeaderRow, 2).Value = "Loại xe";
    summarySheet.Cell(summaryHeaderRow, 3).Value = "Hạng xe";
    summarySheet.Cell(summaryHeaderRow, 4).Value = "Booking";
    summarySheet.Cell(summaryHeaderRow, 5).Value = "Vé";
    summarySheet.Cell(summaryHeaderRow, 6).Value = "Doanh thu";
    summarySheet.Cell(summaryHeaderRow, 7).Value = "Tỷ lệ";
    StyleHeader(summarySheet.Range(summaryHeaderRow, 1, summaryHeaderRow, 7));

    var row = summaryHeaderRow + 1;
    var index = 1;

    foreach (var item in model.BusTypeRevenueItems)
    {
        summarySheet.Cell(row, 1).Value = index;
        summarySheet.Cell(row, 2).Value = item.BusType;
        summarySheet.Cell(row, 3).Value = item.BusClassName;
        summarySheet.Cell(row, 4).Value = item.TotalBookings;
        summarySheet.Cell(row, 5).Value = item.TotalTickets;
        summarySheet.Cell(row, 6).Value = item.TotalRevenue;
        summarySheet.Cell(row, 7).Value = item.Percentage / 100;
        row++;
        index++;
    }

    summarySheet.Cell(row, 2).Value = "TỔNG CỘNG";
    summarySheet.Cell(row, 4).Value = model.GrandTotalBookings;
    summarySheet.Cell(row, 5).Value = model.GrandTotalTickets;
    summarySheet.Cell(row, 6).Value = model.GrandTotalRevenue;
    summarySheet.Cell(row, 7).Value = model.GrandTotalRevenue > 0 ? 1 : 0;
    StyleTotal(summarySheet.Range(row, 1, row, 7));

    summarySheet.Column(6).Style.NumberFormat.Format = "#,##0";
    summarySheet.Column(7).Style.NumberFormat.Format = "0.00%";
    summarySheet.Columns().AdjustToContents();

    var busSheet = workbook.Worksheets.Add("Revenue By Bus");
    busSheet.Style.Font.FontName = "Arial";

    busSheet.Cell(1, 1).Value = "BÁO CÁO DOANH THU THEO TỪNG XE";
    busSheet.Range(1, 1, 1, 9).Merge();
    busSheet.Cell(1, 1).Style.Font.Bold = true;
    busSheet.Cell(1, 1).Style.Font.FontSize = 16;
    busSheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

    var busHeaderRow = 3;
    busSheet.Cell(busHeaderRow, 1).Value = "STT";
    busSheet.Cell(busHeaderRow, 2).Value = "Mã xe";
    busSheet.Cell(busHeaderRow, 3).Value = "Biển số";
    busSheet.Cell(busHeaderRow, 4).Value = "Loại xe";
    busSheet.Cell(busHeaderRow, 5).Value = "Hạng xe";
    busSheet.Cell(busHeaderRow, 6).Value = "Booking";
    busSheet.Cell(busHeaderRow, 7).Value = "Vé";
    busSheet.Cell(busHeaderRow, 8).Value = "Doanh thu";
    busSheet.Cell(busHeaderRow, 9).Value = "Tỷ lệ";
    StyleHeader(busSheet.Range(busHeaderRow, 1, busHeaderRow, 9));

    row = busHeaderRow + 1;
    index = 1;

    foreach (var item in model.BusRevenueItems)
    {
        busSheet.Cell(row, 1).Value = index;
        busSheet.Cell(row, 2).Value = item.BusCode;
        busSheet.Cell(row, 3).Value = item.LicensePlate;
        busSheet.Cell(row, 4).Value = item.BusType;
        busSheet.Cell(row, 5).Value = item.BusClassName;
        busSheet.Cell(row, 6).Value = item.TotalBookings;
        busSheet.Cell(row, 7).Value = item.TotalTickets;
        busSheet.Cell(row, 8).Value = item.TotalRevenue;
        busSheet.Cell(row, 9).Value = item.Percentage / 100;
        row++;
        index++;
    }

    busSheet.Cell(row, 2).Value = "TỔNG CỘNG";
    busSheet.Cell(row, 6).Value = model.GrandTotalBookings;
    busSheet.Cell(row, 7).Value = model.GrandTotalTickets;
    busSheet.Cell(row, 8).Value = model.GrandTotalRevenue;
    busSheet.Cell(row, 9).Value = model.GrandTotalRevenue > 0 ? 1 : 0;
    StyleTotal(busSheet.Range(row, 1, row, 9));

    busSheet.Column(8).Style.NumberFormat.Format = "#,##0";
    busSheet.Column(9).Style.NumberFormat.Format = "0.00%";
    busSheet.Columns().AdjustToContents();

    using var stream = new MemoryStream();
    workbook.SaveAs(stream);

    var fileName = $"VehicleRevenue_{model.FromDate:yyyyMMdd}_{model.ToDate:yyyyMMdd}.xlsx";

    return File(
        stream.ToArray(),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        fileName
    );
}
    [HttpGet]
[Authorize(Roles = "Admin,Employee")]
public async Task<IActionResult> LowOccupancyTripsPartial(
    DateTime? fromDate,
    DateTime? toDate,
    double occupancyThreshold = 40)
{
    var (from, to) = NormalizeDateRange(fromDate, toDate, 30);

    var model = await _lowOccupancyTripsService
        .GetLowOccupancyTripsAsync(from, to, occupancyThreshold);

    ViewBag.FromDateValue = from;
    ViewBag.ToDateValue = to;

    return PartialView("_LowOccupancyTripsPartial", model);
}

[HttpGet]
[Authorize(Roles = "Admin,Employee")]
public async Task<IActionResult> ExportLowOccupancyTrips(
    DateTime? fromDate,
    DateTime? toDate,
    double occupancyThreshold = 40)
{
    var (from, to) = NormalizeDateRange(fromDate, toDate, 30);

    var model = await _lowOccupancyTripsService
        .GetLowOccupancyTripsAsync(from, to, occupancyThreshold);

    using var workbook = new XLWorkbook();

    var summarySheet = workbook.Worksheets.Add("Summary");
    summarySheet.Cell(1, 1).Value = "BÁO CÁO CHUYẾN XE ÍT KHÁCH";
    summarySheet.Range(1, 1, 1, 6).Merge();
    summarySheet.Cell(1, 1).Style.Font.Bold = true;
    summarySheet.Cell(1, 1).Style.Font.FontSize = 16;
    summarySheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

    summarySheet.Cell(3, 1).Value = "Từ ngày";
    summarySheet.Cell(3, 2).Value = model.FromDate.ToString("dd/MM/yyyy");
    summarySheet.Cell(4, 1).Value = "Đến ngày";
    summarySheet.Cell(4, 2).Value = model.ToDate.ToString("dd/MM/yyyy");
    summarySheet.Cell(5, 1).Value = "Ngưỡng ít khách";
    summarySheet.Cell(5, 2).Value = $"{model.OccupancyThreshold:N2}%";
    summarySheet.Cell(6, 1).Value = "Tổng chuyến kiểm tra";
    summarySheet.Cell(6, 2).Value = model.TotalTripsChecked;
    summarySheet.Cell(7, 1).Value = "Số chuyến ít khách";
    summarySheet.Cell(7, 2).Value = model.LowOccupancyTripCount;
    summarySheet.Cell(8, 1).Value = "Số chuyến sold out";
    summarySheet.Cell(8, 2).Value = model.SoldOutTripCount;
    summarySheet.Cell(9, 1).Value = "Tổng ghế trống ở chuyến ít khách";
    summarySheet.Cell(9, 2).Value = model.TotalEmptySeats;
    summarySheet.Cell(10, 1).Value = "Tỷ lệ lấp đầy trung bình";
    summarySheet.Cell(10, 2).Value = $"{model.AverageOccupancyRate:N2}%";
    summarySheet.Columns().AdjustToContents();

    var lowSheet = workbook.Worksheets.Add("Low Occupancy Trips");
    lowSheet.Cell(1, 1).Value = "STT";
    lowSheet.Cell(1, 2).Value = "Mã chuyến";
    lowSheet.Cell(1, 3).Value = "Tuyến đường";
    lowSheet.Cell(1, 4).Value = "Xe";
    lowSheet.Cell(1, 5).Value = "Biển số";
    lowSheet.Cell(1, 6).Value = "Giờ khởi hành";
    lowSheet.Cell(1, 7).Value = "Tổng ghế";
    lowSheet.Cell(1, 8).Value = "Ghế đã đặt";
    lowSheet.Cell(1, 9).Value = "Ghế trống";
    lowSheet.Cell(1, 10).Value = "Tỷ lệ lấp đầy";

    var headerRange = lowSheet.Range(1, 1, 1, 10);
    headerRange.Style.Font.Bold = true;
    headerRange.Style.Fill.BackgroundColor = XLColor.DarkBlue;
    headerRange.Style.Font.FontColor = XLColor.White;

    var row = 2;
    var index = 1;

    foreach (var item in model.LowOccupancyTrips)
    {
        lowSheet.Cell(row, 1).Value = index;
        lowSheet.Cell(row, 2).Value = item.TripCode;
        lowSheet.Cell(row, 3).Value = item.RouteName;
        lowSheet.Cell(row, 4).Value = item.BusCode;
        lowSheet.Cell(row, 5).Value = item.LicensePlate;
        lowSheet.Cell(row, 6).Value = item.DepartureTime.ToString("dd/MM/yyyy HH:mm");
        lowSheet.Cell(row, 7).Value = item.TotalSeats;
        lowSheet.Cell(row, 8).Value = item.BookedSeats;
        lowSheet.Cell(row, 9).Value = item.EmptySeats;
        lowSheet.Cell(row, 10).Value = item.OccupancyRate / 100;
        row++;
        index++;
    }

    lowSheet.Column(10).Style.NumberFormat.Format = "0.00%";
    lowSheet.Columns().AdjustToContents();

    var soldOutSheet = workbook.Worksheets.Add("Sold Out Time Frames");
    soldOutSheet.Cell(1, 1).Value = "STT";
    soldOutSheet.Cell(1, 2).Value = "Khung giờ";
    soldOutSheet.Cell(1, 3).Value = "Số chuyến sold out";
    soldOutSheet.Cell(1, 4).Value = "Tổng ghế";
    soldOutSheet.Cell(1, 5).Value = "Ghế đã đặt";

    var soldOutHeader = soldOutSheet.Range(1, 1, 1, 5);
    soldOutHeader.Style.Font.Bold = true;
    soldOutHeader.Style.Fill.BackgroundColor = XLColor.DarkGreen;
    soldOutHeader.Style.Font.FontColor = XLColor.White;

    row = 2;
    index = 1;

    foreach (var item in model.SoldOutTimeFrames)
    {
        soldOutSheet.Cell(row, 1).Value = index;
        soldOutSheet.Cell(row, 2).Value = item.TimeFrame;
        soldOutSheet.Cell(row, 3).Value = item.SoldOutTripCount;
        soldOutSheet.Cell(row, 4).Value = item.TotalSeats;
        soldOutSheet.Cell(row, 5).Value = item.BookedSeats;
        row++;
        index++;
    }

    soldOutSheet.Columns().AdjustToContents();

    using var stream = new MemoryStream();
    workbook.SaveAs(stream);

    var fileName = $"LowOccupancyTrips_{model.FromDate:yyyyMMdd}_{model.ToDate:yyyyMMdd}.xlsx";

    return File(
        stream.ToArray(),
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        fileName
    );
}

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> TicketStatusStatisticsPartial(DateTime? fromDate, DateTime? toDate, int page = 1)
    {
        var (from, to) = NormalizeDateRange(fromDate, toDate, 30);
        var model = await _ticketStatisticsService.GetTicketStatusStatisticsAsync(from, to);

        ViewBag.FromDateValue = from;
        ViewBag.ToDateValue = to;

        return PartialView("_TicketStatusStatisticsPartial", model);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> ExportTicketStatusStatistics(DateTime? fromDate, DateTime? toDate)
    {
        var (from, to) = NormalizeDateRange(fromDate, toDate, 30);
        var model = await _ticketStatisticsService.GetTicketStatusStatisticsAsync(from, to);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Ticket Status");
        worksheet.Style.Font.FontName = "Arial";

        worksheet.Cell(1, 1).Value = "BÁO CÁO THỐNG KÊ VÉ THÀNH CÔNG VÀ VÉ HỦY";
        worksheet.Range(1, 1, 1, 6).Merge();
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
        worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        worksheet.Cell(3, 1).Value = "Từ ngày:";
        worksheet.Cell(3, 2).Value = model.FromDate.ToString("dd/MM/yyyy");
        worksheet.Cell(4, 1).Value = "Đến ngày:";
        worksheet.Cell(4, 2).Value = model.ToDate.ToString("dd/MM/yyyy");

        worksheet.Cell(6, 1).Value = "Tổng booking:";
        worksheet.Cell(6, 2).Value = model.TotalBookings;
        worksheet.Cell(7, 1).Value = "Tổng vé:";
        worksheet.Cell(7, 2).Value = model.TotalTickets;
        worksheet.Cell(8, 1).Value = "Tỷ lệ hủy:";
        worksheet.Cell(8, 2).Value = model.CancelledPercentage / 100;
        worksheet.Cell(8, 2).Style.NumberFormat.Format = "0.00%";

        var headerRow = 10;
        worksheet.Cell(headerRow, 1).Value = "STT";
        worksheet.Cell(headerRow, 2).Value = "Trạng thái";
        worksheet.Cell(headerRow, 3).Value = "Số booking";
        worksheet.Cell(headerRow, 4).Value = "Số vé";
        worksheet.Cell(headerRow, 5).Value = "Tỷ lệ";
        worksheet.Cell(headerRow, 6).Value = "Khoảng thời gian";
        StyleHeader(worksheet.Range(headerRow, 1, headerRow, 6));

        var row = headerRow + 1;
        var index = 1;
        foreach (var item in model.Items)
        {
            worksheet.Cell(row, 1).Value = index;
            worksheet.Cell(row, 2).Value = item.StatusLabel;
            worksheet.Cell(row, 3).Value = item.BookingCount;
            worksheet.Cell(row, 4).Value = item.TicketCount;
            worksheet.Cell(row, 5).Value = item.Percentage / 100;
            worksheet.Cell(row, 6).Value = $"{model.FromDate:dd/MM/yyyy} - {model.ToDate:dd/MM/yyyy}";
            row++;
            index++;
        }

        worksheet.Cell(row, 2).Value = "TOTAL";
        worksheet.Cell(row, 3).Value = model.TotalBookings;
        worksheet.Cell(row, 4).Value = model.TotalTickets;
        worksheet.Cell(row, 5).Value = model.TotalTickets > 0 ? 1 : 0;
        StyleTotal(worksheet.Range(row, 1, row, 6));

        worksheet.Column(5).Style.NumberFormat.Format = "0.00%";
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var fileName = $"TicketStatusStatistics_{model.FromDate:yyyyMMdd}_{model.ToDate:yyyyMMdd}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
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