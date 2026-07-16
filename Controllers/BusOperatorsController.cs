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
        NormalizeModelForValidation(model);
        RevalidateModel(model);

        if (!ModelState.IsValid)
        {
            TempData["OpenCreateModal"] = true;
            TempData["ErrorMessage"] = GetFirstValidationError() ?? "Vui lòng kiểm tra lại thông tin nhà xe.";
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
        NormalizeModelForValidation(model);
        RevalidateModel(model);

        if (!ModelState.IsValid)
        {
            TempData["OpenEditModalId"] = id;
            TempData["ErrorMessage"] = GetFirstValidationError() ?? "Vui lòng kiểm tra lại thông tin nhà xe.";
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
            searchTerm = GetReturnQueryValue("returnSearchTerm", "searchTerm"),
            status = GetReturnQueryValue("returnStatus", "status"),
            page = GetReturnQueryValue("returnPage", "page")
        });
    }

    private string GetReturnQueryValue(string formKey, string queryKey)
    {
        var formValue = Request.Form[formKey].ToString();
        if (!string.IsNullOrEmpty(formValue))
        {
            return formValue;
        }

        return Request.Query[queryKey].ToString();
    }

    private static void NormalizeModelForValidation(BusOperator model)
    {
        model.OperatorCode = model.OperatorCode.Trim().ToUpperInvariant();
        model.OperatorName = model.OperatorName.Trim();
        model.PhoneNumber = NormalizeHotline(model.PhoneNumber);
        model.Email = model.Email.Trim();
        model.Address = model.Address.Trim();
        model.ContactPerson = model.ContactPerson.Trim();
        model.Status = string.IsNullOrWhiteSpace(model.Status) ? "Active" : model.Status.Trim();
    }

    private string? GetFirstValidationError()
    {
        return ModelState.Values
            .SelectMany(entry => entry.Errors)
            .Select(error => error.ErrorMessage)
            .FirstOrDefault(message => !string.IsNullOrWhiteSpace(message));
    }

    private static string NormalizeHotline(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return string.Empty;
        }

        return System.Text.RegularExpressions.Regex.Replace(phoneNumber.Trim(), @"[\s\-\.\(\)]", string.Empty);
    }

    private void RevalidateModel(BusOperator model)
    {
        ModelState.Remove(nameof(BusOperator.OperatorCode));
        ModelState.Remove(nameof(BusOperator.OperatorName));
        ModelState.Remove(nameof(BusOperator.PhoneNumber));
        ModelState.Remove(nameof(BusOperator.Email));
        ModelState.Remove(nameof(BusOperator.Address));
        ModelState.Remove(nameof(BusOperator.ContactPerson));
        ModelState.Remove(nameof(BusOperator.Status));
        TryValidateModel(model);
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
