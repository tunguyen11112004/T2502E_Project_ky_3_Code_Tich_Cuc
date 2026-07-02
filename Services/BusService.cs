using System.Text.RegularExpressions;
using Bus_ticket.Data;
using Bus_ticket.Helpers;
using Bus_ticket.Models;
using Bus_ticket.ViewModels;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Bus_ticket.Services;

public record BusPagedResult(List<BusListItemViewModel> Items, long TotalItems, int TotalPages, int CurrentPage, int PageSize);

public record DeleteBusResult(bool Succeeded, string Message, bool SoftDeleted = false, bool IsInUse = false);

public class BusService
{
    private static readonly string[] ValidStatuses = { "Active", "Maintenance", "Inactive" };

    private readonly ApplicationDbContext _dbContext;

    public BusService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<BusPagedResult> GetPagedAsync(
        string? searchTerm,
        string? status,
        string? busClassId,
        string? branchId,
        int page = 1,
        int pageSize = 10)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 10 : pageSize;

        var filter = BuildFilter(searchTerm, status, busClassId, branchId);
        var totalItems = await _dbContext.Buses.CountDocumentsAsync(filter);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        if (totalPages > 0 && page > totalPages)
        {
            page = totalPages;
        }

        var sort = Builders<Bus>.Sort
            .Descending(bus => bus.UpdatedAt)
            .Ascending(bus => bus.BusCode);

        var buses = await _dbContext.Buses
            .Find(filter)
            .Sort(sort)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        var items = await BuildListItemsAsync(buses);

