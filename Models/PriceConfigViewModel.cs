namespace Bus_ticket.Models;

public class PriceConfigViewModel
{
    public string ClassName { get; set; } = string.Empty;
    public string BusType { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int TotalSeats { get; set; }
    public decimal BasePrice { get; set; }
    public string Amenities { get; set; } = string.Empty;
    public bool IsHot { get; set; }
}