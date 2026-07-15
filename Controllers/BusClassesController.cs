using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
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
    private const string ActiveFormTokenSessionKey = "BusClass_ActiveFormToken";
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ClassNameCreateLocks = new();

    private readonly ApplicationDbContext _context;
    private readonly ICloudinaryService _cloudinaryService;

    public BusClassesController(ApplicationDbContext context, ICloudinaryService cloudinaryService)
    {
        _context = context;
        _cloudinaryService = cloudinaryService;
    }

    public async Task<IActionResult> Index(string? searchTerm, int page = 1, int pageSize = 10)
    {
        var filter = BusClassFilters.NotDeleted;
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var keyword = searchTerm.Trim();
            filter &= Builders<BusClass>.Filter.Regex(
                bc => bc.ClassName,
                new BsonRegularExpression(keyword, "i"));
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

        var items = classes.Select(c => new BusClassListItemViewModel
        {
            Id = c.Id,
            ClassName = c.ClassName,
            ImageUrl = c.ImageUrl,
            TotalRows = c.TotalRows,
            TotalColumns = c.TotalColumns,
            TotalFloors = c.TotalFloors,
            TotalSeats = c.TotalSeats,
            Status = NormalizeClassStatus(c.Status)
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
        var token = FormSubmissionGuard.CreateToken();
        HttpContext.Session.SetString(ActiveFormTokenSessionKey, token);
        ViewBag.FormToken = token;
        return View(new BusClassFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BusClassFormViewModel model, string? formToken)
    {
        var sessionToken = HttpContext.Session.GetString(ActiveFormTokenSessionKey);
        if (string.IsNullOrWhiteSpace(sessionToken) ||
            !string.Equals(sessionToken, formToken?.Trim(), StringComparison.Ordinal))
        {
            TempData["ErrorMessage"] =
                "Form không còn hợp lệ (có thể do bạn mở nhiều tab Thêm mới). Vui lòng bấm Thêm loại xe một lần.";
            return RedirectToAction(nameof(Index));
        }

        if (!FormSubmissionGuard.TryAcquire(formToken))
        {
            TempData[FormSubmissionGuard.IsCompleted(formToken) ? "SuccessMessage" : "ErrorMessage"] =
                FormSubmissionGuard.IsCompleted(formToken)
                    ? "Cấu hình loại xe đã được lưu trước đó."
                    : "Yêu cầu đang được xử lý. Mỗi form chỉ lưu một lần.";
            return RedirectToAction(nameof(Index));
        }

        ModelState.Remove(nameof(model.Id));
        ModelState.Remove(nameof(model.ImageUrl));
        ModelState.Remove(nameof(model.ImagePublicId));

        ValidateImage(model, isEdit: false);

        if (!ModelState.IsValid)
        {
            return CreateViewWithToken(model, formToken);
        }

        if (await IsDuplicateClassNameAsync(model.ClassName))
        {
            ModelState.AddModelError(nameof(model.ClassName), "Tên loại xe này đã tồn tại.");
            return CreateViewWithToken(model, formToken);
        }

        var classNameLock = ClassNameCreateLocks.GetOrAdd(
            BusClassNameHelper.NormalizeKey(model.ClassName),
            _ => new SemaphoreSlim(1, 1));

        await classNameLock.WaitAsync();
        try
        {
            if (await IsDuplicateClassNameAsync(model.ClassName))
            {
                ModelState.AddModelError(nameof(model.ClassName), "Tên loại xe này đã tồn tại.");
                return CreateViewWithToken(model, formToken);
            }

            var busType = ResolveBusType(model.TotalFloors, model.ClassName);
            var layout = BusSeatLayoutGenerator.Generate(
                model.TotalRows, model.TotalColumns, model.TotalFloors, busType);

            string imageUrl = string.Empty;
            string imagePublicId = string.Empty;
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                await using var stream = model.ImageFile.OpenReadStream();
                (imageUrl, imagePublicId) = await _cloudinaryService.UploadImageAsync(stream, model.ImageFile.FileName);
            }

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                ModelState.AddModelError(nameof(model.ImageFile), "Ảnh loại xe không được để trống.");
                return CreateViewWithToken(model, formToken);
            }

            var busClass = new BusClass
            {
                Id = ObjectId.GenerateNewId().ToString(),
                ClassName = model.ClassName.Trim(),
                ClassNameKey = BusClassNameHelper.NormalizeKey(model.ClassName),
                BusType = busType,
                ImageUrl = imageUrl,
                ImagePublicId = imagePublicId,
                TotalRows = model.TotalRows,
                TotalColumns = model.TotalColumns,
                TotalFloors = model.TotalFloors,
                DefaultLayout = layout,
                TotalSeats = layout.Count,
                Status = NormalizeClassStatus(model.Status),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "Admin",
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = User.Identity?.Name ?? "Admin"
            };

            await _context.BusClasses.InsertOneAsync(busClass);

            HttpContext.Session.Remove(ActiveFormTokenSessionKey);
            FormSubmissionGuard.MarkCompleted(formToken);
            TempData["SuccessMessage"] = "Thêm cấu hình loại xe thành công.";
            return RedirectToAction(nameof(Index));
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            ModelState.AddModelError(nameof(model.ClassName), "Tên loại xe này đã tồn tại.");
            return CreateViewWithToken(model, formToken);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, "Lỗi khi lưu: " + ex.Message);
            return CreateViewWithToken(model, formToken);
        }
        finally
        {
            classNameLock.Release();
        }
    }

    private IActionResult CreateViewWithToken(BusClassFormViewModel model, string? formToken)
    {
        ViewBag.FormToken = formToken;
        FormSubmissionGuard.Release(formToken);
        return View(model);
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var busClass = await _context.BusClasses.Find(bc => bc.Id == id).FirstOrDefaultAsync();
        if (busClass == null || busClass.DeletedAt.HasValue) return NotFound();

        return View(MapToFormViewModel(busClass));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, BusClassFormViewModel model)
    {
        if (id != model.Id) return NotFound();

        ModelState.Remove(nameof(model.ImageUrl));
        ModelState.Remove(nameof(model.ImagePublicId));

        var existing = await _context.BusClasses.Find(bc => bc.Id == id).FirstOrDefaultAsync();
        if (existing == null || existing.DeletedAt.HasValue) return NotFound();

        ValidateImage(model, isEdit: true, existing.ImageUrl);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (await IsDuplicateClassNameAsync(model.ClassName, id))
        {
            ModelState.AddModelError(nameof(model.ClassName), "Tên loại xe này đã tồn tại.");
            return View(model);
        }

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

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                ModelState.AddModelError(nameof(model.ImageFile), "Ảnh loại xe không được để trống.");
                return View(model);
            }

            var busType = ResolveBusType(model.TotalFloors, model.ClassName);
            var layout = BusSeatLayoutGenerator.Generate(
                model.TotalRows, model.TotalColumns, model.TotalFloors, busType);
            var normalizedStatus = NormalizeClassStatus(model.Status);

            var update = Builders<BusClass>.Update
                .Set(bc => bc.ClassName, model.ClassName.Trim())
                .Set(bc => bc.ClassNameKey, BusClassNameHelper.NormalizeKey(model.ClassName))
                .Set(bc => bc.BusType, busType)
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

            TempData["SuccessMessage"] = "Cập nhật cấu hình loại xe thành công.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, "Lỗi khi cập nhật: " + ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var busClass = await _context.BusClasses.Find(bc => bc.Id == id).FirstOrDefaultAsync();
        if (busClass == null) return NotFound();

        // 1. Kiểm tra trạng thái xóa mềm
        if (busClass.DeletedAt.HasValue || busClass.Status == "Inactive")
        {
            TempData["ErrorMessage"] = "Loại xe này đã được xóa trước đó.";
            return RedirectToAction(nameof(Index));
        }

        // 2. Kiểm tra ràng buộc dữ liệu chuyến đi (Trips)
        var inUse = await _context.Trips
            .Find(Builders<Trip>.Filter.And(
                TripFilters.NotDeleted,
                Builders<Trip>.Filter.Eq(t => t.BusId, id)))
            .AnyAsync();

        if (inUse)
        {
            TempData["ErrorMessage"] = "Không thể xóa xe! Xe đang được gán cho chuyến đi.";
            return RedirectToAction(nameof(Edit), new { id = id });
        }

        // 3. Tiến hành xóa mềm bằng cách cập nhật các trường tương ứng trong Model mới
        var currentUserName = User.Identity?.Name ?? "SystemAdmin"; // Lấy tên user đang đăng nhập (nếu có)

        var update = Builders<BusClass>.Update
            .Set(bc => bc.DeletedAt, DateTime.UtcNow)
            .Set(bc => bc.DeletedBy, currentUserName) // Lưu vết người xóa
            .Set(bc => bc.Status, "Inactive")
            .Set(bc => bc.UpdatedAt, DateTime.UtcNow)
            .Set(bc => bc.UpdatedBy, currentUserName);

        await _context.BusClasses.UpdateOneAsync(bc => bc.Id == id, update);

        TempData["SuccessMessage"] = "Đã xóa loại xe thành công (Xóa mềm).";
        return RedirectToAction(nameof(Index));
    }

    private void ValidateImage(BusClassFormViewModel model, bool isEdit, string? existingImageUrl = null)
    {
        var hasNewImage = model.ImageFile != null && model.ImageFile.Length > 0;
        var hasExistingImage = !string.IsNullOrWhiteSpace(existingImageUrl);

        // 1. Kiểm tra nếu tạo mới (hoặc edit nhưng không có ảnh cũ) mà lại không upload ảnh mới
        if (!hasNewImage && (!isEdit || !hasExistingImage))
        {
            ModelState.AddModelError(nameof(model.ImageFile), "Vui lòng tải lên một hình ảnh cho loại xe.");
            return;
        }

        // 2. Nếu không upload ảnh mới (nhưng đã có ảnh cũ - trường hợp Edit hợp lệ), không cần check định dạng nữa
        if (!hasNewImage)
        {
            return;
        }

        // 3. Kiểm tra định dạng file ảnh tải lên
        if (!model.ImageFile!.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.ImageFile), "File tải lên phải là định dạng ảnh (image/*).");
        }
    }

    private static string NormalizeClassStatus(string? status) =>
        string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase) ? "Inactive" : "Active";

    private async Task<bool> IsDuplicateClassNameAsync(string className, string? excludeId = null)
    {
        var key = BusClassNameHelper.NormalizeKey(className);
        var filter = Builders<BusClass>.Filter.And(
            BusClassFilters.NotDeleted,
            Builders<BusClass>.Filter.Eq(bc => bc.ClassNameKey, key));

        if (!string.IsNullOrWhiteSpace(excludeId))
        {
            filter = Builders<BusClass>.Filter.And(
                filter,
                Builders<BusClass>.Filter.Ne(bc => bc.Id, excludeId));
        }

        return await _context.BusClasses.Find(filter).AnyAsync();
    }

    private static string ResolveBusType(int totalFloors, string className)
    {
        var name = className.Trim().ToLowerInvariant();

        if (name.Contains("limousine"))
        {
            return "Limousine_Sleeper";
        }

        if (totalFloors >= 2
            || name.Contains("giường")
            || name.Contains("sleeper")
            || name.Contains("luxury"))
        {
            return "Luxury_Sleeper";
        }

        return "Express_Seat";
    }

    private static BusClassFormViewModel MapToFormViewModel(BusClass busClass) =>
        new()
        {
            Id = busClass.Id,
            ClassName = busClass.ClassName,
            TotalRows = busClass.TotalRows,
            TotalColumns = busClass.TotalColumns,
            TotalFloors = busClass.TotalFloors,
            Status = NormalizeClassStatus(busClass.Status),
            ImageUrl = busClass.ImageUrl,
            ImagePublicId = busClass.ImagePublicId
        };
}