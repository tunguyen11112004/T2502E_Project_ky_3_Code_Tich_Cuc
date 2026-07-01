using System;
using System.Collections.Generic;

namespace Bus_ticket.ViewModels;

public class RouteRevenueItemViewModel
{
    public string RouteId { get; set; } = string.Empty;

    public string RouteName { get; set; } = string.Empty;

    public int TotalBookings { get; set; }

    public int TotalTickets { get; set; }

    public decimal TotalRevenue { get; set; }

    public double Percentage { get; set; }
}

public class RouteRevenueReportViewModel
{
    public DateTime FromDate { get; set; }

    public DateTime ToDate { get; set; }

    public decimal GrandTotalRevenue { get; set; }

    public int GrandTotalBookings { get; set; }

    public int GrandTotalTickets { get; set; }

    public List<RouteRevenueItemViewModel> Items { get; set; } = new();
}