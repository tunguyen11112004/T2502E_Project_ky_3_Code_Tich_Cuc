using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bus_ticket.Data;
using Bus_ticket.Helpers;
using Bus_ticket.Models;
using Bus_ticket.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Bus_ticket.Controllers;

[Authorize(Roles = "Admin,Employee")]
public class BusesController : Controller
{
    private readonly ApplicationDbContext _context;

    public BusesController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? searchTerm, int page = 1, int pageSize = 10)
    {
        var filter = BusFilters.NotDeleted;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var keyword = searchTerm.Trim();
            var regex = new BsonRegularExpression(keyword, "i");
            filter &= Builders<Bus>.Filter.Or(
                Builders<Bus>.Filter.Regex(b => b.LicensePlate, regex),
                Builders<Bus>.Filter.Regex(b => b.BusCode, regex));
        }

        long totalItems = await _context.Buses.CountDocumentsAsync(filter);
        int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        if (page < 1) page = 1;
        if (page > totalPages && totalPages > 0) page = totalPages;

        var buses = await _context.Buses
            .Find(filter)
            .SortByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        var busClasses = await _context.BusClasses.Find(_ => true).ToListAsync();
        var branches = await _context.Branches.Find(_ => true).ToListAsync();
        var operators = await _context.BusOperators.Find(_ => true).ToListAsync();

        var items = buses.Select(b => new BusListItemViewModel
        {
            Id = b.Id,
            BusCode = b.BusCode,
            LicensePlate = b.LicensePlate,
            BusClassName = busClasses.FirstOrDefault(c => c.Id == b.BusClassId)?.ClassName ?? "—",
            TotalSeats = busClasses.FirstOrDefault(c => c.Id == b.BusClassId)?.TotalSeats ?? 0,
            BranchName = branches.FirstOrDefault(br => br.Id == b.BranchId)?.BranchName ?? "—",
            OperatorName = operators.FirstOrDefault(o => o.Id == b.OperatorId)?.OperatorName ?? "—",
            Status = NormalizeStatus(b.Status)
        }).ToList();

        ViewBag.SearchTerm = searchTerm ?? string.Empty;
        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;

        ViewBag.CreateForm = new BusFormViewModel
        {
            BusClassOptions = await BuildBusClassOptionsAsync(),
            BranchOptions = await BuildBranchOptionsAsync(),
            OperatorOptions = await BuildOperatorOptionsAsync()
        };

        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BusFormViewModel model)
    {
        ModelState.Remove(nameof(model.Id));

        if (await IsDuplicateAsync(model.LicensePlate, model.BusCode))
        {
            ModelState.AddModelError(nameof(model.LicensePlate), "Biển số hoặc mã xe đã tồn tại.");
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Thêm xe thất bại. Vui lòng kiểm tra dữ liệu.";
            return RedirectToAction(nameof(Index));
        }

        var bus = new Bus
        {
            Id = ObjectId.GenerateNewId().ToString(),
            BusCode = model.BusCode.Trim(),
            LicensePlate = model.LicensePlate.Trim().ToUpperInvariant(),
            BusClassId = model.BusClassId,
            BranchId = string.IsNullOrWhiteSpace(model.BranchId) ? null : model.BranchId,
            OperatorId = string.IsNullOrWhiteSpace(model.OperatorId) ? null : model.OperatorId,
            Status = NormalizeStatus(model.Status),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name ?? "Admin",
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = User.Identity?.Name ?? "Admin"
        };

        await _context.Buses.InsertOneAsync(bus);
        TempData["SuccessMessage"] = "Thêm xe mới thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetForEdit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var bus = await _context.Buses.Find(b => b.Id == id).FirstOrDefaultAsync();
        if (bus == null || bus.DeletedAt.HasValue) return NotFound();

        return Json(new
        {
            id = bus.Id,
            busCode = bus.BusCode,
            licensePlate = bus.LicensePlate,
            busClassId = bus.BusClassId ?? "",
            branchId = bus.BranchId ?? "",
            operatorId = bus.OperatorId ?? "",
            status = NormalizeStatus(bus.Status)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, BusFormViewModel model)
    {
        if (id != model.Id) return NotFound();

        var existing = await _context.Buses.Find(b => b.Id == id).FirstOrDefaultAsync();
        if (existing == null || existing.DeletedAt.HasValue) return NotFound();

        if (await IsDuplicateAsync(model.LicensePlate, model.BusCode, id))
        {
            TempData["ErrorMessage"] = "Biển số hoặc mã xe đã tồn tại trên xe khác.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Cập nhật thất bại.";
            return RedirectToAction(nameof(Index));
        }

        var update = Builders<Bus>.Update
            .Set(b => b.BusCode, model.BusCode.Trim())
            .Set(b => b.LicensePlate, model.LicensePlate.Trim().ToUpperInvariant())
            .Set(b => b.BusClassId, model.BusClassId)
            .Set(b => b.BranchId, string.IsNullOrWhiteSpace(model.BranchId) ? null : model.BranchId)
            .Set(b => b.OperatorId, string.IsNullOrWhiteSpace(model.OperatorId) ? null : model.OperatorId)
            .Set(b => b.Status, NormalizeStatus(model.Status))
            .Set(b => b.UpdatedAt, DateTime.UtcNow)
            .Set(b => b.UpdatedBy, User.Identity?.Name ?? "Admin");

        await _context.Buses.UpdateOneAsync(b => b.Id == id, update);
        TempData["SuccessMessage"] = "Cập nhật xe thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var bus = await _context.Buses.Find(b => b.Id == id).FirstOrDefaultAsync();
        if (bus == null) return NotFound();

        var inUse = await _context.Trips
            .Find(Builders<Trip>.Filter.And(TripFilters.NotDeleted, Builders<Trip>.Filter.Eq(t => t.BusId, id)))
            .AnyAsync();

        var currentUser = User.Identity?.Name ?? "Admin";

        if (inUse)
        {
            var deactivate = Builders<Bus>.Update
                .Set(b => b.Status, "Inactive")
                .Set(b => b.UpdatedAt, DateTime.UtcNow)
                .Set(b => b.UpdatedBy, currentUser);

            await _context.Buses.UpdateOneAsync(b => b.Id == id, deactivate);
            TempData["ErrorMessage"] = "Xe đang được sử dụng, chỉ chuyển sang Ngưng hoạt động.";
        }
        else
        {
            var softDelete = Builders<Bus>.Update
                .Set(b => b.DeletedAt, DateTime.UtcNow)
                .Set(b => b.DeletedBy, currentUser)
                .Set(b => b.Status, "Inactive")
                .Set(b => b.UpdatedAt, DateTime.UtcNow)
                .Set(b => b.UpdatedBy, currentUser);

            await _context.Buses.UpdateOneAsync(b => b.Id == id, softDelete);
            TempData["SuccessMessage"] = "Xóa xe thành công.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> IsDuplicateAsync(string licensePlate, string busCode, string? excludeId = null)
    {
        var plateKey = licensePlate.Trim().ToUpperInvariant();
        var codeKey = busCode.Trim();

        var filter = Builders<Bus>.Filter.And(
            BusFilters.NotDeleted,
            Builders<Bus>.Filter.Or(
                Builders<Bus>.Filter.Eq(b => b.LicensePlate, plateKey),
                Builders<Bus>.Filter.Eq(b => b.BusCode, codeKey)));

        if (!string.IsNullOrEmpty(excludeId))
            filter &= Builders<Bus>.Filter.Ne(b => b.Id, excludeId);

        return await _context.Buses.Find(filter).AnyAsync();
    }

    private static string NormalizeStatus(string? status)
    {
        return status?.ToLower() switch
        {
            "maintenance" => "Maintenance",
            "inactive" => "Inactive",
            _ => "Active"
        };
    }

    private async Task<List<SelectListItem>> BuildBusClassOptionsAsync()
    {
        var classes = await _context.BusClasses.Find(BusClassFilters.NotDeleted)
            .SortBy(c => c.ClassName).ToListAsync();
        return classes.Select(c => new SelectListItem 
        { 
            Value = c.Id, 
            Text = $"{c.ClassName} ({c.TotalSeats} ghế)" 
        }).ToList();
    }

    private async Task<List<SelectListItem>> BuildBranchOptionsAsync()
    {
        var branches = await _context.Branches.Find(_ => true)
            .SortBy(b => b.BranchName).ToListAsync();
        return branches.Select(b => new SelectListItem 
        { 
            Value = b.Id, 
            Text = b.BranchName 
        }).ToList();
    }

    private async Task<List<SelectListItem>> BuildOperatorOptionsAsync()
    {
        var operators = await _context.BusOperators.Find(_ => true)
            .SortBy(o => o.OperatorName).ToListAsync();
        return operators.Select(o => new SelectListItem 
        { 
            Value = o.Id, 
            Text = o.OperatorName 
        }).ToList();
    }
}