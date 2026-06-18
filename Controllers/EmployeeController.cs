using Bus_ticket.DTOs;
using Bus_ticket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bus_ticket.Controllers;

[Authorize(Roles = "Employee")]
public class EmployeeController : Controller
{
    private readonly ITicketSearchService _ticketSearchService;

    public EmployeeController(ITicketSearchService ticketSearchService)
    {
        _ticketSearchService = ticketSearchService;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> SearchTrips(string? from, string? to, DateTime? date, string? format)
    {
        var result = await _ticketSearchService.SearchAsync(new TicketSearchQuery
        {
            From = from,
            To = to,
            Date = date
        });

        ViewBag.From = from ?? string.Empty;
        ViewBag.To = to ?? string.Empty;
        ViewBag.Date = date?.ToString("yyyy-MM-dd") ?? string.Empty;

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            return Json(result);
        }

        return View(result);
    }
}
