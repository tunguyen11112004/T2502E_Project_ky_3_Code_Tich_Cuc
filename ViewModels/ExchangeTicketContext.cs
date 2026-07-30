using System;

namespace Bus_ticket.ViewModels;

public class ExchangeTicketContext
{
    public string BookingCode { get; set; } = string.Empty;
    public string SeatNumber { get; set; } = string.Empty;
    public decimal OldAmount { get; set; }
    public string PassengerName { get; set; } = string.Empty;
    public string PassengerPhone { get; set; } = string.Empty;
    public string PassengerEmail { get; set; } = string.Empty;
    public DateTime PassengerDob { get; set; }
    public string OldRouteName { get; set; } = string.Empty;
    public string OldTripId { get; set; } = string.Empty;
    public DateTime OldDepartureUtc { get; set; }
    public string OldDepartureDisplay { get; set; } = string.Empty;
    public decimal PenaltyPercent { get; set; }
    public string PenaltyPercentDisplay { get; set; } = string.Empty;
}
