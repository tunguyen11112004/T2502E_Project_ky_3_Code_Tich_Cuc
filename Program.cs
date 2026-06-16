using Bus_ticket.Data;
using Bus_ticket.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Bus_ticket.Services;
using Bus_ticket.Settings;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

builder.Services.AddSingleton<MongoUserService>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
Console.WriteLine("CONNECTION STRING = " + connectionString);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(10, 4, 32))
    ));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});
var app = builder.Build();
// Seed default MongoDB users for testing login
using (var scope = app.Services.CreateScope())
{
    var userService = scope.ServiceProvider.GetRequiredService<MongoUserService>();

    var adminEmail = "admin@src.com";
    var employeeEmail = "employee@src.com";

    var existingAdmin = await userService.GetByEmailAsync(adminEmail);
    if (existingAdmin == null)
    {
        await userService.CreateAsync(new MongoUser
        {
            FullName = "System Admin",
            EmployeeCode = "000001",
            Email = adminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Role = "Admin",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        });
    }

    var existingEmployee = await userService.GetByEmailAsync(employeeEmail);
    if (existingEmployee == null)
    {
        await userService.CreateAsync(new MongoUser
        {
            FullName = "Ticket Agent",
            EmployeeCode = "123456",
            Email = employeeEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Employee@123"),
            Role = "Employee",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        });
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();