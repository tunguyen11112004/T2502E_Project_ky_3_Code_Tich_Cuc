using Bus_ticket.Models;

namespace Bus_ticket.ViewModels;

public class TripFormViewModel
{
    public string? TripId { get; set; }
    public string RouteId { get; set; } = string.Empty;
    public string DeparturePoint { get; set; } = string.Empty;
    public string DestinationPoint { get; set; } = string.Empty;
    public string BusId { get; set; } = string.Empty;
    public List<RouteOptionViewModel> Routes { get; set; } = new();
    public decimal BaseFare { get; set; }
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public string Status { get; set; } = "Scheduled";
    public List<BusOptionViewModel> Buses { get; set; } = new();
    public List<PriceConfig> PriceConfigs { get; set; } = new();
    public bool IsEdit => !string.IsNullOrEmpty(TripId);
}
