using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Bus_ticket.Data;
using Bus_ticket.Models;
using MongoDB.Bson;
using Microsoft.AspNetCore.Authorization;

namespace Bus_ticket.Controllers
{  [Authorize(Roles = "Admin,Employee")]
    public class DynamicRolesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DynamicRolesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. DANH SÁCH VAI TRÒ
        // GET: /DynamicRoles hoặc /DynamicRoles/Index
        public async Task<IActionResult> Index(string searchTerm, int page = 1, int pageSize = 10)
        {
            var filterBuilder = Builders<DynamicRole>.Filter;
            var filter = filterBuilder.Empty;

            // ĐÃ SỬA: Đổi r.Name thành r.RoleName theo đúng Model. Loại bỏ Description vì Model không có.
            if (!string.IsNullOrEmpty(searchTerm))
            {
                filter = filterBuilder.Regex(r => r.RoleName, new BsonRegularExpression(searchTerm, "i"));
            }

            // Tính toán phân trang
            long totalItems = await _context.DynamicRoles.CountDocumentsAsync(filter);
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var roles = await _context.DynamicRoles.Find(filter)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            ViewBag.SearchTerm = searchTerm;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            return View(roles);
        }
        
        // 2. GIAO DIỆN TẠO MỚI
        public async Task<IActionResult> Create()
        {
            // Đã cập nhật nạp đủ 37 quyền
            var allPermissions = await _context.Permissions.Find(_ => true).SortBy(p => p.Name).ToListAsync();
            ViewBag.AllPermissions = allPermissions;
            
            return View(new DynamicRole());
        }

        // 3. XỬ LÝ LƯU VAI TRÒ MỚI
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DynamicRole role, List<string> selectedPermissions)
        {
            ModelState.Remove("Id");
            ModelState.Remove("CreatedBy");
            ModelState.Remove("UpdatedBy");
            if (ModelState.IsValid)
            {
                role.PermissionIds = selectedPermissions ?? new List<string>();

                // ĐÃ SỬA: Kiểm tra trùng tên theo r.RoleName và gán các trường audit lúc tạo mới
                var isExist = await _context.DynamicRoles.Find(r => r.RoleName.ToLower() == role.RoleName.ToLower()).AnyAsync();
                if (isExist)
                {
                    ModelState.AddModelError("RoleName", "Tên vai trò này đã tồn tại trong hệ thống.");
                    ViewBag.AllPermissions = await _context.Permissions.Find(_ => true).SortBy(p => p.Name).ToListAsync();
                    return View(role);
                }

                // THÊM: Gán thông tin audit khi tạo mới
                role.CreatedAt = DateTime.UtcNow;
                role.CreatedBy = User.Identity?.Name ?? "Admin"; // Lấy tên người dùng đăng nhập 
                role.UpdatedAt = DateTime.UtcNow;
                role.UpdatedBy = User.Identity?.Name ?? "Admin";

                await _context.DynamicRoles.InsertOneAsync(role);
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] = "Tạo vai trò mới thành công.";
            ViewBag.AllPermissions = await _context.Permissions.Find(_ => true).SortBy(p => p.Name).ToListAsync();
            return View(role);
        }

        // 4. GIAO DIỆN CHỈNH SỬA VAI TRÒ
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var role = await _context.DynamicRoles.Find(r => r.Id == id).FirstOrDefaultAsync();
            if (role == null) return NotFound();

            ViewBag.AllPermissions = await _context.Permissions.Find(_ => true).SortBy(p => p.Name).ToListAsync();
            return View(role);
        }

        // 5. XỬ LÝ CẬP NHẬT VAI TRÒ XUỐNG DATABASE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, DynamicRole role, List<string> selectedPermissions)
        {
            if (id != role.Id) return NotFound();

            if (ModelState.IsValid)
            {
                // ĐÃ SỬA: Cập nhật các trường audit UpdateAt, UpdateBy và đổi Name thành RoleName
                var update = Builders<DynamicRole>.Update
                    .Set(r => r.RoleName, role.RoleName)
                    .Set(r => r.PermissionIds, selectedPermissions ?? new List<string>())
                    .Set(r => r.UpdatedAt, DateTime.UtcNow)
                    .Set(r => r.UpdatedBy, User.Identity?.Name ?? "Admin");

                await _context.DynamicRoles.UpdateOneAsync(r => r.Id == id, update);
                return RedirectToAction(nameof(Index));
            }
            TempData["SuccessMessage"] = "Sửa vai trò thành công.";
            ViewBag.AllPermissions = await _context.Permissions.Find(_ => true).SortBy(p => p.Name).ToListAsync();
            return View(role);
        }

        // 6. XỬ LÝ XÓA VAI TRÒ
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            // Kiểm tra ràng buộc thực thể User (RoleId) trước khi cho phép xóa
            var isUsed = await _context.Users.Find(u => u.RoleId == id).AnyAsync();
            if (isUsed)
            {
                TempData["ErrorMessage"] = "Không thể xóa! Vai trò này hiện đang được gán cho nhân viên.";
                return RedirectToAction(nameof(Index));
            }

            await _context.DynamicRoles.DeleteOneAsync(r => r.Id == id);
            return RedirectToAction(nameof(Index));
        }
    }
}