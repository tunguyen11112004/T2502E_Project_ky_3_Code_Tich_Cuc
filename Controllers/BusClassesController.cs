using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bus_ticket.Data;
using Bus_ticket.Helpers;
using Bus_ticket.Models;
using Bus_ticket.Services;
using Bus_ticket.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Bus_ticket.Controllers;

[Authorize(Roles = "Admin,Employee")]
public class BusClassesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ICloudinaryService _cloudinaryService;

    public BusClassesController(ApplicationDbContext context, ICloudinaryService cloudinaryService)
    {
        _context = context;
        _cloudinaryService = cloudinaryService;
    }

    public async Task<IActionResult> Index(string? searchTerm, int page = 1, int pageSize = 10)
    {
        var filter = Builders<BusClass>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            filter &= Builders<BusClass>.Filter.Regex(
                bc => bc.ClassName,
                new BsonRegularExpression(searchTerm.Trim(), "i"));
        }

        long totalItems = await _context.BusClasses.CountDocumentsAsync(filter);
        int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        if (page < 1) page = 1;
        if (page > totalPages && totalPages > 0) page = totalPages;

        var classes = await _context.BusClasses
            .Find(filter)
            .SortBy(bc => bc.ClassName)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        var classIds = classes.Select(c => c.Id).ToList();
        var buses = classIds.Count > 0
            ? await _context.Buses.Find(Builders<Bus>.Filter.In(b => b.BusClassId, classIds)).ToListAsync()
            : new List<Bus>();

        var busMap = buses
            .Where(b => !string.IsNullOrEmpty(b.BusClassId))
            .GroupBy(b => b.BusClassId!)
            .ToDictionary(g => g.Key, g => g.Select(b => b.LicensePlate).ToList());

        var items = classes.Select(c => new BusClassListItemViewModel
        {
            Id = c.Id,
            ClassName = c.ClassName,
            BusType = c.BusType,
            ImageUrl = c.ImageUrl,
            TotalRows = c.TotalRows,
            TotalColumns = c.TotalColumns,
            TotalSeats = c.TotalSeats,
            Status = string.IsNullOrWhiteSpace(c.Status) ? "Active" : c.Status,
            LicensePlates = busMap.GetValueOrDefault(c.Id, new List<string>())
        }).ToList();

        ViewBag.SearchTerm = searchTerm ?? string.Empty;
        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;

        return View(items);
    }

    public IActionResult Create()
    {
        return View(new BusClassFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BusClassFormViewModel model)
    {
        ModelState.Remove(nameof(model.Id));
        ModelState.Remove(nameof(model.ImageUrl));
        ModelState.Remove(nameof(model.ImagePublicId));
        ModelState.Remove(nameof(model.ImageFile));
        ModelState.Remove(nameof(model.LinkedBuses));

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var duplicate = await _context.BusClasses
            .Find(bc => bc.ClassName.ToLower() == model.ClassName.Trim().ToLower())
            .AnyAsync();
        if (duplicate)
        {
            ModelState.AddModelError(nameof(model.ClassName), "Tên hạng xe này đã tồn tại.");
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.LicensePlate))
        {
            var plateExists = await _context.Buses
                .Find(b => b.LicensePlate == model.LicensePlate.Trim())
                .AnyAsync();
            if (plateExists)
            {
                ModelState.AddModelError(nameof(model.LicensePlate), "Biển số xe này đã tồn tại.");
                return View(model);
            }
        }

        try
        {
            string imageUrl = string.Empty;
            string imagePublicId = string.Empty;
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                await using var stream = model.ImageFile.OpenReadStream();
                (imageUrl, imagePublicId) = await _cloudinaryService.UploadImageAsync(stream, model.ImageFile.FileName);
            }

            var layout = BusSeatLayoutGenerator.Generate(
                model.TotalRows, model.TotalColumns, model.TotalFloors, model.BusType);

            var busClass = new BusClass
            {
                ClassName = model.ClassName.Trim(),
                BusType = model.BusType,
                ImageUrl = imageUrl,
                ImagePublicId = imagePublicId,
                TotalRows = model.TotalRows,
                TotalColumns = model.TotalColumns,
                TotalFloors = model.TotalFloors,
                DefaultLayout = layout,
                TotalSeats = layout.Count,
                Status = model.Status,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "Admin",
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = User.Identity?.Name ?? "Admin"
            };

            await _context.BusClasses.InsertOneAsync(busClass);

            if (!string.IsNullOrWhiteSpace(model.LicensePlate))
            {
                await CreateBusForClassAsync(busClass.Id, model.LicensePlate.Trim(), model.Status);
            }

            TempData["SuccessMessage"] = "Thêm hạng xe thành công.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, "Lỗi khi lưu: " + ex.Message);
            return View(model);
        }
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var busClass = await _context.BusClasses.Find(bc => bc.Id == id).FirstOrDefaultAsync();
        if (busClass == null) return NotFound();

        var linkedBuses = await _context.Buses
            .Find(b => b.BusClassId == id)
            .ToListAsync();

        var model = new BusClassFormViewModel
        {
            Id = busClass.Id,
            ClassName = busClass.ClassName,
            BusType = busClass.BusType,
            TotalRows = busClass.TotalRows,
            TotalColumns = busClass.TotalColumns,
            TotalFloors = busClass.TotalFloors,
            Status = string.IsNullOrWhiteSpace(busClass.Status) ? "Active" : busClass.Status,
            ImageUrl = busClass.ImageUrl,
            ImagePublicId = busClass.ImagePublicId,
            LinkedBuses = linkedBuses.Select(b => new BusSummaryViewModel
            {
                Id = b.Id,
                LicensePlate = b.LicensePlate,
                Status = b.Status
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, BusClassFormViewModel model)
    {
        if (id != model.Id) return NotFound();

        ModelState.Remove(nameof(model.LinkedBuses));
        ModelState.Remove(nameof(model.LicensePlate));
        ModelState.Remove(nameof(model.ImageFile));
        ModelState.Remove(nameof(model.ImageUrl));
        ModelState.Remove(nameof(model.ImagePublicId));

        if (!ModelState.IsValid)
        {
            model.LinkedBuses = await LoadLinkedBusesAsync(id);
            return View(model);
        }

        var normalizedStatus = model.Status == "Inactive" ? "Inactive" : "Active";
        model.Status = normalizedStatus;

        var existing = await _context.BusClasses.Find(bc => bc.Id == id).FirstOrDefaultAsync();
        if (existing == null) return NotFound();

        try
        {
            string imageUrl = existing.ImageUrl;
            string imagePublicId = existing.ImagePublicId;

            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                await _cloudinaryService.DeleteImageAsync(existing.ImagePublicId);
                await using var stream = model.ImageFile.OpenReadStream();
                (imageUrl, imagePublicId) = await _cloudinaryService.UploadImageAsync(stream, model.ImageFile.FileName);
            }

            var layout = BusSeatLayoutGenerator.Generate(
                model.TotalRows, model.TotalColumns, model.TotalFloors, model.BusType);

            var update = Builders<BusClass>.Update
                .Set(bc => bc.ClassName, model.ClassName.Trim())
                .Set(bc => bc.BusType, model.BusType)
                .Set(bc => bc.ImageUrl, imageUrl)
                .Set(bc => bc.ImagePublicId, imagePublicId)
                .Set(bc => bc.TotalRows, model.TotalRows)
                .Set(bc => bc.TotalColumns, model.TotalColumns)
                .Set(bc => bc.TotalFloors, model.TotalFloors)
                .Set(bc => bc.DefaultLayout, layout)
                .Set(bc => bc.TotalSeats, layout.Count)
                .Set(bc => bc.Status, normalizedStatus)
                .Set(bc => bc.UpdatedAt, DateTime.UtcNow)
                .Set(bc => bc.UpdatedBy, User.Identity?.Name ?? "Admin");

            await _context.BusClasses.UpdateOneAsync(bc => bc.Id == id, update);

            var busStatus = normalizedStatus == "Inactive" ? "Inactive" : "Active";
            await _context.Buses.UpdateManyAsync(
                b => b.BusClassId == id,
                Builders<Bus>.Update
                    .Set(b => b.Status, busStatus)
                    .Set(b => b.UpdatedAt, DateTime.UtcNow)
                    .Set(b => b.UpdatedBy, User.Identity?.Name ?? "Admin"));

            TempData["SuccessMessage"] = "Cập nhật hạng xe thành công.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, "Lỗi khi cập nhật: " + ex.Message);
            model.LinkedBuses = await LoadLinkedBusesAsync(id);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBus(string busClassId, string licensePlate)
    {
        if (string.IsNullOrEmpty(busClassId) || string.IsNullOrWhiteSpace(licensePlate))
        {
            TempData["ErrorMessage"] = "Biển số xe không hợp lệ.";
            return RedirectToAction(nameof(Edit), new { id = busClassId });
        }

        var plate = licensePlate.Trim();
        var exists = await _context.Buses.Find(b => b.LicensePlate == plate).AnyAsync();
        if (exists)
        {
            TempData["ErrorMessage"] = "Biển số xe đã tồn tại trong hệ thống.";
            return RedirectToAction(nameof(Edit), new { id = busClassId });
        }

        await CreateBusForClassAsync(busClassId, plate, await GetBusClassStatusAsync(busClassId));
        TempData["SuccessMessage"] = "Thêm xe thành công.";
        return RedirectToAction(nameof(Edit), new { id = busClassId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBus(string busClassId, string busId)
    {
        if (string.IsNullOrEmpty(busId))
        {
            return RedirectToAction(nameof(Edit), new { id = busClassId });
        }

        var inUse = await _context.Trips
            .Find(Builders<Trip>.Filter.And(
                TripFilters.NotDeleted,
                Builders<Trip>.Filter.Eq(t => t.BusId, busId)))
            .AnyAsync();
        if (inUse)
        {
            TempData["ErrorMessage"] = "Không thể xóa xe! Xe đang được gán cho chuyến đi.";
            return RedirectToAction(nameof(Edit), new { id = busClassId });
        }

        await _context.Buses.DeleteOneAsync(b => b.Id == busId);
        TempData["SuccessMessage"] = "Xóa xe thành công.";
        return RedirectToAction(nameof(Edit), new { id = busClassId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var buses = await _context.Buses.Find(b => b.BusClassId == id).ToListAsync();
        foreach (var bus in buses)
        {
            var inTrip = await _context.Trips
                .Find(Builders<Trip>.Filter.And(
                    TripFilters.NotDeleted,
                    Builders<Trip>.Filter.Eq(t => t.BusId, bus.Id)))
                .AnyAsync();
            if (inTrip)
            {
                TempData["ErrorMessage"] = "Không thể xóa! Có xe trong hạng này đang được gán chuyến đi.";
                return RedirectToAction(nameof(Index));
            }
        }

        var busClass = await _context.BusClasses.Find(bc => bc.Id == id).FirstOrDefaultAsync();
        if (busClass != null)
        {
            await _cloudinaryService.DeleteImageAsync(busClass.ImagePublicId);
        }

        await _context.Buses.DeleteManyAsync(b => b.BusClassId == id);
        await _context.BusClasses.DeleteOneAsync(bc => bc.Id == id);

        TempData["SuccessMessage"] = "Xóa hạng xe và các xe liên kết thành công.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<string> GetBusClassStatusAsync(string busClassId)
    {
        var busClass = await _context.BusClasses.Find(bc => bc.Id == busClassId).FirstOrDefaultAsync();
        if (busClass == null || string.IsNullOrWhiteSpace(busClass.Status))
        {
            return "Active";
        }

        return busClass.Status == "Inactive" ? "Inactive" : "Active";
    }

    private async Task CreateBusForClassAsync(string busClassId, string licensePlate, string? classStatus = null)
    {
        var busStatus = classStatus == "Inactive" ? "Inactive" : "Active";

        var bus = new Bus
        {
            BusCode = "BUS-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper(),
            LicensePlate = licensePlate,
            BusClassId = busClassId,
            BranchId = DataSeeder.BranchHanoiId,
            Status = busStatus,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name ?? "Admin",
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = User.Identity?.Name ?? "Admin"
        };

        await _context.Buses.InsertOneAsync(bus);
    }

    private async Task<List<BusSummaryViewModel>> LoadLinkedBusesAsync(string busClassId)
    {
        var buses = await _context.Buses.Find(b => b.BusClassId == busClassId).ToListAsync();
        return buses.Select(b => new BusSummaryViewModel
        {
            Id = b.Id,
            LicensePlate = b.LicensePlate,
            Status = b.Status
        }).ToList();
    }
}
