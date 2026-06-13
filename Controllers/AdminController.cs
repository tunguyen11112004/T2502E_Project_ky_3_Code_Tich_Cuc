using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Bus_ticket.Controllers
{   [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        // Định tuyến truy cập vào trang chủ Admin (Ví dụ: https://localhost:xxxx/Admin)
        public IActionResult Index()
        {
            return View();
        }public IActionResult Booking() { return View(); }
    }
}
