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
}