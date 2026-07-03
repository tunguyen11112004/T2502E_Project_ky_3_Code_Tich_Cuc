namespace Bus_ticket.ViewModels;

public class VehicleRevenueByBusItemViewModel
{
    public string BusId { get; set; } = string.Empty;

    public string BusCode { get; set; } = string.Empty;

    public string LicensePlate { get; set; } = string.Empty;

    public string BusType { get; set; } = string.Empty;

    public string BusClassName { get; set; } = string.Empty;

    public int TotalBookings { get; set; }

    public int TotalTickets { get; set; }

    public decimal TotalRevenue { get; set; }

    public double Percentage { get; set; }
}

public class VehicleRevenueByTypeItemViewModel
{
    public string BusType { get; set; } = string.Empty;

    public string BusClassName { get; set; } = string.Empty;

    public int TotalBookings { get; set; }

    public int TotalTickets { get; set; }

    public decimal TotalRevenue { get; set; }

    public double Percentage { get; set; }
}

public class VehicleRevenueStatisticsViewModel
{
    public DateTime FromDate { get; set; }

    public DateTime ToDate { get; set; }

    public decimal GrandTotalRevenue { get; set; }

    public int GrandTotalBookings { get; set; }

    public int GrandTotalTickets { get; set; }

    public int TotalBusesWithRevenue => BusRevenueItems.Count;

    public int TotalBusTypesWithRevenue => BusTypeRevenueItems.Count;

    public List<VehicleRevenueByBusItemViewModel> BusRevenueItems { get; set; } = new();

    public List<VehicleRevenueByTypeItemViewModel> BusTypeRevenueItems { get; set; } = new();
}