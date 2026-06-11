using Bus_ticket.Models;
using Bus_ticket.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Bus_ticket.Services;

public class BusService : IBusService
{
    private readonly IMongoCollection<Bus> _buses;

    public BusService(IOptions<MongoDbSettings> mongoDbSettings)
    {
        var client = new MongoClient(mongoDbSettings.Value.ConnectionString);

        var database = client.GetDatabase(mongoDbSettings.Value.DatabaseName);

        _buses = database.GetCollection<Bus>(mongoDbSettings.Value.BusesCollectionName);

        CreateIndexes();
    }

    public async Task<List<Bus>> GetAllAsync()
    {
        return await _buses
            .Find(_ => true)
            .SortByDescending(x => x.DepartureTime)
            .ToListAsync();
    }

    public async Task<Bus?> GetByIdAsync(string id)
    {
        return await _buses
            .Find(x => x.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> BusNumberExistsAsync(string busNumber)
    {
        return await _buses
            .Find(x => x.BusNumber == busNumber)
            .AnyAsync();
    }

    public async Task<bool> BusCodeExistsAsync(string busCode)
    {
        return await _buses
            .Find(x => x.BusCode == busCode)
            .AnyAsync();
    }

    public async Task CreateAsync(Bus bus)
    {
        await _buses.InsertOneAsync(bus);
    }

    public async Task CancelManyAsync(List<string> ids)
    {
        var filter = Builders<Bus>.Filter.In(x => x.Id, ids);

        var update = Builders<Bus>.Update.Set(x => x.Status, BusStatus.Cancelled);

        await _buses.UpdateManyAsync(filter, update);
    }

    private void CreateIndexes()
    {
        var busCodeIndex = new CreateIndexModel<Bus>(
            Builders<Bus>.IndexKeys.Ascending(x => x.BusCode),
            new CreateIndexOptions { Unique = true });

        var busNumberIndex = new CreateIndexModel<Bus>(
            Builders<Bus>.IndexKeys.Ascending(x => x.BusNumber),
            new CreateIndexOptions { Unique = true });

        _buses.Indexes.CreateMany(new[] { busCodeIndex, busNumberIndex });
    }
}

public class MongoDbSettings
{
    public MongoClientSettings ConnectionString { get; set; }
}