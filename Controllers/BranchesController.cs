using System.Security.Claims;
using Bus_ticket.Models;
using Bus_ticket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bus_ticket.Controllers;

[Authorize(Roles = "Admin,Employee")]
public class BranchesController : Controller
{
    private readonly BranchService _branchService;

    public BranchesController(BranchService branchService)
    {
        _branchService = branchService;
    }

    // GET: /Branches hoặc /Branches/Index
    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm, string? status, int page = 1, int pageSize = 10)
    {
        var result = await _branchService.GetPagedAsync(searchTerm, status, page, pageSize);

        ViewBag.SearchTerm = searchTerm ?? string.Empty;
        ViewBag.Status = status ?? string.Empty;
        ViewBag.CurrentPage = result.CurrentPage;
        ViewBag.PageSize = result.PageSize;
        ViewBag.TotalPages = result.TotalPages;
        ViewBag.TotalItems = result.TotalItems;

        return View(result.Items);
    }

    // GET: /Branches/Details/{id}
    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var branch = await _branchService.GetByIdAsync(id);
        if (branch == null)
        {
            return NotFound();
        }

        return View(branch);
    }

    // GET: /Branches/Create
    [HttpGet]
    public IActionResult Create()
    {
        return View(new Branch { Status = "Active" });
    }

    // POST: /Branches/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Branch branch)
    {
        RemoveSystemModelStateKeys();

        if (!ModelState.IsValid)
        {
            return View(branch);
        }

        try
        {
            await _branchService.CreateAsync(branch, GetCurrentActor());
            TempData["SuccessMessage"] = "Thêm chi nhánh mới thành công.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(branch);
        }
    }

    // GET: /Branches/Edit/{id}
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var branch = await _branchService.GetByIdAsync(id);
        if (branch == null)
        {
            return NotFound();
        }

        return View(branch);
    }

    // POST: /Branches/Edit/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, Branch branch)
    {
        if (id != branch.Id)
        {
            return NotFound();
        }

        RemoveSystemModelStateKeys();

        if (!ModelState.IsValid)
        {
            return View(branch);
        }

        try
        {
            var updatedBranch = await _branchService.UpdateAsync(id, branch, GetCurrentActor());
            if (updatedBranch == null)
            {
                return NotFound();
            }

            TempData["SuccessMessage"] = "Cập nhật chi nhánh thành công.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(branch);
        }
    }

    // POST: /Branches/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _branchService.DeleteAsync(id);

        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = result.Message;
        }
        else
        {
            TempData["ErrorMessage"] = result.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: /Branches/ChangeStatus/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(string id, string status)
    {
        try
        {
            var updatedBranch = await _branchService.ChangeStatusAsync(id, status, GetCurrentActor());
            if (updatedBranch == null)
            {
                return NotFound();
            }

            TempData["SuccessMessage"] = "Cập nhật trạng thái chi nhánh thành công.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    // GET: /Branches/GetActiveBranches
    // Endpoint JSON dùng cho dropdown chọn chi nhánh ở module Bus, User, Booking.
    [HttpGet]
    public async Task<IActionResult> GetActiveBranches()
    {
        var branches = await _branchService.GetActiveBranchesAsync();

        return Json(branches.Select(branch => new
        {
            id = branch.Id,
            branchCode = branch.BranchCode,
            branchName = branch.BranchName,
            displayName = $"{branch.BranchCode} - {branch.BranchName}"
        }));
    }

    private string GetCurrentActor()
    {
        return User.FindFirstValue(ClaimTypes.Email)
               ?? User.Identity?.Name
               ?? "System Admin";
    }

    private void RemoveSystemModelStateKeys()
    {
        ModelState.Remove(nameof(Branch.Id));
        ModelState.Remove(nameof(Branch.CreatedAt));
        ModelState.Remove(nameof(Branch.CreatedBy));
        ModelState.Remove(nameof(Branch.UpdatedAt));
        ModelState.Remove(nameof(Branch.UpdatedBy));
    }
}
