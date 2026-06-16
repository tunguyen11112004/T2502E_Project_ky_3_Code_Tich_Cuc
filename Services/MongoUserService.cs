using Bus_ticket.Models;
using Bus_ticket.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Bus_ticket.Services;

public class MongoUserService
{
    private readonly IMongoCollection<MongoUser> _users;

    public MongoUserService(IOptions<MongoDbSettings> mongoDbSettings)
    {
        var settings = mongoDbSettings.Value;

        var mongoClient = new MongoClient(settings.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(settings.DatabaseName);

        _users = mongoDatabase.GetCollection<MongoUser>(settings.UsersCollectionName);
    }

    public async Task<MongoUser?> GetByEmailAsync(string email)
    {
        return await _users
            .Find(user => user.Email == email)
            .FirstOrDefaultAsync();
    }

    public async Task<MongoUser?> GetByIdAsync(string id)
    {
        return await _users
            .Find(user => user.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task CreateAsync(MongoUser user)
    {
        await _users.InsertOneAsync(user);
    }
}