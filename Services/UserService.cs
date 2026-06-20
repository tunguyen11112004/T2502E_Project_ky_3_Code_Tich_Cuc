using Bus_ticket.Models;
using Bus_ticket.Settings;
using Bus_ticket.ViewModels;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Security.Cryptography;

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

    public async Task<List<User>> GetEmployeesAsync()
    {
        return await _users
            .Find(user => user.Role == "Employee")
            .SortByDescending(user => user.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> EmployeeCodeExistsAsync(string employeeCode)
    {
        return await _users
            .Find(user => user.EmployeeCode == employeeCode || user.UserCode == employeeCode)
            .AnyAsync();
    }

    public async Task<string> GenerateUniqueEmployeeCodeAsync()
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var employeeCode = RandomNumberGenerator
                .GetInt32(0, 1_000_000)
                .ToString("D6");

            var exists = await EmployeeCodeExistsAsync(employeeCode);

            if (!exists)
            {
                return employeeCode;
            }
        }

        throw new InvalidOperationException("Unable to generate a unique employee code.");
    }

    public async Task CreateAsync(User user)
    {
        user.Email = user.Email.Trim().ToLower();

        await _users.InsertOneAsync(user);
    }

    public async Task<User> CreateEmployeeAsync(CreateEmployeeViewModel model, string createdBy)
    {
        var normalizedEmail = model.Email.Trim().ToLower();

        var existingUser = await GetByEmailAsync(normalizedEmail);
        if (existingUser != null)
        {
            throw new InvalidOperationException("Email này đã được đăng ký trong hệ thống");
        }

        var employeeCode = await GenerateUniqueEmployeeCodeAsync();

        var employee = new User
        {
            UserCode = employeeCode,
            EmployeeCode = employeeCode,
            FullName = model.FullName.Trim(),
            Email = normalizedEmail,
            PhoneNumber = model.PhoneNumber?.Trim() ?? string.Empty,
            EducationLevel = model.Qualifications?.Trim() ?? string.Empty,
            Username = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password, 10),
            Role = "Employee",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = createdBy
        };

        await CreateAsync(employee);

        return employee;
    }
}