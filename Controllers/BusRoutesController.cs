using Bus_ticket.Data;
using Bus_ticket.Models;
using Bus_ticket.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Bus_ticket.Controllers;

[Authorize(Roles = "Admin,Employee")]
public class BusRoutesController : Controller
{
    private readonly ApplicationDbContext _context;

    public BusRoutesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var routes = await _context.BusRoutes
            .Find(_ => true)
            .SortBy(r => r.DeparturePoint)
            .ThenBy(r => r.DestinationPoint)
            .ToListAsync();

        return View(routes);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BusRouteFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ thông tin.";
            return RedirectToAction(nameof(Index));
        }

        var existed = await _context.BusRoutes
            .Find(r =>
                r.DeparturePoint == model.DeparturePoint &&
                r.DestinationPoint == model.DestinationPoint)
            .AnyAsync();

        if (existed)
        {
            TempData["ErrorMessage"] = "Tuyến đường đã tồn tại.";
            return RedirectToAction(nameof(Index));
        }

        var route = new BusRoute
        {
            Id = ObjectId.GenerateNewId().ToString(),
            DeparturePoint = model.DeparturePoint.Trim(),
            DestinationPoint = model.DestinationPoint.Trim(),
            DistanceKm = model.DistanceKm,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name ?? "Admin",
            UpdatedBy = User.Identity?.Name ?? "Admin"
        };

        await _context.BusRoutes.InsertOneAsync(route);

        TempData["SuccessMessage"] = "Thêm tuyến đường thành công.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetForEdit(string id)
    {
        var route = await _context.BusRoutes
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync();

        if (route == null)
            return NotFound();

        return Json(new
        {
            id = route.Id,
            departurePoint = route.DeparturePoint,
            destinationPoint = route.DestinationPoint,
            distanceKm = route.DistanceKm
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(BusRouteFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var existed = await _context.BusRoutes
            .Find(r =>
                r.Id != model.Id &&
                r.DeparturePoint == model.DeparturePoint &&
                r.DestinationPoint == model.DestinationPoint)
            .AnyAsync();

        if (existed)
        {
            TempData["ErrorMessage"] = "Tuyến đường đã tồn tại.";
            return RedirectToAction(nameof(Index));
        }

        var update = Builders<BusRoute>.Update
            .Set(x => x.DeparturePoint, model.DeparturePoint.Trim())
            .Set(x => x.DestinationPoint, model.DestinationPoint.Trim())
            .Set(x => x.DistanceKm, model.DistanceKm)
            .Set(x => x.UpdatedAt, DateTime.UtcNow)
            .Set(x => x.UpdatedBy, User.Identity?.Name ?? "Admin");

        await _context.BusRoutes.UpdateOneAsync(
            x => x.Id == model.Id,
            update);

        TempData["SuccessMessage"] = "Cập nhật tuyến đường thành công.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var route = await _context.BusRoutes
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync();

        if (route == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy tuyến đường.";
            return RedirectToAction(nameof(Index));
        }

        await _context.BusRoutes.DeleteOneAsync(x => x.Id == id);

        TempData["SuccessMessage"] = "Đã xóa tuyến đường.";

        return RedirectToAction(nameof(Index));
    }
}