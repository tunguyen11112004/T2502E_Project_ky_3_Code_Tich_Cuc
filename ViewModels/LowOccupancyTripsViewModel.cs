namespace Bus_ticket.ViewModels;

public class LowOccupancyTripItemViewModel
{
    public string TripId { get; set; } = string.Empty;

    public string TripCode { get; set; } = string.Empty;

    public string BusCode { get; set; } = string.Empty;

    public string LicensePlate { get; set; } = string.Empty;

    public string BusType { get; set; } = string.Empty;

    public string RouteName { get; set; } = string.Empty;

    public DateTime DepartureTime { get; set; }

    public int TotalSeats { get; set; }

    public int BookedSeats { get; set; }

    public int EmptySeats => TotalSeats - BookedSeats;

    public double OccupancyRate { get; set; }

    public string WarningLabel { get; set; } = "Low Occupancy";
}

public class SoldOutTimeFrameItemViewModel
{
    public string TimeFrame { get; set; } = string.Empty;

    public int SoldOutTripCount { get; set; }

    public int TotalSeats { get; set; }

    public int BookedSeats { get; set; }
}

public class LowOccupancyTripsViewModel
{
    public DateTime FromDate { get; set; }

    public DateTime ToDate { get; set; }

    public double OccupancyThreshold { get; set; }

    public int TotalTrips { get; set; }

    public int LowOccupancyTripCount { get; set; }

    public int SoldOutTripCount { get; set; }

    public double AverageOccupancyRate { get; set; }

    public List<LowOccupancyTripItemViewModel> LowOccupancyTrips { get; set; } = new();

    public List<SoldOutTimeFrameItemViewModel> SoldOutTimeFrames { get; set; } = new();
}