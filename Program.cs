using Bus_ticket.Data;
using Bus_ticket.Helpers;
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
builder.Services.AddScoped<BranchService>();
builder.Services.AddSingleton<ICloudinaryService, CloudinaryService>();
builder.Services.AddScoped<SidebarPermissionService>();
builder.Services.AddScoped<NewsScraperService>();
builder.Services.AddSingleton<CrawlerProducer>();
builder.Services.AddScoped<NewsScraperService>();
builder.Services.AddHostedService<ArticleProcessorConsumer>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<TicketStatisticsService>();
builder.Services.AddScoped<VehicleRevenueStatisticsService>();
builder.Services.AddScoped<LowOccupancyTripsService>();

// Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });

builder.Services.AddAuthorization();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddControllersWithViews()
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix);

builder.Services.Configure<Bus_ticket.Settings.MomoSettings>(builder.Configuration.GetSection("Momo"));

builder.Services.AddScoped<Bus_ticket.Interfaces.IMomoService, Bus_ticket.Services.MomoService>();

builder.Services.AddScoped<IRabbitMQService, RabbitMQService>();

builder.Services.AddHostedService<RabbitMqConsumerService>();

var app = builder.Build();

var supportedCultures = new[] { "vi", "en" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);


using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<IDbSeeder>();
    await seeder.SeedAllAsync();

    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await BusClassIndexInitializer.EnsureIndexesAsync(dbContext.BusClasses);
}

// Configure HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseMiddleware<PermissionMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
using (var scope = app.Services.CreateScope())
{
    try
    {
        var crawler = scope.ServiceProvider.GetRequiredService<Bus_ticket.Services.CrawlerProducer>();
        // Fire and forget: Chạy ngầm tiến trình cào dữ liệu
        _ = Task.Run(() => crawler.StartCrawlingAsync());
        Console.WriteLine("[System] Đã tự động đẩy lệnh cào tin tức vào RabbitMQ!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] Lỗi auto-crawler: {ex.Message}");
    }
}

app.Run();