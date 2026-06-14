using Bus_ticket.Models;
using Bus_ticket.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bus_ticket.Controllers;

public class BusesController : Controller
{
    private readonly IBusService _busService;
    private readonly ICloudinaryService _cloudinaryService;

    public BusesController(
        IBusService busService,
        ICloudinaryService cloudinaryService)
    {
        _busService = busService;
        _cloudinaryService = cloudinaryService;
    }

    public async Task<IActionResult> Index()
    {
        var buses = await _busService.GetAllAsync();

        return View(buses);
    }

    public IActionResult New()
    {
        return View(new Bus
        {
            DepartureTime = DateTime.Now.AddHours(1)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(Bus bus)
    {
        ModelState.Remove(nameof(Bus.BusCode));

        bus.BusNumber = bus.BusNumber?.Trim().ToUpper() ?? "";
        bus.Route = bus.Route?.Trim() ?? "";
        bus.BusCode = await GenerateUniqueBusCodeAsync();

        if (await _busService.BusNumberExistsAsync(bus.BusNumber))
        {
            ModelState.AddModelError(nameof(Bus.BusNumber), "Số xe đã tồn tại");
        }

        if (!ModelState.IsValid)
        {
            return View(bus);
        }

        try
        {
            bus.ImageUrl = await _cloudinaryService.UploadImageAsync(bus.ImageFile);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(nameof(Bus.ImageFile), ex.Message);
            return View(bus);
        }

        bus.Status = BusStatus.Active;

        await _busService.CreateAsync(bus);

        TempData["Success"] = "Thêm xe khách thành công";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelSelected(List<string> ids)
    {
        if (ids == null || ids.Count == 0)
        {
            return Json(new
            {
                success = false,
                message = "Bạn chưa chọn xe nào"
            });
        }

        await _busService.CancelManyAsync(ids);

        return Json(new
        {
            success = true,
            message = $"Đã hủy {ids.Count} xe"
        });
    }

    private async Task<string> GenerateUniqueBusCodeAsync()
    {
        while (true)
        {
            var code = Random.Shared.Next(10000, 100000).ToString();

            var exists = await _busService.BusCodeExistsAsync(code);

            if (!exists)
            {
                return code;
            }
        }
    }
}

public interface ICloudinaryService
{
    Task<string?> UploadImageAsync(IFormFile? busImageFile);
}