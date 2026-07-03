using Bus_ticket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;

namespace Bus_ticket.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly DashboardService _dashboardService;
    private readonly VehicleRevenueStatisticsService _vehicleRevenueStatisticsService;

    public DashboardController(
        DashboardService dashboardService,
        VehicleRevenueStatisticsService vehicleRevenueStatisticsService)
    {
        _dashboardService = dashboardService;
        _vehicleRevenueStatisticsService = vehicleRevenueStatisticsService;
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
public async Task<IActionResult> VehicleRevenueStatistics(DateTime? fromDate, DateTime? toDate)
{
    var from = fromDate ?? DateTime.Today.AddDays(-30);
    var to = toDate ?? DateTime.Today;

    if (from > to)
    {
        var temp = from;
        from = to;
        to = temp;
    }

    var model = await _vehicleRevenueStatisticsService
        .GetVehicleRevenueStatisticsAsync(from, to);

    return View(model);
}

[HttpGet]
[Authorize(Roles = "Admin,Employee")]
public async Task<IActionResult> ExportVehicleRevenueStatistics(DateTime? fromDate, DateTime? toDate)
{
    var from = fromDate ?? DateTime.Today.AddDays(-30);
    var to = toDate ?? DateTime.Today;

    if (from > to)
    {
        var temp = from;
        from = to;
        to = temp;
    }

    var model = await _vehicleRevenueStatisticsService
        .GetVehicleRevenueStatisticsAsync(from, to);

    using var workbook = new XLWorkbook();

    var summarySheet = workbook.Worksheets.Add("Vehicle Revenue Summary");

    summarySheet.Cell(1, 1).Value = "VEHICLE REVENUE STATISTICS REPORT";
    summarySheet.Range(1, 1, 1, 6).Merge();
    summarySheet.Cell(1, 1).Style.Font.Bold = true;
    summarySheet.Cell(1, 1).Style.Font.FontSize = 16;
    summarySheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

    summarySheet.Cell(3, 1).Value = "From Date";
    summarySheet.Cell(3, 2).Value = model.FromDate.ToString("yyyy-MM-dd");

    summarySheet.Cell(4, 1).Value = "To Date";
    summarySheet.Cell(4, 2).Value = model.ToDate.ToString("yyyy-MM-dd");

    summarySheet.Cell(5, 1).Value = "Grand Total Revenue";
    summarySheet.Cell(5, 2).Value = model.GrandTotalRevenue;
    summarySheet.Cell(5, 2).Style.NumberFormat.Format = "#,##0";

    summarySheet.Cell(6, 1).Value = "Grand Total Bookings";
    summarySheet.Cell(6, 2).Value = model.GrandTotalBookings;

    summarySheet.Cell(7, 1).Value = "Grand Total Tickets";
    summarySheet.Cell(7, 2).Value = model.GrandTotalTickets;

    var typeHeaderRow = 10;

    summarySheet.Cell(typeHeaderRow, 1).Value = "No.";
    summarySheet.Cell(typeHeaderRow, 2).Value = "Bus Type";
    summarySheet.Cell(typeHeaderRow, 3).Value = "Bus Class";
    summarySheet.Cell(typeHeaderRow, 4).Value = "Bookings";
    summarySheet.Cell(typeHeaderRow, 5).Value = "Tickets";
    summarySheet.Cell(typeHeaderRow, 6).Value = "Revenue";
    summarySheet.Cell(typeHeaderRow, 7).Value = "Percentage";

    var typeHeaderRange = summarySheet.Range(typeHeaderRow, 1, typeHeaderRow, 7);
    typeHeaderRange.Style.Font.Bold = true;
    typeHeaderRange.Style.Fill.BackgroundColor = XLColor.LightGray;
    typeHeaderRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
    typeHeaderRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

    var row = typeHeaderRow + 1;
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

    summarySheet.Cell(row, 2).Value = "TOTAL";
    summarySheet.Cell(row, 4).Value = model.GrandTotalBookings;
    summarySheet.Cell(row, 5).Value = model.GrandTotalTickets;
    summarySheet.Cell(row, 6).Value = model.GrandTotalRevenue;
    summarySheet.Cell(row, 7).Value = model.GrandTotalRevenue > 0 ? 1 : 0;

    var typeTotalRange = summarySheet.Range(row, 1, row, 7);
    typeTotalRange.Style.Font.Bold = true;
    typeTotalRange.Style.Fill.BackgroundColor = XLColor.LightYellow;

    summarySheet.Column(6).Style.NumberFormat.Format = "#,##0";
    summarySheet.Column(7).Style.NumberFormat.Format = "0.00%";
    summarySheet.Columns().AdjustToContents();

    var busSheet = workbook.Worksheets.Add("Revenue By Bus");

    busSheet.Cell(1, 1).Value = "REVENUE BY BUS";
    busSheet.Range(1, 1, 1, 9).Merge();
    busSheet.Cell(1, 1).Style.Font.Bold = true;
    busSheet.Cell(1, 1).Style.Font.FontSize = 16;
    busSheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

    var busHeaderRow = 3;

    busSheet.Cell(busHeaderRow, 1).Value = "No.";
    busSheet.Cell(busHeaderRow, 2).Value = "Bus Code";
    busSheet.Cell(busHeaderRow, 3).Value = "License Plate";
    busSheet.Cell(busHeaderRow, 4).Value = "Bus Type";
    busSheet.Cell(busHeaderRow, 5).Value = "Bus Class";
    busSheet.Cell(busHeaderRow, 6).Value = "Bookings";
    busSheet.Cell(busHeaderRow, 7).Value = "Tickets";
    busSheet.Cell(busHeaderRow, 8).Value = "Revenue";
    busSheet.Cell(busHeaderRow, 9).Value = "Percentage";

    var busHeaderRange = busSheet.Range(busHeaderRow, 1, busHeaderRow, 9);
    busHeaderRange.Style.Font.Bold = true;
    busHeaderRange.Style.Fill.BackgroundColor = XLColor.LightGray;
    busHeaderRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
    busHeaderRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

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

    busSheet.Cell(row, 2).Value = "TOTAL";
    busSheet.Cell(row, 6).Value = model.GrandTotalBookings;
    busSheet.Cell(row, 7).Value = model.GrandTotalTickets;
    busSheet.Cell(row, 8).Value = model.GrandTotalRevenue;
    busSheet.Cell(row, 9).Value = model.GrandTotalRevenue > 0 ? 1 : 0;

    var busTotalRange = busSheet.Range(row, 1, row, 9);
    busTotalRange.Style.Font.Bold = true;
    busTotalRange.Style.Fill.BackgroundColor = XLColor.LightYellow;

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
}