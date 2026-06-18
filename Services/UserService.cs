using Bus_ticket.Models;
using Bus_ticket.Settings;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Bus_ticket.Services;

public class UserService
{
    private readonly IMongoCollection<User> _users;

    public UserService(IOptions<MongoDbSettings> mongoDbSettings)
    {
        var settings = mongoDbSettings.Value;

        var mongoClient = new MongoClient(settings.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(settings.DatabaseName);

        _users = mongoDatabase.GetCollection<User>(settings.UsersCollectionName);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLower();

        return await _users
            .Find(user => user.Email == normalizedEmail)
            .FirstOrDefaultAsync();
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        return await _users
            .Find(user => user.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task CreateAsync(User user)
    {
        user.Email = user.Email.Trim().ToLower();

        await _users.InsertOneAsync(user);
    }
}