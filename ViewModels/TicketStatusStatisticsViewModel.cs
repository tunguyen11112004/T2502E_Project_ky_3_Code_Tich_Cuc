namespace Bus_ticket.ViewModels;

public class TicketStatusStatisticsItemViewModel
{
    public string StatusKey { get; set; } = string.Empty;

    public string StatusLabel { get; set; } = string.Empty;

    public int BookingCount { get; set; }

    public int TicketCount { get; set; }

    public double Percentage { get; set; }
}

public class TicketStatusStatisticsViewModel
{
    public DateTime FromDate { get; set; }

    public DateTime ToDate { get; set; }

    public int SuccessfulBookings { get; set; }

    public int CancelledBookings { get; set; }

    public int SuccessfulTickets { get; set; }

    public int CancelledTickets { get; set; }

    public int TotalBookings => SuccessfulBookings + CancelledBookings;

    public int TotalTickets => SuccessfulTickets + CancelledTickets;

    public double SuccessfulPercentage { get; set; }

    public double CancelledPercentage { get; set; }

    public List<TicketStatusStatisticsItemViewModel> Items { get; set; } = new();
}