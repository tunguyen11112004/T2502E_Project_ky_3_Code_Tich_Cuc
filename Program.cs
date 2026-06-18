using Bus_ticket.Data;
using Bus_ticket.Middlewares;
using Bus_ticket.Models;
using Bus_ticket.Services;
using Bus_ticket.Settings;
using Microsoft.AspNetCore.Authentication.Cookies;
using MongoDB.Driver;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// MongoDB settings
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

// Services
builder.Services.AddSingleton<ApplicationDbContext>();
builder.Services.AddSingleton<UserService>();

// Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });

builder.Services.AddAuthorization();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllersWithViews()
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix);

var app = builder.Build();

var supportedCultures = new[] { "vi", "en" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

// ==================== SEED DATA INITIALIZER ====================
using (var scope = app.Services.CreateScope())
{
    var userService = scope.ServiceProvider.GetRequiredService<UserService>();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // 1. Seed Accounts (Admin & Employee)
    var adminEmail = "admin@src.com";
    var employeeEmail = "employee@src.com";

    var existingAdmin = await userService.GetByEmailAsync(adminEmail);
    if (existingAdmin == null)
    {
        await userService.CreateAsync(new User
        {
            UserCode = "ADM001",
            EmployeeCode = "000001",
            FullName = "System Admin",
            Email = adminEmail,
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Role = "Admin",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System"
        });
    }

    var existingEmployee = await userService.GetByEmailAsync(employeeEmail);
    if (existingEmployee == null)
    {
        await userService.CreateAsync(new User
        {
            UserCode = "EMP001",
            EmployeeCode = "123456",
            FullName = "Ticket Agent",
            Email = employeeEmail,
            Username = "employee01",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Employee@123"),
            Role = "Employee",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System"
        });
    }

    // 2. Seed Buses & Trips (Đã khớp hoàn toàn thuộc tính của RealtimeSeat)
    var busCollection = dbContext.Buses;
    var tripCollection = dbContext.Trips;

    if (await busCollection.Find(_ => true).CountDocumentsAsync() == 0)
    {
        var mockBus = new Bus
        {
            BusCode = "BUS-SRC01",
            LicensePlate = "30F-123.45",
            BusType = "Standard (Seat)",
            TotalSeats = 40
        };
        await busCollection.InsertOneAsync(mockBus);

        if (await tripCollection.Find(_ => true).CountDocumentsAsync() == 0)
        {
            var mockTrip = new Trip
            {
                BusId = mockBus.Id,
                DepartureTime = DateTime.UtcNow.AddDays(1),
                BaseFare = 150000,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                RealtimeSeats = Enumerable.Range(1, 40).Select(i => new RealtimeSeat
                {
                    SeatNumber = i < 10 ? "0" + i : i.ToString(),
                    Status = "Available"
                }).ToList()
            };
            await tripCollection.InsertOneAsync(mockTrip);
        }
    }
}
// ===============================================================

// Configure HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"), apiApp =>
{
    apiApp.UseMiddleware<PermissionMiddleware>();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();