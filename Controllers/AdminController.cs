using Bus_ticket.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bus_ticket.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        public AdminController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Trang chủ Admin
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Booking()
        {
            return View();
        }
    }
}