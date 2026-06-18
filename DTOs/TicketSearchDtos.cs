namespace Bus_ticket.DTOs;

public class TicketSearchQuery
{
    public string? From { get; set; }
    public string? To { get; set; }
    public DateTime? Date { get; set; }
}

public class SeatStatusDto
{
    public string SeatNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Available";
}

public class TicketSearchResultDto
{
    public string TripCode { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string BusClass { get; set; } = string.Empty;
    public DateTime DepartureTime { get; set; }
    public string DeparturePoint { get; set; } = string.Empty;
    public string DestinationPoint { get; set; } = string.Empty;
    public List<SeatStatusDto> Seats { get; set; } = new();
}

public class TicketSearchResponse
{
    public int Total { get; set; }
    public List<TicketSearchResultDto> Data { get; set; } = new();
}
