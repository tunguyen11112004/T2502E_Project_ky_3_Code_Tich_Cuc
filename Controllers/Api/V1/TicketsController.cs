using Bus_ticket.DTOs;
using Bus_ticket.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bus_ticket.Controllers.Api.V1;

[ApiController]
[Route("api/v1/tickets")]
public class TicketsController : ControllerBase
{
    private readonly ITicketSearchService _ticketSearchService;

    public TicketsController(ITicketSearchService ticketSearchService)
    {
        _ticketSearchService = ticketSearchService;
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(TicketSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] TicketSearchQuery query)
    {
        var result = await _ticketSearchService.SearchAsync(query);
        return Ok(result);
    }
}
