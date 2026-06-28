namespace Bus_ticket.ViewModels;

public class BookingTripItemViewModel
{
    public string TripId { get; set; } = string.Empty;
    public string TripCode { get; set; } = string.Empty;
    public string DeparturePoint { get; set; } = string.Empty;
    public string DestinationPoint { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public decimal BaseFare { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsReturnLeg { get; set; }
}

public class BookingIndexViewModel
{
    public List<BookingTripItemViewModel> Trips { get; set; } = new();
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public DateTime? ReturnDate { get; set; }
    public bool UseNewAddress { get; set; }
    public bool ShowReturnDate { get; set; }
}
