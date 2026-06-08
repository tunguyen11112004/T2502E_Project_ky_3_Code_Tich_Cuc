using Microsoft.AspNetCore.Mvc;

namespace Bus_ticket.Controllers
{
    public class AdminController : Controller
    {
        // Định tuyến truy cập vào trang chủ Admin (Ví dụ: https://localhost:xxxx/Admin)
        public IActionResult Index()
        {
            return View();
        }
    }
}