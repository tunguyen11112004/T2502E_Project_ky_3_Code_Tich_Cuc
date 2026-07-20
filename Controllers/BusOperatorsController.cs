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
public class BusOperatorsController : Controller
{
    private readonly ApplicationDbContext _context;

    public BusOperatorsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? searchTerm, string? status, int page = 1, int pageSize = 10)
    {
        var filter = Builders<BusOperator>.Filter.Empty;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var keyword = searchTerm.Trim();
            var regex = new BsonRegularExpression(keyword, "i");
            filter &= Builders<BusOperator>.Filter.Or(
                Builders<BusOperator>.Filter.Regex(o => o.OperatorName, regex),
                Builders<BusOperator>.Filter.Regex(o => o.OperatorCode, regex),
                Builders<BusOperator>.Filter.Regex(o => o.ContactEmail, regex),
                Builders<BusOperator>.Filter.Regex(o => o.PhoneNumber, regex));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= Builders<BusOperator>.Filter.Eq(o => o.Status, status);
        }

        long totalItems = await _context.BusOperators.CountDocumentsAsync(filter);
        int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        if (page < 1) page = 1;
        if (page > totalPages && totalPages > 0) page = totalPages;

        var operators = await _context.BusOperators
            .Find(filter)
            .SortBy(o => o.OperatorName)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        var items = operators.Select(o => new BusOperatorListItemViewModel
        {
            Id = o.Id,
            OperatorCode = o.OperatorCode,
            OperatorName = o.OperatorName,
            ContactEmail = o.ContactEmail,
            PhoneNumber = o.PhoneNumber,
            TaxCode = string.IsNullOrWhiteSpace(o.TaxCode) ? "—" : o.TaxCode,
            Status = o.Status
        }).ToList();

        ViewBag.SearchTerm = searchTerm ?? string.Empty;
        ViewBag.Status = status ?? string.Empty;
        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalItems = totalItems;

        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BusOperatorFormViewModel model)
    {
        ModelState.Remove(nameof(model.Id));

        if (await IsDuplicateCodeAsync(model.OperatorCode))
        {
            ModelState.AddModelError(nameof(model.OperatorCode), "Mã nhà xe đã tồn tại.");
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Thêm nhà xe thất bại. Vui lòng kiểm tra dữ liệu.";
            return RedirectToAction(nameof(Index));
        }

        var currentUser = User.Identity?.Name ?? "Admin";

        var op = new BusOperator
        {
            Id = ObjectId.GenerateNewId().ToString(),
            OperatorCode = model.OperatorCode.Trim(),
            OperatorName = model.OperatorName.Trim(),
            ContactEmail = model.ContactEmail.Trim(),
            PhoneNumber = model.PhoneNumber.Trim(),
            TaxCode = string.IsNullOrWhiteSpace(model.TaxCode) ? null : model.TaxCode.Trim(),
            Status = NormalizeStatus(model.Status),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = currentUser,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = currentUser
        };

        await _context.BusOperators.InsertOneAsync(op);
        TempData["SuccessMessage"] = "Thêm nhà xe thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetForEdit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var op = await _context.BusOperators.Find(o => o.Id == id).FirstOrDefaultAsync();
        if (op == null) return NotFound();

        return Json(new
        {
            id = op.Id,
            operatorCode = op.OperatorCode,
            operatorName = op.OperatorName,
            contactEmail = op.ContactEmail,
            phoneNumber = op.PhoneNumber,
            taxCode = op.TaxCode ?? "",
            status = NormalizeStatus(op.Status)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(BusOperatorFormViewModel model)
    {
        if (string.IsNullOrEmpty(model.Id)) return NotFound();

        var existing = await _context.BusOperators.Find(o => o.Id == model.Id).FirstOrDefaultAsync();
        if (existing == null) return NotFound();

        if (await IsDuplicateCodeAsync(model.OperatorCode, model.Id))
        {
            TempData["ErrorMessage"] = "Mã nhà xe đã tồn tại trên một nhà xe khác.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Cập nhật nhà xe thất bại. Vui lòng kiểm tra dữ liệu.";
            return RedirectToAction(nameof(Index));
        }

        var update = Builders<BusOperator>.Update
            .Set(o => o.OperatorCode, model.OperatorCode.Trim())
            .Set(o => o.OperatorName, model.OperatorName.Trim())
            .Set(o => o.ContactEmail, model.ContactEmail.Trim())
            .Set(o => o.PhoneNumber, model.PhoneNumber.Trim())
            .Set(o => o.TaxCode, string.IsNullOrWhiteSpace(model.TaxCode) ? null : model.TaxCode.Trim())
            .Set(o => o.Status, NormalizeStatus(model.Status))
            .Set(o => o.UpdatedAt, DateTime.UtcNow)
            .Set(o => o.UpdatedBy, User.Identity?.Name ?? "Admin");

        await _context.BusOperators.UpdateOneAsync(o => o.Id == model.Id, update);
        TempData["SuccessMessage"] = "Cập nhật nhà xe thành công!";
        return RedirectToAction(nameof(Index));
    }

    // POST: /BusOperators/Delete/{id}
    // Ngưng hợp tác (Xóa mềm): không xóa cứng dữ liệu, chỉ chuyển trạng thái sang "Inactive"
    // để bảo toàn dữ liệu đối soát doanh thu ở các bảng thống kê.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var op = await _context.BusOperators.Find(o => o.Id == id).FirstOrDefaultAsync();
        if (op == null) return NotFound();

        var update = Builders<BusOperator>.Update
            .Set(o => o.Status, "Inactive")
            .Set(o => o.UpdatedAt, DateTime.UtcNow)
            .Set(o => o.UpdatedBy, User.Identity?.Name ?? "Admin");

        await _context.BusOperators.UpdateOneAsync(o => o.Id == id, update);
        TempData["SuccessMessage"] = "Đã ngưng hợp tác với nhà xe.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> IsDuplicateCodeAsync(string operatorCode, string? excludeId = null)
    {
        var codeKey = operatorCode.Trim();
        var filter = Builders<BusOperator>.Filter.Eq(o => o.OperatorCode, codeKey);

        if (!string.IsNullOrEmpty(excludeId))
        {
            filter &= Builders<BusOperator>.Filter.Ne(o => o.Id, excludeId);
        }

        return await _context.BusOperators.Find(filter).AnyAsync();
    }

    private static string NormalizeStatus(string? status)
    {
        return string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase) ? "Inactive" : "Active";
    }
}