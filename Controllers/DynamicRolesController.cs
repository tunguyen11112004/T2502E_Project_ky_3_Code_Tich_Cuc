using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Bus_ticket.Data;
using Bus_ticket.Models;

namespace Bus_ticket.Controllers
{
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
            // 1. Bộ lọc tìm kiếm theo Tên vai trò hoặc Mô tả
            var filterBuilder = Builders<DynamicRole>.Filter;
            var filter = filterBuilder.Empty;

            if (!string.IsNullOrEmpty(searchTerm))
            {
                filter = filterBuilder.Regex(r => r.Name, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i")) |
                         filterBuilder.Regex(r => r.Description, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i"));
            }

            // 2. Tính toán phân trang
            long totalItems = await _context.DynamicRoles.CountDocumentsAsync(filter);
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            // 3. Truy vấn dữ liệu Skip & Limit
            var roles = await _context.DynamicRoles.Find(filter)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            // 4. Đẩy thông số ra View
            ViewBag.SearchTerm = searchTerm;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            return View(roles);
        }
        
        // 2. GIAO DIỆN TẠO MỚI (Đổ danh sách quyền ra ma trận)
        public async Task<IActionResult> Create()
        {
            // Lấy 33 quyền xếp theo tên để giao diện tự động gom cụm nhóm Checkbox
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

            if (ModelState.IsValid)
            {
                role.PermissionIds = selectedPermissions ?? new List<string>();

                var isExist = await _context.DynamicRoles.Find(r => r.Name.ToLower() == role.Name.ToLower()).AnyAsync();
                if (isExist)
                {
                    ModelState.AddModelError("Name", "Tên vai trò này đã tồn tại trong hệ thống.");
                    ViewBag.AllPermissions = await _context.Permissions.Find(_ => true).SortBy(p => p.Name).ToListAsync();
                    return View(role);
                }

                await _context.DynamicRoles.InsertOneAsync(role);
                return RedirectToAction(nameof(Index));
            }

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
                var update = Builders<DynamicRole>.Update
                    .Set(r => r.Name, role.Name)
                    .Set(r => r.Description, role.Description)
                    .Set(r => r.PermissionIds, selectedPermissions ?? new List<string>());

                await _context.DynamicRoles.UpdateOneAsync(r => r.Id == id, update);
                return RedirectToAction(nameof(Index));
            }

            ViewBag.AllPermissions = await _context.Permissions.Find(_ => true).SortBy(p => p.Name).ToListAsync();
            return View(role);
        }

        
        // 6. XỬ LÝ XÓA VAI TRÒ
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            // Kiểm tra ràng buộc thực thể User (RoleId)
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