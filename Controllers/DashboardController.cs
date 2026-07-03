using Bus_ticket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;
using System;
using System.IO;
using System.Threading.Tasks;

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
    public async Task<IActionResult> GetDashboardSummary(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.Today;
        var to = toDate ?? DateTime.Today;
        var stats = await _dashboardService.GetTotalRevenueStatsAsync(from, to);
        return Json(new { success = true, data = stats });
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> GetRouteOccupancy(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.Today;
        var to = toDate ?? DateTime.Today;
        var stats = await _dashboardService.GetRouteOccupancyAsync(from, to);
        return Json(new { success = true, data = stats });
    }

    // Kết nối trang Khung Giờ Cháy Vé
    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> SoldOutStats(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.Today;
        var to = toDate ?? DateTime.Today;
        var stats = await _dashboardService.GetSoldOutTimeFramesAsync(from, to);
        
        ViewBag.FromDate = from;
        ViewBag.ToDate = to;
        return View(stats); 
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> ExportTotalRevenue(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate ?? DateTime.Today;
        var to = toDate ?? DateTime.Today;
        var stats = await _dashboardService.GetTotalRevenueStatsAsync(from, to);
        
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Tong Doanh Thu");

        worksheet.Cell(1, 1).Value = "BÁO CÁO TỔNG DOANH THU";
        worksheet.Range(1, 1, 1, 2).Merge().Style.Font.Bold = true;

        worksheet.Cell(3, 1).Value = "Từ ngày:";
        worksheet.Cell(3, 2).Value = from.ToString("dd/MM/yyyy");
        worksheet.Cell(4, 1).Value = "Đến ngày:";
        worksheet.Cell(4, 2).Value = to.ToString("dd/MM/yyyy");
        worksheet.Cell(6, 1).Value = "Tổng doanh thu (VNĐ):";
        worksheet.Cell(6, 2).Value = stats.TotalRevenue;
        worksheet.Cell(6, 2).Style.NumberFormat.Format = "#,##0";

        worksheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"DoanhThu_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx");
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> ExportSoldOutTimeFrames(DateTime fromDate, DateTime toDate)
    {
        if (fromDate > toDate) { var temp = fromDate; fromDate = toDate; toDate = temp; }

        var stats = await _dashboardService.GetSoldOutTimeFramesAsync(fromDate, toDate);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Khung Gio Chay Ve");

        worksheet.Cell(1, 1).Value = "BÁO CÁO KHUNG GIỜ CHÁY VÉ";
        var headerRow = 3;
        worksheet.Cell(headerRow, 1).Value = "Khung Giờ";
        worksheet.Cell(headerRow, 2).Value = "Số Lần";
        
        var row = headerRow + 1;
        foreach (var item in stats)
        {
            worksheet.Cell(row, 1).Value = item.TimeFrame;
            worksheet.Cell(row, 2).Value = item.SoldOutCount;
            row++;
        }

        worksheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"ChayVe_{fromDate:yyyyMMdd}.xlsx");
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> RouteRevenue(DateTime? fromDate, DateTime? toDate)
    {
        var model = await _dashboardService.GetRouteRevenueReportAsync(fromDate ?? DateTime.Today.AddDays(-7), toDate ?? DateTime.Today);
        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> ExportRouteRevenue(DateTime fromDate, DateTime toDate)
    {
        var model = await _dashboardService.GetRouteRevenueReportAsync(fromDate, toDate);
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Route Revenue");
        
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "RouteRevenue.xlsx");
    }
}