using System.Security.Claims;
using Bus_ticket.Models;
using Bus_ticket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bus_ticket.Controllers;

[Authorize(Roles = "Admin,Employee")]
public class BusOperatorsController : Controller
{
    private readonly BusOperatorService _busOperatorService;

    public BusOperatorsController(BusOperatorService busOperatorService)
    {
        _busOperatorService = busOperatorService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm, string? status, int page = 1, int pageSize = 10)
    {
        var result = await _busOperatorService.GetPagedAsync(searchTerm, status, page, pageSize);

        ViewBag.SearchTerm = searchTerm ?? string.Empty;
        ViewBag.Status = status ?? string.Empty;
        ViewBag.CurrentPage = result.CurrentPage;
        ViewBag.PageSize = result.PageSize;
        ViewBag.TotalPages = result.TotalPages;
        ViewBag.TotalItems = result.TotalItems;
        ViewBag.SuggestedOperatorCode = await _busOperatorService.GenerateOperatorCodeAsync();
        ViewBag.OpenCreateModal = TempData["OpenCreateModal"] is true;
        ViewBag.OpenEditModalId = TempData["OpenEditModalId"] as string;

        return View(result.Items);
    }

    [HttpGet]
    public async Task<IActionResult> SuggestOperatorCode(string? operatorName)
    {
        var code = await _busOperatorService.GenerateOperatorCodeAsync(operatorName);
        return Json(new { code });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BusOperator model)
    {
        RemoveSystemModelStateKeys();

        if (!ModelState.IsValid)
        {
            TempData["OpenCreateModal"] = true;
            TempData["ErrorMessage"] = "Vui lòng kiểm tra lại thông tin nhà xe.";
            return RedirectToIndexWithQuery();
        }

        try
        {
            await _busOperatorService.CreateAsync(model, GetCurrentActor());
            TempData["SuccessMessage"] = "Thêm đối tác nhà xe thành công.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["OpenCreateModal"] = true;
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToIndexWithQuery();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, BusOperator model)
    {
        RemoveSystemModelStateKeys();

        if (!ModelState.IsValid)
        {
            TempData["OpenEditModalId"] = id;
            TempData["ErrorMessage"] = "Vui lòng kiểm tra lại thông tin nhà xe.";
            return RedirectToIndexWithQuery();
        }

        try
        {
            var updated = await _busOperatorService.UpdateAsync(id, model);
            if (updated == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đối tác nhà xe.";
            }
            else
            {
                TempData["SuccessMessage"] = "Cập nhật đối tác nhà xe thành công.";
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["OpenEditModalId"] = id;
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToIndexWithQuery();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _busOperatorService.SoftDeleteAsync(id);

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = result.Message;
        }
        else
        {
            TempData["ErrorMessage"] = result.Message;
        }

        return RedirectToIndexWithQuery();
    }

    private IActionResult RedirectToIndexWithQuery()
    {
        return RedirectToAction(nameof(Index), new
        {
            searchTerm = Request.Query["searchTerm"].ToString(),
            status = Request.Query["status"].ToString(),
            page = Request.Query["page"].ToString()
        });
    }

    private string GetCurrentActor()
    {
        return User.FindFirstValue(ClaimTypes.Email)
               ?? User.Identity?.Name
               ?? "System Admin";
    }

    private void RemoveSystemModelStateKeys()
    {
        ModelState.Remove(nameof(BusOperator.Id));
        ModelState.Remove(nameof(BusOperator.CreatedAt));
        ModelState.Remove(nameof(BusOperator.CreatedBy));
    }
}
