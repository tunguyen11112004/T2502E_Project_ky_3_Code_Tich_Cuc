using System.Text.RegularExpressions;
using Bus_ticket.Data;
using Bus_ticket.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Bus_ticket.Services;

public record BranchPagedResult(List<Branch> Items, long TotalItems, int TotalPages, int CurrentPage, int PageSize);

public record DeleteBranchResult(bool Succeeded, string Message, bool IsInUse = false);

public class BranchService
{
    private static readonly string[] ValidStatuses = { "Active", "Inactive", "Maintenance" };

    private readonly ApplicationDbContext _dbContext;

    public BranchService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BranchPagedResult> GetPagedAsync(string? searchTerm, string? status, int page = 1, int pageSize = 10)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 10 : pageSize;

        var filter = BuildFilter(searchTerm, status);
        var totalItems = await _dbContext.Branches.CountDocumentsAsync(filter);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        if (totalPages > 0 && page > totalPages)
        {
            page = totalPages;
        }

        var items = await _dbContext.Branches
            .Find(filter)
            .SortByDescending(branch => branch.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return new BranchPagedResult(items, totalItems, totalPages, page, pageSize);
    }

    public async Task<List<Branch>> GetActiveBranchesAsync()
    {
        return await _dbContext.Branches
            .Find(branch => branch.Status == "Active")
            .SortBy(branch => branch.BranchName)
            .ToListAsync();
    }

    public async Task<Branch?> GetByIdAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
        {
            return null;
        }

        return await _dbContext.Branches
            .Find(branch => branch.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<Branch> CreateAsync(Branch branch, string actor)
    {
        NormalizeBranch(branch);
        ValidateBranch(branch);

        if (await BranchCodeExistsAsync(branch.BranchCode))
        {
            throw new InvalidOperationException("Mã chi nhánh đã tồn tại trong hệ thống.");
        }

        branch.Id = ObjectId.GenerateNewId().ToString();
        branch.CreatedAt = DateTime.UtcNow;
        branch.CreatedBy = actor;
        branch.UpdatedAt = DateTime.UtcNow;
        branch.UpdatedBy = actor;

        await _dbContext.Branches.InsertOneAsync(branch);

        return branch;
    }

    public async Task<Branch?> UpdateAsync(string id, Branch branch, string actor)
    {
        if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
        {
            return null;
        }

        var existingBranch = await GetByIdAsync(id);
        if (existingBranch == null)
        {
            return null;
        }

        NormalizeBranch(branch);
        ValidateBranch(branch);

        if (await BranchCodeExistsAsync(branch.BranchCode, id))
        {
            throw new InvalidOperationException("Mã chi nhánh đã tồn tại ở một chi nhánh khác.");
        }

        var update = Builders<Branch>.Update
            .Set(item => item.BranchCode, branch.BranchCode)
            .Set(item => item.BranchName, branch.BranchName)
            .Set(item => item.Address, branch.Address)
            .Set(item => item.PhoneNumber, branch.PhoneNumber)
            .Set(item => item.Status, branch.Status)
            .Set(item => item.UpdatedAt, DateTime.UtcNow)
            .Set(item => item.UpdatedBy, actor);

        await _dbContext.Branches.UpdateOneAsync(item => item.Id == id, update);

        return await GetByIdAsync(id);
    }

    public async Task<DeleteBranchResult> DeleteAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
        {
            return new DeleteBranchResult(false, "Mã chi nhánh không hợp lệ.");
        }

        var branch = await GetByIdAsync(id);
        if (branch == null)
        {
            return new DeleteBranchResult(false, "Không tìm thấy chi nhánh cần xóa.");
        }

        var isInUse = await IsBranchInUseAsync(id);
        if (isInUse)
        {
            return new DeleteBranchResult(
                false,
                "Không thể xóa chi nhánh này vì đang được liên kết với xe, nhân viên hoặc đơn đặt vé. Hãy chuyển trạng thái sang Inactive nếu muốn ngừng sử dụng.",
                true);
        }

        await _dbContext.Branches.DeleteOneAsync(item => item.Id == id);

        return new DeleteBranchResult(true, "Xóa chi nhánh thành công.");
    }

