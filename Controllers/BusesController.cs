using System.Security.Claims;
using Bus_ticket.Services;
using Bus_ticket.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bus_ticket.Controllers;

[Authorize(Roles = "Admin,Employee")]
public class BusesController : Controller
{
    private readonly BusService _busService;

    public BusesController(BusService busService)
    {
        _busService = busService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? searchTerm,
        string? status,
        string? busClassId,
        string? branchId,
        int page = 1,
        int pageSize = 10)
    {
        var model = await BuildIndexViewModelAsync(searchTerm, status, busClassId, branchId, page, pageSize);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BusFormViewModel model)
    {
        RemoveOptionalModelStateKeys();

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = GetFirstModelStateError() ?? "Dữ liệu thêm xe chưa hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _busService.CreateAsync(model, GetCurrentActor());
            TempData["SuccessMessage"] = "Thêm xe khách mới thành công.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, BusFormViewModel model)
    {
        RemoveOptionalModelStateKeys();

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = GetFirstModelStateError() ?? "Dữ liệu cập nhật xe chưa hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var updatedBus = await _busService.UpdateAsync(id, model, GetCurrentActor());
            if (updatedBus == null)
            {
                return NotFound();
            }

            TempData["SuccessMessage"] = "Cập nhật xe thành công. Ma trận ghế cũ được giữ nguyên, không bị reset.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _busService.DeleteAsync(id, GetCurrentActor());

        if (result.Succeeded)
        {
            TempData[result.SoftDeleted ? "WarningMessage" : "SuccessMessage"] = result.Message;
        }
        else
        {
            TempData["ErrorMessage"] = result.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<BusIndexViewModel> BuildIndexViewModelAsync(
        string? searchTerm,
        string? status,
        string? busClassId,
        string? branchId,
        int page,
        int pageSize)
    {
        var pagedResult = await _busService.GetPagedAsync(searchTerm, status, busClassId, branchId, page, pageSize);
        var busClasses = await _busService.GetBusClassOptionsAsync();
        var branches = await _busService.GetBranchOptionsAsync();

        return new BusIndexViewModel
        {
            Items = pagedResult.Items,
            BusClasses = busClasses,
            Branches = branches,
            SearchTerm = searchTerm ?? string.Empty,
            Status = status ?? string.Empty,
            BusClassId = busClassId ?? string.Empty,
            BranchId = branchId ?? string.Empty,
            CurrentPage = pagedResult.CurrentPage,
            PageSize = pagedResult.PageSize,
            TotalPages = pagedResult.TotalPages,
            TotalItems = pagedResult.TotalItems,
            CreateForm = new BusFormViewModel
            {
                Status = "Active",
                BusClassId = busClasses.FirstOrDefault()?.Id ?? string.Empty,
                BranchId = branches.FirstOrDefault()?.Id
            }
        };
    }

    private string GetCurrentActor()
    {
        return User.FindFirstValue(ClaimTypes.Email)
               ?? User.Identity?.Name
               ?? "System Admin";
    }

    private void RemoveOptionalModelStateKeys()
    {
        ModelState.Remove(nameof(BusFormViewModel.Id));
        ModelState.Remove(nameof(BusFormViewModel.BusCode));
        ModelState.Remove(nameof(BusFormViewModel.BranchId));
    }

    private string? GetFirstModelStateError()
    {
        return ModelState.Values
            .SelectMany(value => value.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(error => !string.IsNullOrWhiteSpace(error));
    }
}
