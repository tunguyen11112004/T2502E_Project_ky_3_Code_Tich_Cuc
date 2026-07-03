using Bus_ticket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;

namespace Bus_ticket.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly DashboardService _dashboardService;
    private readonly LowOccupancyTripsService _lowOccupancyTripsService;

    public DashboardController(
        DashboardService dashboardService,
        LowOccupancyTripsService lowOccupancyTripsService)
    {
        _dashboardService = dashboardService;
        _lowOccupancyTripsService = lowOccupancyTripsService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> RouteRevenue(DateTime? fromDate, DateTime? toDate)
    {
        var (from, to) = NormalizeDateRange(fromDate, toDate, 7);

        var model = await _dashboardService.GetRouteRevenueReportAsync(from, to);

        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> ExportRouteRevenue(DateTime? fromDate, DateTime? toDate)
    {
        var (from, to) = NormalizeDateRange(fromDate, toDate, 7);

        var model = await _dashboardService.GetRouteRevenueReportAsync(from, to);

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

        var fileName = $"RouteRevenue_{model.FromDate:yyyyMMdd}_{model.ToDate:yyyyMMdd}.xlsx";

        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName
        );
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> LowOccupancyTrips(
        DateTime? fromDate,
        DateTime? toDate,
        double? occupancyThreshold)
    {
        var (from, to) = NormalizeDateRange(fromDate, toDate, 30);
        var threshold = NormalizeThreshold(occupancyThreshold);

        var model = await _lowOccupancyTripsService
            .GetLowOccupancyTripsAsync(from, to, threshold);

        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<IActionResult> ExportLowOccupancyTrips(
        DateTime? fromDate,
        DateTime? toDate,
        double? occupancyThreshold)
    {
        var (from, to) = NormalizeDateRange(fromDate, toDate, 30);
        var threshold = NormalizeThreshold(occupancyThreshold);

        var model = await _lowOccupancyTripsService
            .GetLowOccupancyTripsAsync(from, to, threshold);

        using var workbook = new XLWorkbook();

        var summarySheet = workbook.Worksheets.Add("Summary");
        summarySheet.Style.Font.FontName = "Arial";

        summarySheet.Cell(1, 1).Value = "BÁO CÁO CHUYẾN XE VẮNG KHÁCH";
        summarySheet.Range(1, 1, 1, 5).Merge();
        summarySheet.Cell(1, 1).Style.Font.Bold = true;
        summarySheet.Cell(1, 1).Style.Font.FontSize = 16;
        summarySheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        summarySheet.Cell(3, 1).Value = "Từ ngày";
        summarySheet.Cell(3, 2).Value = model.FromDate.ToString("yyyy-MM-dd");

        summarySheet.Cell(4, 1).Value = "Đến ngày";
        summarySheet.Cell(4, 2).Value = model.ToDate.ToString("yyyy-MM-dd");

        summarySheet.Cell(5, 1).Value = "Ngưỡng lấp đầy";
        summarySheet.Cell(5, 2).Value = model.OccupancyThreshold / 100;
        summarySheet.Cell(5, 2).Style.NumberFormat.Format = "0.00%";

        summarySheet.Cell(7, 1).Value = "Tổng số chuyến";
        summarySheet.Cell(7, 2).Value = model.TotalTrips;

        summarySheet.Cell(8, 1).Value = "Số chuyến vắng khách";
        summarySheet.Cell(8, 2).Value = model.LowOccupancyTripCount;

        summarySheet.Cell(9, 1).Value = "Số chuyến cháy vé";
        summarySheet.Cell(9, 2).Value = model.SoldOutTripCount;

        summarySheet.Cell(10, 1).Value = "Tỷ lệ lấp đầy trung bình";
        summarySheet.Cell(10, 2).Value = model.AverageOccupancyRate / 100;
        summarySheet.Cell(10, 2).Style.NumberFormat.Format = "0.00%";

        summarySheet.Columns().AdjustToContents();

        var lowSheet = workbook.Worksheets.Add("Low Occupancy Trips");
        lowSheet.Style.Font.FontName = "Arial";

        lowSheet.Cell(1, 1).Value = "DANH SÁCH CHUYẾN XE VẮNG KHÁCH";
        lowSheet.Range(1, 1, 1, 10).Merge();
        lowSheet.Cell(1, 1).Style.Font.Bold = true;
        lowSheet.Cell(1, 1).Style.Font.FontSize = 16;
        lowSheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var headerRow = 3;

        lowSheet.Cell(headerRow, 1).Value = "STT";
        lowSheet.Cell(headerRow, 2).Value = "Mã chuyến";
        lowSheet.Cell(headerRow, 3).Value = "Ngày giờ chạy";
        lowSheet.Cell(headerRow, 4).Value = "Tuyến";
        lowSheet.Cell(headerRow, 5).Value = "Mã xe";
        lowSheet.Cell(headerRow, 6).Value = "Biển số";
        lowSheet.Cell(headerRow, 7).Value = "Loại xe";
        lowSheet.Cell(headerRow, 8).Value = "Tổng ghế";
        lowSheet.Cell(headerRow, 9).Value = "Ghế đã đặt";
        lowSheet.Cell(headerRow, 10).Value = "Tỷ lệ lấp đầy";

        var lowHeaderRange = lowSheet.Range(headerRow, 1, headerRow, 10);
        lowHeaderRange.Style.Font.Bold = true;
        lowHeaderRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        lowHeaderRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        lowHeaderRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        var row = headerRow + 1;
        var index = 1;

        foreach (var item in model.LowOccupancyTrips)
        {
            lowSheet.Cell(row, 1).Value = index;
            lowSheet.Cell(row, 2).Value = item.TripCode;
            lowSheet.Cell(row, 3).Value = item.DepartureTime.ToString("yyyy-MM-dd HH:mm");
            lowSheet.Cell(row, 4).Value = item.RouteName;
            lowSheet.Cell(row, 5).Value = item.BusCode;
            lowSheet.Cell(row, 6).Value = item.LicensePlate;
            lowSheet.Cell(row, 7).Value = item.BusType;
            lowSheet.Cell(row, 8).Value = item.TotalSeats;
            lowSheet.Cell(row, 9).Value = item.BookedSeats;
            lowSheet.Cell(row, 10).Value = item.OccupancyRate / 100;
            lowSheet.Cell(row, 10).Style.NumberFormat.Format = "0.00%";

            row++;
            index++;
        }

        lowSheet.Columns().AdjustToContents();

        var soldOutSheet = workbook.Worksheets.Add("Sold Out Time Frames");
        soldOutSheet.Style.Font.FontName = "Arial";

        soldOutSheet.Cell(1, 1).Value = "KHUNG GIỜ CHÁY VÉ";
        soldOutSheet.Range(1, 1, 1, 4).Merge();
        soldOutSheet.Cell(1, 1).Style.Font.Bold = true;
        soldOutSheet.Cell(1, 1).Style.Font.FontSize = 16;
        soldOutSheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        soldOutSheet.Cell(3, 1).Value = "Khung giờ";
        soldOutSheet.Cell(3, 2).Value = "Số chuyến cháy vé";
        soldOutSheet.Cell(3, 3).Value = "Tổng ghế";
        soldOutSheet.Cell(3, 4).Value = "Ghế đã đặt";

        var soldHeaderRange = soldOutSheet.Range(3, 1, 3, 4);
        soldHeaderRange.Style.Font.Bold = true;
        soldHeaderRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        soldHeaderRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        soldHeaderRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        row = 4;

        foreach (var item in model.SoldOutTimeFrames)
        {
            soldOutSheet.Cell(row, 1).Value = item.TimeFrame;
            soldOutSheet.Cell(row, 2).Value = item.SoldOutTripCount;
            soldOutSheet.Cell(row, 3).Value = item.TotalSeats;
            soldOutSheet.Cell(row, 4).Value = item.BookedSeats;

            row++;
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

    private static (DateTime From, DateTime To) NormalizeDateRange(
        DateTime? fromDate,
        DateTime? toDate,
        int defaultDaysBack)
    {
        var from = fromDate ?? DateTime.Today.AddDays(-defaultDaysBack);
        var to = toDate ?? DateTime.Today;

        from = from.Date;
        to = to.Date;

        if (from > to)
        {
            var temp = from;
            from = to;
            to = temp;
        }

        return (from, to);
    }

    private static double NormalizeThreshold(double? occupancyThreshold)
    {
        var threshold = occupancyThreshold ?? 40;

        if (threshold < 0)
        {
            threshold = 0;
        }

        if (threshold > 100)
        {
            threshold = 100;
        }

        return threshold;
    }
}