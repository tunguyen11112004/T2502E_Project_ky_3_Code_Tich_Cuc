using Bus_ticket.Data;
using Bus_ticket.Interfaces;
using Bus_ticket.Middlewares;
using Bus_ticket.Models;
using Bus_ticket.Services;
using Bus_ticket.Settings;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

//Seeder
builder.Services.AddScoped<IDbSeeder, DataSeeder>();

// MVC
builder.Services.AddControllersWithViews();

// MongoDB settings
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

// Services
builder.Services.AddSingleton<ApplicationDbContext>();
builder.Services.AddSingleton<UserService>();
builder.Services.AddScoped<ITicketSearchService, TicketSearchService>();

// Cookie Authentication
// Used for MVC login session after successful MongoDB authentication.
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

// Seed default MongoDB users for testing login.
// NOTE: These accounts are only for local/demo testing.
// Later, real Admin/Employee accounts should be created from the system flow.
using (var scope = app.Services.CreateScope())
{
    var userService = scope.ServiceProvider.GetRequiredService<UserService>();

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
}

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<IDbSeeder>();
    await seeder.SeedAllAsync();
}

// Configure HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// NOTE:
// If local development only runs on http://localhost:5280,
// this line may show "Failed to determine the https port for redirect".
// It is safe to comment it during local testing.
// app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// NOTE:
// PermissionMiddleware belongs to the MongoDB dynamic RBAC/database structure branch.
// It checks permissions by RoleId + permissions collection.
//
// We only apply it to /api routes to avoid blocking MVC pages such as:
// /Account/Login
// /Admin
// /Employee
//
// MVC pages are protected by [Authorize] and [Authorize(Roles = "...")] in controllers.
app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"), apiApp =>
{
    apiApp.UseMiddleware<PermissionMiddleware>();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();