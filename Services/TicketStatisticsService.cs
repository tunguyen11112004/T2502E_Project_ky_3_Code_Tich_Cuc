using Bus_ticket.Data;
using Bus_ticket.Models;
using Bus_ticket.ViewModels;
using MongoDB.Driver;

namespace Bus_ticket.Services;

public class TicketStatisticsService
{
    private readonly ApplicationDbContext _dbContext;

    private static readonly string[] SuccessfulStatuses =
    {
        "completed"
    };

    private static readonly string[] CancelledStatuses =
    {
        "cancelled",
        "canceled"
    };

    private static readonly string[] CancelledPaymentStatuses =
    {
        "refunded"
    };

    public TicketStatisticsService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TicketStatusStatisticsViewModel> GetTicketStatusStatisticsAsync(DateTime fromDate, DateTime toDate)
    {
        var (fromUtc, toUtc) = ToUtcDateRange(fromDate, toDate);

        var bookings = await _dbContext.Bookings
            .Find(booking => booking.BookingTime >= fromUtc && booking.BookingTime <= toUtc)
            .ToListAsync();

        var cancelledBookings = bookings
            .Where(IsCancelledBooking)
            .ToList();

        var successfulBookings = bookings
            .Where(booking =>
                IsStatusInGroup(booking.BookingStatus, SuccessfulStatuses)
                && !IsCancelledBooking(booking))
            .ToList();

        var successfulTickets = successfulBookings.Sum(GetTicketCount);
        var cancelledTickets = cancelledBookings.Sum(GetTicketCount);
        var totalTickets = successfulTickets + cancelledTickets;

        var successfulPercentage = totalTickets > 0
            ? Math.Round((double)successfulTickets / totalTickets * 100, 2)
            : 0;

        var cancelledPercentage = totalTickets > 0
            ? Math.Round((double)cancelledTickets / totalTickets * 100, 2)
            : 0;

        var items = new List<TicketStatusStatisticsItemViewModel>
        {
            new()
            {
                StatusKey = "successful",
                StatusLabel = "Vé thành công",
                BookingCount = successfulBookings.Count,
                TicketCount = successfulTickets,
                Percentage = successfulPercentage
            },
            new()
            {
                StatusKey = "cancelled",
                StatusLabel = "Vé hủy",
                BookingCount = cancelledBookings.Count,
                TicketCount = cancelledTickets,
                Percentage = cancelledPercentage
            }
        }
        .OrderByDescending(item => item.TicketCount)
        .ThenByDescending(item => item.BookingCount)
        .ThenBy(item => item.StatusLabel)
        .ToList();

        return new TicketStatusStatisticsViewModel
        {
            FromDate = fromDate.Date,
            ToDate = toDate.Date,
            SuccessfulBookings = successfulBookings.Count,
            CancelledBookings = cancelledBookings.Count,
            SuccessfulTickets = successfulTickets,
            CancelledTickets = cancelledTickets,
            SuccessfulPercentage = successfulPercentage,
            CancelledPercentage = cancelledPercentage,
            Items = items
        };
    }

    private static bool IsCancelledBooking(Booking booking)
    {
        if (booking.Cancellation != null)
        {
            return true;
        }

        if (IsStatusInGroup(booking.BookingStatus, CancelledStatuses))
        {
            return true;
        }

        if (IsStatusInGroup(booking.PaymentStatus, CancelledPaymentStatuses))
        {
            return true;
        }

        return false;
    }

    private static (DateTime FromUtc, DateTime ToUtc) ToUtcDateRange(DateTime fromDate, DateTime toDate)
    {
        var from = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(toDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

        return (from, to);
    }

    private static bool IsStatusInGroup(string? status, IReadOnlyCollection<string> validStatuses)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;

        var normalizedStatus = status.Trim().ToLowerInvariant();
        return validStatuses.Contains(normalizedStatus);
    }

    private static int GetTicketCount(Booking booking)
    {
        return booking.Passengers?.Count > 0 ? booking.Passengers.Count : 1;
    }
}