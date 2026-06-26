using Bus_ticket.Data;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace Bus_ticket.Controllers;

public class CustomerController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public CustomerController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    [HttpGet]
    public async Task<IActionResult> GetCustomerByPhone(string phone)
    {
        var customer = await _dbContext.Customers
            .Find(c => c.PhoneNumber == phone)
            .FirstOrDefaultAsync();

        if (customer == null) return NotFound();

        return Json(customer);
    }
}