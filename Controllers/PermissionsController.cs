using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Bus_ticket.Data;
using Bus_ticket.Models;
using Microsoft.AspNetCore.Authorization;

namespace Bus_ticket.Controllers
{   [Authorize(Roles = "Admin,Employee")]
    public class PermissionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PermissionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. TRANG DANH SÁCH QUYỀN
        // GET: /Permissions hoặc /Permissions/Index
        public async Task<IActionResult> Index(string searchTerm, int page = 1, int pageSize = 10)
        {
            // 1. Khởi tạo bộ lọc tìm kiếm mặc định (lấy tất cả)
            var filterBuilder = Builders<Permission>.Filter;
            var filter = filterBuilder.Empty;

            // Nếu người dùng nhập từ khóa tìm kiếm
            if (!string.IsNullOrEmpty(searchTerm))
            {
                // Tìm kiếm không phân biệt hoa thường theo Tên quyền hoặc Mô tả
                filter = filterBuilder.Regex(p => p.Name, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i")) |
                         filterBuilder.Regex(p => p.Description, new MongoDB.Bson.BsonRegularExpression(searchTerm, "i"));
            }

            // 2. Đếm tổng số bản ghi thỏa mãn điều kiện lọc để tính tổng số trang
            long totalItems = await _context.Permissions.CountDocumentsAsync(filter);
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            // Đảm bảo số trang hợp lệ
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            // 3. Truy vấn dữ liệu có phân trang (Skip và Limit)
            var permissions = await _context.Permissions.Find(filter)
                .Skip((page - 1) * pageSize)
                .Limit(pageSize)
                .ToListAsync();

            // 4. Đẩy các thông số phân trang ra View qua ViewBag để dựng thanh chuyển trang
            ViewBag.SearchTerm = searchTerm;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            return View(permissions);
        }

        // 2. TẠO MỚI QUYỀN (GIAO DIỆN)
        // GET: /Permissions/Create
        public IActionResult Create()
        {
            return View();
        }

        // 3. XỬ LÝ LƯU QUYỀN MỚI XUỐNG DATABASE
        // POST: /Permissions/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Permission permission)
        {
            // BỎ QUA KIỂM TRA LỖI VALIDATION CỦA TRƯỜNG ID (Vì MongoDB tự sinh ObjectId)
            ModelState.Remove("Id");

            if (ModelState.IsValid)
            {
                // Chuẩn hóa dữ liệu đầu vào: Viết hoa Method, xóa dấu gạch chéo thừa đầu Link
                permission.Method = permission.Method.ToUpper();
                permission.Link = permission.Link.TrimStart('/');

                // Kiểm tra xem trùng lặp (Tên Quyền + Phương Thức) trong hệ thống chưa
                var isExist = await _context.Permissions.Find(p => 
                    p.Name.ToLower() == permission.Name.ToLower() && 
                    p.Method == permission.Method
                ).AnyAsync();

                if (isExist)
                {
                    ModelState.AddModelError("", "Quyền này với phương thức tương ứng đã tồn tại trong hệ thống.");
                    return View(permission);
                }
                TempData["SuccessMessage"] = "Thêm quyền mới thành công.";
                // Lưu trực tiếp vào MongoDB
                await _context.Permissions.InsertOneAsync(permission);
                return RedirectToAction(nameof(Index));
            }

            // Nếu dữ liệu không hợp lệ, trả về View kèm theo các thông báo lỗi cụ thể
            return View(permission);
        }

        // 4. CHỈNH SỬA QUYỀN (GIAO DIỆN ĐỔ DỮ LIỆU CŨ)
        // GET: /Permissions/Edit/{id}
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var permission = await _context.Permissions.Find(p => p.Id == id).FirstOrDefaultAsync();
            if (permission == null) return NotFound();

            return View(permission);
        }

        // 5. XỬ LÝ CẬP NHẬT QUYỀN XUỐNG DATABASE
        // POST: /Permissions/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Permission permission)
        {
            if (id != permission.Id) return NotFound();

            if (ModelState.IsValid)
            {
                permission.Method = permission.Method.ToUpper();
                permission.Link = permission.Link.TrimStart('/');

                // Kiểm tra trùng lặp với bản ghi khác khi đổi tên
                var isDuplicate = await _context.Permissions.Find(p => 
                    p.Id != id && 
                    p.Name.ToLower() == permission.Name.ToLower() && 
                    p.Method == permission.Method
                ).AnyAsync();

                if (isDuplicate)
                {
                    ModelState.AddModelError("", "Tên quyền hoặc phương thức bị trùng với một bản ghi khác.");
                    return View(permission);
                }

                // Định nghĩa tập lệnh cập nhật dữ liệu MongoDB
                var update = Builders<Permission>.Update
                    .Set(p => p.Name, permission.Name)
                    .Set(p => p.Description, permission.Description)
                    .Set(p => p.Link, permission.Link)
                    .Set(p => p.Method, permission.Method);
                TempData["SuccessMessage"] = "Sửa quyền thành công.";
                await _context.Permissions.UpdateOneAsync(p => p.Id == id, update);
                return RedirectToAction(nameof(Index));
            }
            return View(permission);
        }

        // 6. XỬ LÝ XÓA QUYỀN
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            // Kiểm tra ràng buộc: Quyền này đang được gán cho DynamicRole nào không
            var isUsedInRole = await _context.DynamicRoles.Find(r => r.PermissionIds.Contains(id)).AnyAsync();
            if (isUsedInRole)
            {
                TempData["ErrorMessage"] = "Không thể xóa! Quyền này hiện đang được gán cho một Vai trò trong hệ thống.";
                return RedirectToAction(nameof(Index));
            }
            TempData["SuccessMessage"] = "Xóa quyền thành công.";
            await _context.Permissions.DeleteOneAsync(p => p.Id == id);
            return RedirectToAction(nameof(Index));
        }
    }
}