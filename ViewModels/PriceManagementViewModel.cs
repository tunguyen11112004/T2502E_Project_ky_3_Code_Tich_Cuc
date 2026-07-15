using Bus_ticket.Models;

namespace Bus_ticket.ViewModels;

public class TripListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string TripCode { get; set; } = string.Empty;
    public string RouteId { get; set; } = string.Empty;
    public string DeparturePoint { get; set; } = string.Empty;
    public string DestinationPoint { get; set; } = string.Empty;
    public string BusId { get; set; } = string.Empty;
    public string RouteLabel { get; set; } = string.Empty;
    public string BusLabel { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string BusClassName { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public decimal BaseFare { get; set; }
    public string Status { get; set; } = string.Empty;
    public int TotalSeats { get; set; }
    public int BookedSeats { get; set; }
}

public class RouteOptionViewModel
{
    public string Id { get; set; } = string.Empty;
    public string DeparturePoint { get; set; } = string.Empty;
    public string DestinationPoint { get; set; } = string.Empty;
    public double DistanceKm { get; set; }
    public List<RouteFareOptionViewModel> FareConfigs { get; set; } = new();
}

public class RouteFareOptionViewModel
{
    public string BusType { get; set; } = string.Empty;
    public decimal FlatPrice { get; set; }
}

public class BusOptionViewModel
{
    public string Id { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string BusCode { get; set; } = string.Empty;
    public string BusClassId { get; set; } = string.Empty;
    public string BusClassName { get; set; } = string.Empty;
    public string BusType { get; set; } = string.Empty;
}

public class PriceManagementViewModel
{
    public List<PriceConfig> PriceConfigs { get; set; } = new();
    public List<TripListItemViewModel> Trips { get; set; } = new();
    public List<RouteOptionViewModel> Routes { get; set; } = new();
    public List<BusOptionViewModel> Buses { get; set; } = new();
}
