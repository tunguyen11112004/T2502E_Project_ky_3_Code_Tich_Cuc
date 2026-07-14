using System.Text.RegularExpressions;
using Bus_ticket.Data;
using Bus_ticket.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Bus_ticket.Services;

public record BusOperatorPagedResult(
    List<BusOperator> Items,
    long TotalItems,
    int TotalPages,
    int CurrentPage,
    int PageSize);

public record SoftDeleteBusOperatorResult(bool Succeeded, string Message);

public class BusOperatorService
{
    private static readonly string[] ValidStatuses = { "Active", "Inactive" };

    private readonly ApplicationDbContext _dbContext;

    public BusOperatorService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BusOperatorPagedResult> GetPagedAsync(
        string? searchTerm,
        string? status,
        int page = 1,
        int pageSize = 10)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 10 : pageSize;

        var filter = BuildFilter(searchTerm, status);
        var totalItems = await _dbContext.BusOperators.CountDocumentsAsync(filter);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        if (totalPages > 0 && page > totalPages)
        {
            page = totalPages;
        }

        var items = await _dbContext.BusOperators
            .Find(filter)
            .SortBy(op => op.OperatorCode)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return new BusOperatorPagedResult(items, totalItems, totalPages, page, pageSize);
    }

    public async Task<BusOperator?> GetByIdAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
        {
            return null;
        }

        return await _dbContext.BusOperators
            .Find(op => op.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<BusOperator> CreateAsync(BusOperator busOperator, string actor)
    {
        NormalizeOperator(busOperator);
        ValidateOperator(busOperator);

        if (string.IsNullOrWhiteSpace(busOperator.OperatorCode)
            || !IsValidOperatorCodeFormat(busOperator.OperatorCode))
        {
            busOperator.OperatorCode = await GenerateOperatorCodeAsync(busOperator.OperatorName);
        }

        if (!IsValidOperatorCodeFormat(busOperator.OperatorCode))
        {
            throw new InvalidOperationException("Operator code must follow format OP-HL-03.");
        }

        if (await OperatorCodeExistsAsync(busOperator.OperatorCode))
        {
            throw new InvalidOperationException("Operator code already exists.");
        }

        busOperator.Id = ObjectId.GenerateNewId().ToString();
        busOperator.CreatedAt = DateTime.UtcNow;
        busOperator.CreatedBy = actor;

        await _dbContext.BusOperators.InsertOneAsync(busOperator);

        return busOperator;
    }

    public async Task<BusOperator?> UpdateAsync(string id, BusOperator busOperator)
    {
        if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
        {
            return null;
        }

        var existing = await GetByIdAsync(id);
        if (existing == null)
        {
            return null;
        }

        NormalizeOperator(busOperator);
        ValidateOperator(busOperator);

        if (!IsValidOperatorCodeFormat(busOperator.OperatorCode))
        {
            throw new InvalidOperationException("Operator code must follow format OP-HL-03.");
        }

        if (await OperatorCodeExistsAsync(busOperator.OperatorCode, id))
        {
            throw new InvalidOperationException("Operator code already exists on another record.");
        }

        var update = Builders<BusOperator>.Update
            .Set(op => op.OperatorCode, busOperator.OperatorCode)
            .Set(op => op.OperatorName, busOperator.OperatorName)
            .Set(op => op.PhoneNumber, busOperator.PhoneNumber)
            .Set(op => op.Email, busOperator.Email)
            .Set(op => op.Address, busOperator.Address)
            .Set(op => op.ContactPerson, busOperator.ContactPerson)
            .Set(op => op.Status, busOperator.Status);

        await _dbContext.BusOperators.UpdateOneAsync(op => op.Id == id, update);

        return await GetByIdAsync(id);
    }

    public async Task<SoftDeleteBusOperatorResult> SoftDeleteAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
        {
            return new SoftDeleteBusOperatorResult(false, "Mã đối tác không hợp lệ.");
        }

        var existing = await GetByIdAsync(id);
        if (existing == null)
        {
            return new SoftDeleteBusOperatorResult(false, "Không tìm thấy đối tác nhà xe.");
        }

        if (existing.Status == "Inactive")
        {
            return new SoftDeleteBusOperatorResult(false, "Đối tác đã ở trạng thái Inactive.");
        }

        var update = Builders<BusOperator>.Update.Set(op => op.Status, "Inactive");
        await _dbContext.BusOperators.UpdateOneAsync(op => op.Id == id, update);

        return new SoftDeleteBusOperatorResult(true, "Ngưng hợp tác đối tác thành công.");
    }

    public async Task<string> GenerateOperatorCodeAsync(string? operatorName = null)
    {
        var prefix = ExtractPrefix(operatorName);
        var existingCodes = await _dbContext.BusOperators
            .Find(op => op.OperatorCode.StartsWith($"OP-{prefix}-"))
            .Project(op => op.OperatorCode)
            .ToListAsync();

        var maxSequence = 0;
        foreach (var code in existingCodes)
        {
            var parts = code.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out var sequence))
            {
                maxSequence = Math.Max(maxSequence, sequence);
            }
        }

        return $"OP-{prefix}-{(maxSequence + 1):D2}";
    }

    private static string ExtractPrefix(string? operatorName)
    {
        if (string.IsNullOrWhiteSpace(operatorName))
        {
            return "XX";
        }

        var words = operatorName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length >= 2)
        {
            return $"{char.ToUpperInvariant(words[^2][0])}{char.ToUpperInvariant(words[^1][0])}";
        }

        var word = words[0];
        if (word.Length >= 2)
        {
            return $"{char.ToUpperInvariant(word[0])}{char.ToUpperInvariant(word[1])}";
        }

        return $"{char.ToUpperInvariant(word[0])}X";
    }

    private static bool IsValidOperatorCodeFormat(string code)
    {
        return Regex.IsMatch(code, @"^OP-[A-Z]{2}-\d{2}$");
    }

    private async Task<bool> OperatorCodeExistsAsync(string operatorCode, string? excludedId = null)
    {
        var normalizedCode = operatorCode.Trim().ToUpperInvariant();
        var codeRegex = new BsonRegularExpression($"^{Regex.Escape(normalizedCode)}$", "i");

        var filter = Builders<BusOperator>.Filter.Regex(op => op.OperatorCode, codeRegex);
        if (!string.IsNullOrWhiteSpace(excludedId))
        {
            filter &= Builders<BusOperator>.Filter.Ne(op => op.Id, excludedId);
        }

        return await _dbContext.BusOperators.Find(filter).AnyAsync();
    }

    private static FilterDefinition<BusOperator> BuildFilter(string? searchTerm, string? status)
    {
        var filterBuilder = Builders<BusOperator>.Filter;
        var filter = filterBuilder.Empty;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var regex = new BsonRegularExpression(Regex.Escape(searchTerm.Trim()), "i");
            filter &= filterBuilder.Or(
                filterBuilder.Regex(op => op.OperatorCode, regex),
                filterBuilder.Regex(op => op.OperatorName, regex),
                filterBuilder.Regex(op => op.Email, regex),
                filterBuilder.Regex(op => op.PhoneNumber, regex),
                filterBuilder.Regex(op => op.Address, regex),
                filterBuilder.Regex(op => op.ContactPerson, regex));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= filterBuilder.Eq(op => op.Status, NormalizeStatus(status));
        }

        return filter;
    }

    private static void NormalizeOperator(BusOperator busOperator)
    {
        busOperator.OperatorCode = busOperator.OperatorCode.Trim().ToUpperInvariant();
        busOperator.OperatorName = busOperator.OperatorName.Trim();
        busOperator.PhoneNumber = busOperator.PhoneNumber.Trim();
        busOperator.Email = busOperator.Email.Trim();
        busOperator.Address = busOperator.Address.Trim();
        busOperator.ContactPerson = busOperator.ContactPerson.Trim();
        busOperator.Status = NormalizeStatus(busOperator.Status);
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Active";
        }

        var value = status.Trim();
        return ValidStatuses.FirstOrDefault(item => item.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? value;
    }

    private static void ValidateOperator(BusOperator busOperator)
    {
        if (string.IsNullOrWhiteSpace(busOperator.OperatorName))
        {
            throw new InvalidOperationException("Operator name is required.");
        }

        if (string.IsNullOrWhiteSpace(busOperator.PhoneNumber))
        {
            throw new InvalidOperationException("Phone number is required.");
        }

        if (string.IsNullOrWhiteSpace(busOperator.Email))
        {
            throw new InvalidOperationException("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(busOperator.Address))
        {
            throw new InvalidOperationException("Address is required.");
        }

        if (string.IsNullOrWhiteSpace(busOperator.ContactPerson))
        {
            throw new InvalidOperationException("Contact person is required.");
        }

        if (!ValidStatuses.Contains(busOperator.Status))
        {
            throw new InvalidOperationException("Status must be Active or Inactive.");
        }
    }
}
