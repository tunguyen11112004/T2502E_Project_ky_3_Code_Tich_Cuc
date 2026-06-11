using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Bus_ticket.Controllers
{   [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        // Định tuyến truy cập vào trang chủ Admin (Ví dụ: https://localhost:xxxx/Admin)
        public IActionResult Index()
        {
            return View();
        }
    }
}