        return new BusPagedResult(items, totalItems, totalPages, page, pageSize);
    }

    public async Task<List<BusOptionViewModel>> GetBusClassOptionsAsync()
    {
        var busClasses = await _dbContext.BusClasses
            .Find(Builders<BusClass>.Filter.Empty)
            .SortBy(busClass => busClass.ClassName)
            .ToListAsync();

        return busClasses.Select(busClass => new BusOptionViewModel
        {
            Id = busClass.Id ?? string.Empty,
            Text = $"{busClass.ClassName} - {busClass.BusType} ({busClass.TotalSeats} ghế)"
        }).ToList();
    }

    public async Task<List<BusOptionViewModel>> GetBranchOptionsAsync()
    {
        var branches = await _dbContext.Branches
            .Find(Builders<Branch>.Filter.Empty)
            .SortBy(branch => branch.BranchName)
            .ToListAsync();

        return branches.Select(branch => new BusOptionViewModel
        {
            Id = branch.Id ?? string.Empty,
            Text = $"{branch.BranchCode} - {branch.BranchName}"
        }).ToList();
    }

    public async Task<Bus?> GetByIdAsync(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
        {
            return null;
        }

        return await _dbContext.Buses.Find(bus => bus.Id == id).FirstOrDefaultAsync();
    }

    public async Task<Bus> CreateAsync(BusFormViewModel model, string actor)
    {
        NormalizeForm(model);
        await ValidateFormAsync(model);

        var busClass = await GetBusClassOrThrowAsync(model.BusClassId);
        var layout = BuildSeatLayout(busClass);

        var bus = new Bus
        {
            Id = ObjectId.GenerateNewId().ToString(),
            BusCode = string.IsNullOrWhiteSpace(model.BusCode)
                ? await GenerateBusCodeAsync()
                : model.BusCode.Trim().ToUpperInvariant(),
            LicensePlate = model.LicensePlate.Trim().ToUpperInvariant(),
            Status = NormalizeStatus(model.Status),
            BranchId = NormalizeNullableObjectId(model.BranchId),
            BusClassId = model.BusClassId,
            LegacyBusType = busClass.BusType,
            SeatsLayout = layout,
            LegacyTotalSeats = layout.Count,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = actor
        };

        await _dbContext.Buses.InsertOneAsync(bus);
        return bus;
    }

    public async Task<Bus?> UpdateAsync(string id, BusFormViewModel model, string actor)
    {
        if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
        {
            return null;
        }

        var existingBus = await GetByIdAsync(id);
        if (existingBus == null)
        {
            return null;
        }

        model.Id = id;
        NormalizeForm(model);
        await ValidateFormAsync(model, id);

        var busClass = await GetBusClassOrThrowAsync(model.BusClassId);
        var currentSeatCount = existingBus.SeatsLayout?.Count ?? 0;
        var totalSeats = currentSeatCount > 0 ? currentSeatCount : busClass.TotalSeats;

        var busCodeToSave = string.IsNullOrWhiteSpace(model.BusCode)
            ? (string.IsNullOrWhiteSpace(existingBus.BusCode) ? await GenerateBusCodeAsync() : existingBus.BusCode.Trim().ToUpperInvariant())
            : model.BusCode.Trim().ToUpperInvariant();

        var updateBuilder = Builders<Bus>.Update;
        var updates = new List<UpdateDefinition<Bus>>
        {
            updateBuilder.Set(bus => bus.BusCode, busCodeToSave),
            updateBuilder.Set(bus => bus.LicensePlate, model.LicensePlate.Trim().ToUpperInvariant()),
            updateBuilder.Set(bus => bus.Status, NormalizeStatus(model.Status)),
            updateBuilder.Set(bus => bus.BranchId, NormalizeNullableObjectId(model.BranchId)),
            updateBuilder.Set(bus => bus.BusClassId, model.BusClassId),
            updateBuilder.Set(bus => bus.LegacyBusType, busClass.BusType),
            updateBuilder.Set(bus => bus.LegacyTotalSeats, totalSeats),
            updateBuilder.Set(bus => bus.UpdatedAt, DateTime.UtcNow),
            updateBuilder.Set(bus => bus.UpdatedBy, actor)
        };

        // Chỉ tạo sơ đồ ghế khi dữ liệu cũ chưa có SeatsLayout.
        // Khi sửa xe, tuyệt đối không set đè SeatsLayout để tránh reset ma trận ghế đã cấu hình.
        if (currentSeatCount == 0)
        {
            var layout = BuildSeatLayout(busClass);
            updates.Add(updateBuilder.Set(bus => bus.SeatsLayout, layout));
            updates.Add(updateBuilder.Set(bus => bus.LegacyTotalSeats, layout.Count));
        }

        await _dbContext.Buses.UpdateOneAsync(bus => bus.Id == id, updateBuilder.Combine(updates));
        return await GetByIdAsync(id);
    }

    public async Task<DeleteBusResult> DeleteAsync(string id, string actor)
    {
        if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
        {
            return new DeleteBusResult(false, "Mã xe không hợp lệ.");
        }

        var bus = await GetByIdAsync(id);
        if (bus == null)
        {
            return new DeleteBusResult(false, "Không tìm thấy xe cần xóa.");
        }

        var usage = await GetBusUsageAsync(id);
        if (usage.TripCount > 0 || usage.BookingCount > 0)
        {
            var softDeleteUpdate = Builders<Bus>.Update
                .Set(item => item.Status, "Inactive")
                .Set(item => item.UpdatedAt, DateTime.UtcNow)
                .Set(item => item.UpdatedBy, actor);

            await _dbContext.Buses.UpdateOneAsync(item => item.Id == id, softDeleteUpdate);

            return new DeleteBusResult(
                true,
                $"Xe đã có {usage.TripCount} lịch trình hoặc {usage.BookingCount} vé phát sinh nên hệ thống không xóa cứng. Đã chuyển xe sang trạng thái Inactive.",
                SoftDeleted: true,
                IsInUse: true);
        }

        await _dbContext.Buses.DeleteOneAsync(item => item.Id == id);
        return new DeleteBusResult(true, "Xóa xe thành công.");
    }

    private async Task<List<BusListItemViewModel>> BuildListItemsAsync(List<Bus> buses)
    {
        if (!buses.Any())
        {
            return new List<BusListItemViewModel>();
        }

        var classIds = buses
            .Select(bus => bus.BusClassId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct()
            .ToList();

        var branchIds = buses
            .Select(bus => bus.BranchId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct()
            .ToList();

        var busClasses = classIds.Any()
            ? await _dbContext.BusClasses.Find(Builders<BusClass>.Filter.In(busClass => busClass.Id, classIds)).ToListAsync()
            : new List<BusClass>();

        var branches = branchIds.Any()
            ? await _dbContext.Branches.Find(Builders<Branch>.Filter.In(branch => branch.Id, branchIds)).ToListAsync()
            : new List<Branch>();

        var classMap = busClasses
            .Where(busClass => !string.IsNullOrWhiteSpace(busClass.Id))
            .ToDictionary(busClass => busClass.Id, busClass => busClass);
        var branchMap = branches
            .Where(branch => !string.IsNullOrWhiteSpace(branch.Id))
            .ToDictionary(branch => branch.Id!, branch => branch);

        var busIds = buses.Select(bus => bus.Id).ToList();
        var trips = await _dbContext.Trips.Find(Builders<Trip>.Filter.In(trip => trip.BusId, busIds)).ToListAsync();
        var tripIds = trips.Select(trip => trip.Id).ToList();
        var bookings = tripIds.Any()
            ? await _dbContext.Bookings.Find(Builders<Booking>.Filter.In(booking => booking.TripId, tripIds)).ToListAsync()
            : new List<Booking>();

        var tripCountByBus = trips.GroupBy(trip => trip.BusId).ToDictionary(group => group.Key, group => group.Count());
        var bookingCountByTrip = bookings.GroupBy(booking => booking.TripId).ToDictionary(group => group.Key, group => group.Count());

        return buses.Select(bus =>
        {
            classMap.TryGetValue(bus.BusClassId ?? string.Empty, out var busClass);
            branchMap.TryGetValue(bus.BranchId ?? string.Empty, out var branch);

            var busTripIds = trips.Where(trip => trip.BusId == bus.Id).Select(trip => trip.Id).ToList();
            var bookingCount = busTripIds.Sum(tripId => bookingCountByTrip.GetValueOrDefault(tripId, 0));
            var seatMatrixCount = bus.SeatsLayout?.Count ?? 0;
            var totalSeats = seatMatrixCount > 0
                ? seatMatrixCount
                : bus.LegacyTotalSeats ?? busClass?.TotalSeats ?? 0;

            return new BusListItemViewModel
            {
                Id = bus.Id,
                BusCode = bus.BusCode,
                LicensePlate = bus.LicensePlate,
                Status = string.IsNullOrWhiteSpace(bus.Status) ? "Active" : bus.Status,
                BranchId = bus.BranchId,
                BranchName = branch == null ? "—" : $"{branch.BranchCode} - {branch.BranchName}",
                BusClassId = bus.BusClassId,
                BusClassName = busClass?.ClassName ?? "—",
                BusType = busClass?.BusType ?? bus.LegacyBusType ?? "—",
                TotalSeats = totalSeats,
                SeatMatrixCount = seatMatrixCount,
                TripCount = tripCountByBus.GetValueOrDefault(bus.Id, 0),
                BookingCount = bookingCount,
                UpdatedAt = bus.UpdatedAt
            };
        }).ToList();
    }

    private async Task<(int TripCount, int BookingCount)> GetBusUsageAsync(string busId)
    {
        var trips = await _dbContext.Trips.Find(trip => trip.BusId == busId).ToListAsync();
        if (!trips.Any())
        {
            return (0, 0);
        }

        var tripIds = trips.Select(trip => trip.Id).ToList();
        var bookingCount = await _dbContext.Bookings.CountDocumentsAsync(
            Builders<Booking>.Filter.In(booking => booking.TripId, tripIds));

        return (trips.Count, (int)bookingCount);
    }

    private static FilterDefinition<Bus> BuildFilter(string? searchTerm, string? status, string? busClassId, string? branchId)
    {
        var filterBuilder = Builders<Bus>.Filter;
        var filter = filterBuilder.Empty;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var regex = new BsonRegularExpression(Regex.Escape(searchTerm.Trim()), "i");
            filter &= filterBuilder.Or(
                filterBuilder.Regex(bus => bus.BusCode, regex),
                filterBuilder.Regex(bus => bus.LicensePlate, regex),
                filterBuilder.Regex(bus => bus.LegacyBusType, regex));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= filterBuilder.Eq(bus => bus.Status, NormalizeStatus(status));
        }

        if (!string.IsNullOrWhiteSpace(busClassId) && ObjectId.TryParse(busClassId, out _))
        {
            filter &= filterBuilder.Eq(bus => bus.BusClassId, busClassId);
        }

        if (!string.IsNullOrWhiteSpace(branchId) && ObjectId.TryParse(branchId, out _))
        {
            filter &= filterBuilder.Eq(bus => bus.BranchId, branchId);
        }

        return filter;
    }

    private async Task ValidateFormAsync(BusFormViewModel model, string? excludedId = null)
    {
        if (string.IsNullOrWhiteSpace(model.LicensePlate))
        {
            throw new InvalidOperationException("Biển số xe là bắt buộc.");
        }

        if (string.IsNullOrWhiteSpace(model.BusClassId) || !ObjectId.TryParse(model.BusClassId, out _))
        {
            throw new InvalidOperationException("Loại/hạng xe không hợp lệ.");
        }

        if (!string.IsNullOrWhiteSpace(model.BranchId) && !ObjectId.TryParse(model.BranchId, out _))
        {
            throw new InvalidOperationException("Chi nhánh quản lý không hợp lệ.");
        }

        if (!ValidStatuses.Contains(NormalizeStatus(model.Status)))
        {
            throw new InvalidOperationException("Trạng thái xe không hợp lệ.");
        }

        if (!string.IsNullOrWhiteSpace(model.BusCode) && await BusCodeExistsAsync(model.BusCode, excludedId))
        {
            throw new InvalidOperationException("Mã xe đã tồn tại trong hệ thống.");
        }

        if (await LicensePlateExistsAsync(model.LicensePlate, excludedId))
        {
            throw new InvalidOperationException("Biển số xe đã tồn tại trong hệ thống.");
        }
    }

    private async Task<BusClass> GetBusClassOrThrowAsync(string busClassId)
    {
        var busClass = await _dbContext.BusClasses.Find(item => item.Id == busClassId).FirstOrDefaultAsync();
        if (busClass == null)
        {
            throw new InvalidOperationException("Không tìm thấy loại/hạng xe đã chọn.");
        }

        return busClass;
    }

    private async Task<bool> BusCodeExistsAsync(string busCode, string? excludedId = null)
    {
        var regex = new BsonRegularExpression($"^{Regex.Escape(busCode.Trim())}$", "i");
        var filter = Builders<Bus>.Filter.Regex(bus => bus.BusCode, regex);
        if (!string.IsNullOrWhiteSpace(excludedId))
        {
            filter &= Builders<Bus>.Filter.Ne(bus => bus.Id, excludedId);
        }

        return await _dbContext.Buses.Find(filter).AnyAsync();
    }

    private async Task<bool> LicensePlateExistsAsync(string licensePlate, string? excludedId = null)
    {
        var regex = new BsonRegularExpression($"^{Regex.Escape(licensePlate.Trim())}$", "i");
        var filter = Builders<Bus>.Filter.Regex(bus => bus.LicensePlate, regex);
        if (!string.IsNullOrWhiteSpace(excludedId))
        {
            filter &= Builders<Bus>.Filter.Ne(bus => bus.Id, excludedId);
        }

        return await _dbContext.Buses.Find(filter).AnyAsync();
    }

    private async Task<string> GenerateBusCodeAsync()
    {
        string busCode;
        do
        {
            busCode = "BUS-" + DateTime.UtcNow.ToString("yyMMdd") + "-" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        } while (await BusCodeExistsAsync(busCode));

        return busCode;
    }

    private static List<SeatTemplate> BuildSeatLayout(BusClass busClass)
    {
        if (busClass.DefaultLayout != null && busClass.DefaultLayout.Any())
        {
            return busClass.DefaultLayout
                .Select(seat => new SeatTemplate
                {
                    SeatNumber = seat.SeatNumber,
                    Row = seat.Row,
                    Column = seat.Column,
                    Floor = seat.Floor,
                    SeatType = seat.SeatType
                })
                .ToList();
        }

        return BusSeatLayoutGenerator.Generate(
            busClass.TotalRows,
            busClass.TotalColumns,
            busClass.TotalFloors,
            busClass.BusType);
    }

    private static void NormalizeForm(BusFormViewModel model)
    {
        model.BusCode = string.IsNullOrWhiteSpace(model.BusCode)
            ? null
            : model.BusCode.Trim().ToUpperInvariant();
        model.LicensePlate = model.LicensePlate.Trim().ToUpperInvariant();
        model.BusClassId = model.BusClassId.Trim();
        model.BranchId = NormalizeNullableObjectId(model.BranchId);
        model.Status = NormalizeStatus(model.Status);
    }

    private static string? NormalizeNullableObjectId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Active";
        }

        var matchedStatus = ValidStatuses.FirstOrDefault(item => item.Equals(status.Trim(), StringComparison.OrdinalIgnoreCase));
        return matchedStatus ?? status.Trim();
    }
}
