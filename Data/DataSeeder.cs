using Bus_ticket.Interfaces;
using Bus_ticket.Models;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;

namespace Bus_ticket.Data
{
    public class DataSeeder : IDbSeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public DataSeeder(ApplicationDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
        }

        public async Task SeedAllAsync()
        {
            Console.WriteLine("--> Bắt đầu quá trình seeding dữ liệu...");

            // Gọi tuần tự các hàm seeding, mỗi hàm tự kiểm tra dữ liệu tồn tại
            await SeedPermissions();
            await SeedRoles();
            await SeedUsers();
            await SeedBranches();
            await SeedBusClasses(); // Thẻ 2
            await SeedBusesAndRoutes();
            await SeedTrips();

            Console.WriteLine("--> Hoàn tất khởi tạo dữ liệu hệ thống!");
        }

        private async Task SeedPermissions()
        {
            if (await _context.Permissions.CountDocumentsAsync(_ => true) > 0) return;
            await _context.Permissions.InsertManyAsync(new List<Permission> {
                new Permission { Name = "Full Access", Link = "/*", Method = "ALL", Description = "Quyền tối cao" },
                new Permission { Name = "Manage Trip", Link = "/trip", Method = "GET", Description = "Quản lý chuyến" }
            });
        }

        private async Task SeedRoles()
        {
            if (await _context.Roles.CountDocumentsAsync(_ => true) > 0) return;
            var perms = await _context.Permissions.Find(_ => true).ToListAsync();
            await _context.Roles.InsertOneAsync(new DynamicRole { 
                RoleName = "Admin", 
                PermissionIds = perms.Select(p => p.Id).ToList() 
            });
        }

        // Thẻ 1: Seeding tài khoản hệ thống (Admin & Employee)
        private async Task SeedUsers()
        {
            if (await _context.Users.CountDocumentsAsync(_ => true) > 0) return;

            var admin = new User { Username = "admin@src.com", FullName = "System Admin" };
            admin.Password = _passwordHasher.HashPassword(admin, "Admin@123");

            var employee = new User { Username = "employee01@src.com", FullName = "Staff User" };
            employee.Password = _passwordHasher.HashPassword(employee, "Emp@123");

            await _context.Users.InsertManyAsync(new List<User> { admin, employee });
            Console.WriteLine("--> Đã seed tài khoản Admin và Employee.");
        }

        private async Task SeedBranches()
        {
            if (await _context.Branches.CountDocumentsAsync(_ => true) > 0) return;
            var branches = new List<Branch>
            {
                new Branch { BranchCode = "HN01", BranchName = "Hanoi Head Office", Address = "13 Phan Tay Nhac Street, Xuan Phuong Ward, Ha Noi City." },
                new Branch { BranchCode = "HCM01", BranchName = "Ho Chi Minh Head Office", Address = "21 Bis Hau Giang, Tan Son Nhat Ward, Ho Chi Minh City." },
                new Branch { BranchCode = "DN01", BranchName = "Da Nang Branch", Address = "137 Nguyen Thi Thap Street, Thanh Khe Ward, Da Nang City." },
                new Branch { BranchCode = "QN01", BranchName = "Quy Nhon Branch", Address = "107A Thanh Nien Street, Quy Nhon Nam Ward, Gia Lai Province." }
            };
            await _context.Branches.InsertManyAsync(branches);
        }

        // Thẻ 2: Phân lớp xe và giá sàn
        private async Task SeedBusClasses()
        {
            if (await _context.BusClasses.CountDocumentsAsync(_ => true) > 0) return;
            var classes = new List<BusClass> {
                new BusClass { Name = "Express", BasePrice = 150000 },
                new BusClass { Name = "Luxury", BasePrice = 200000 },
                new BusClass { Name = "Volvo Non-A/C", BasePrice = 250000 },
                new BusClass { Name = "Volvo A/C", BasePrice = 300000 }
            };
            await _context.BusClasses.InsertManyAsync(classes);
            Console.WriteLine("--> Đã seed phân lớp xe và giá sàn.");
        }

        private async Task SeedBusesAndRoutes()
        {
            if (await _context.BusRoutes.CountDocumentsAsync(_ => true) > 0) return;

            var routeDefinitions = new List<(string From, string To)>
            {
                ("Hanoi Head Office", "Da Nang Branch"),
                ("Da Nang Branch", "Quy Nhon Branch"),
                ("Quy Nhon Branch", "Ho Chi Minh Head Office"),
                ("Da Nang Branch", "Hanoi Head Office"),
                ("Ho Chi Minh Head Office", "Quy Nhon Branch"),
                ("Quy Nhon Branch", "Da Nang Branch"),
                ("Da Nang Branch", "Hanoi Head Office")
            };

            var routes = routeDefinitions.Select(r => new BusRoute { DeparturePoint = r.From, DestinationPoint = r.To, DistanceKm = 500 }).ToList();
            await _context.BusRoutes.InsertManyAsync(routes);

            var branches = await _context.Branches.Find(_ => true).ToListAsync();
            foreach (var branch in branches)
            {
                await _context.Buses.InsertOneAsync(new Bus {
                    BusCode = $"BUS_{branch.BranchCode}",
                    LicensePlate = "51A-" + new Random().Next(10000, 99999),
                    BranchId = branch.Id,
                    TotalSeats = 40
                });
            }
        }

        private async Task SeedTrips()
        {
            if (await _context.Trips.CountDocumentsAsync(_ => true) > 0) return;
            var routes = await _context.BusRoutes.Find(_ => true).ToListAsync();
            var buses = await _context.Buses.Find(_ => true).ToListAsync();
            
            if (!routes.Any() || !buses.Any()) return;

            var trips = routes.Select(route => new Trip {
                BusId = buses.First().Id,
                RouteId = route.Id,
                BaseFare = 300000,
                DepartureTime = DateTime.UtcNow.AddDays(1),
                Status = "Scheduled"
            }).ToList();

            await _context.Trips.InsertManyAsync(trips);
        }
    }
}