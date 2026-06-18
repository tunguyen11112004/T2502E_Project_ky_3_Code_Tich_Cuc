using Bus_ticket.DTOs;

namespace Bus_ticket.Services;

public interface ITicketSearchService
{
    Task<TicketSearchResponse> SearchAsync(TicketSearchQuery query);
}
