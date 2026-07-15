namespace Bus_ticket.ViewModels;

public class OperatorRevenueViewModel
{
    public string OperatorId { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public decimal TotalRevenue { get; set; }
    public int TotalBookings { get; set; }
}