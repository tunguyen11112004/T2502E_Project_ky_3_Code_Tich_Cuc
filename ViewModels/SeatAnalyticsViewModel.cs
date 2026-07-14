namespace Bus_ticket.ViewModels;

public class SeatAnalyticsViewModel
{
    public string TripCode { get; set; } = string.Empty;
    public string RouteName { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public int TotalSeats { get; set; }
    public int BookedSeats { get; set; }
    public double OccupancyRate { get; set; }
    public string Status { get; set; } = string.Empty;
}