    public async Task<Branch?> ChangeStatusAsync(string id, string status, string actor)
    {
        if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
        {
            return null;
        }

        var normalizedStatus = NormalizeStatus(status);
        if (!ValidStatuses.Contains(normalizedStatus))
        {
            throw new InvalidOperationException("Trạng thái chi nhánh không hợp lệ.");
        }

        var update = Builders<Branch>.Update
            .Set(branch => branch.Status, normalizedStatus)
            .Set(branch => branch.UpdatedAt, DateTime.UtcNow)
            .Set(branch => branch.UpdatedBy, actor);

        var result = await _dbContext.Branches.UpdateOneAsync(branch => branch.Id == id, update);
        if (result.MatchedCount == 0)
        {
            return null;
        }

        return await GetByIdAsync(id);
    }

    private async Task<bool> BranchCodeExistsAsync(string branchCode, string? excludedId = null)
    {
        var normalizedCode = branchCode.Trim().ToUpperInvariant();
        var codeRegex = new BsonRegularExpression($"^{Regex.Escape(normalizedCode)}$", "i");

        var filter = Builders<Branch>.Filter.Regex(branch => branch.BranchCode, codeRegex);
        if (!string.IsNullOrWhiteSpace(excludedId))
        {
            filter &= Builders<Branch>.Filter.Ne(branch => branch.Id, excludedId);
        }

        return await _dbContext.Branches.Find(filter).AnyAsync();
    }

    private async Task<bool> IsBranchInUseAsync(string branchId)
    {
        var isUsedByBus = await _dbContext.Buses.Find(bus => bus.BranchId == branchId).AnyAsync();
        if (isUsedByBus) return true;

        var isUsedByUser = await _dbContext.Users.Find(user => user.BranchId == branchId).AnyAsync();
        if (isUsedByUser) return true;

        var isUsedByBooking = await _dbContext.Bookings.Find(booking => booking.BranchId == branchId).AnyAsync();
        return isUsedByBooking;
    }

    private static FilterDefinition<Branch> BuildFilter(string? searchTerm, string? status)
    {
        var filterBuilder = Builders<Branch>.Filter;
        var filter = filterBuilder.Empty;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var regex = new BsonRegularExpression(Regex.Escape(searchTerm.Trim()), "i");
            filter &= filterBuilder.Or(
                filterBuilder.Regex(branch => branch.BranchCode, regex),
                filterBuilder.Regex(branch => branch.BranchName, regex),
                filterBuilder.Regex(branch => branch.Address, regex),
                filterBuilder.Regex(branch => branch.PhoneNumber, regex));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = NormalizeStatus(status);
            filter &= filterBuilder.Eq(branch => branch.Status, normalizedStatus);
        }

        return filter;
    }

    private static void NormalizeBranch(Branch branch)
    {
        branch.BranchCode = branch.BranchCode.Trim().ToUpperInvariant();
        branch.BranchName = branch.BranchName.Trim();
        branch.Address = branch.Address.Trim();
        branch.PhoneNumber = branch.PhoneNumber.Trim();
        branch.Status = NormalizeStatus(branch.Status);
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Active";
        }

        var value = status.Trim();
        var matchedStatus = ValidStatuses.FirstOrDefault(item => item.Equals(value, StringComparison.OrdinalIgnoreCase));

        return matchedStatus ?? value;
    }

    private static void ValidateBranch(Branch branch)
    {
        if (string.IsNullOrWhiteSpace(branch.BranchCode))
        {
            throw new InvalidOperationException("Mã chi nhánh là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(branch.BranchName))
        {
            throw new InvalidOperationException("Tên chi nhánh là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(branch.Address))
        {
            throw new InvalidOperationException("Địa chỉ chi nhánh là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(branch.PhoneNumber))
        {
            throw new InvalidOperationException("Số điện thoại chi nhánh là bắt buộc.");
        }

        if (!ValidStatuses.Contains(branch.Status))
        {
            throw new InvalidOperationException("Trạng thái chi nhánh không hợp lệ.");
        }
    }
}
