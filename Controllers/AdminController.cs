using Bus_ticket.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System;
using System.Linq;

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

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string licensePlate, string vehicleClass, string route, decimal distanceKm)
        {
            if (string.IsNullOrWhiteSpace(licensePlate) || string.IsNullOrWhiteSpace(vehicleClass) || string.IsNullOrWhiteSpace(route))
            {
                ModelState.AddModelError(string.Empty, "Biển số, lớp xe và tuyến đường là bắt buộc.");
                return View();
            }

            var bus = new Bus
            {
                BusCode = GenerateBusCode(),
                LicensePlate = licensePlate,
                BusType = vehicleClass,
                TotalSeats = 0,
                TotalRows = 0,
                TotalColumns = 0,
                TotalFloors = 1,
                Status = "Active"
            };

            await _dbContext.Buses.InsertOneAsync(bus);

            return RedirectToAction("Index");
        }

        private static string GenerateBusCode()
        {
            return new Random().Next(10000, 99999).ToString();
        }
    }
}