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
            Console.WriteLine("--> Bắt đầu seeding toàn bộ dữ liệu...");
            await SeedPermissions();
            await SeedRoles();
            await SeedUsers();
            await SeedBranches();
            await SeedBusClasses();
            await SeedSystemConfigs();
            await SeedBusesAndRoutes();
            await SeedTrips();
            await SeedBookings();
            Console.WriteLine("--> Hoàn tất khởi tạo dữ liệu hệ thống!");
        }

        private async Task SeedPermissions()
        {
            if (await _context.Permissions.CountDocumentsAsync(_ => true) > 0) return;
            await _context.Permissions.InsertManyAsync(new List<Permission> {
                new Permission { Name = "Full Access", Link = "/*", Method = "ALL" },
                new Permission { Name = "Manage Trip", Link = "/trip", Method = "GET" }
            });
        }

        private async Task SeedRoles()
        {
            if (await _context.Roles.CountDocumentsAsync(_ => true) > 0) return;
            var perms = await _context.Permissions.Find(_ => true).ToListAsync();
            await _context.Roles.InsertOneAsync(new DynamicRole { 
                RoleName = "Admin", PermissionIds = perms.Select(p => p.Id).ToList() 
            });
        }

        private async Task SeedUsers()
        {
            if (await _context.Users.CountDocumentsAsync(_ => true) > 0) return;
            var admin = new User { Username = "admin@src.com", FullName = "System Admin" };
            admin.Password = _passwordHasher.HashPassword(admin, "Admin@123");
            await _context.Users.InsertOneAsync(admin);
        }

        private async Task SeedBranches()
        {
            if (await _context.Branches.CountDocumentsAsync(_ => true) > 0) return;
            await _context.Branches.InsertManyAsync(new List<Branch> {
                new Branch { BranchCode = "HN01", BranchName = "Hanoi Head Office" },
                new Branch { BranchCode = "HCM01", BranchName = "Ho Chi Minh Head Office" },
                new Branch { BranchCode = "DN01", BranchName = "Da Nang Branch" },
                new Branch { BranchCode = "QN01", BranchName = "Quy Nhon Branch" }
            });
        }

        private async Task SeedBusClasses()
        {
            if (await _context.BusClasses.CountDocumentsAsync(_ => true) > 0) return;
            await _context.BusClasses.InsertManyAsync(new List<BusClass> {
                new BusClass { Name = "Express", BasePrice = 150000 },
                new BusClass { Name = "Luxury", BasePrice = 200000 },
                new BusClass { Name = "Volvo Non-A/C", BasePrice = 250000 }, // Đã thêm
                new BusClass { Name = "Volvo A/C", BasePrice = 300000 }
            });
        }

        private async Task SeedSystemConfigs()
        {
            if (await _context.SystemConfigs.CountDocumentsAsync(_ => true) > 0) return;
            var config = new SystemConfig {
                AgeDiscountRules = new List<AgeDiscountRule> {
                    new AgeDiscountRule { MinAge = 0, MaxAge = 4, DiscountPercentage = 100m },
                    new AgeDiscountRule { MinAge = 5, MaxAge = 12, DiscountPercentage = 50m },
                    new AgeDiscountRule { MinAge = 13, MaxAge = 50, DiscountPercentage = 0m },
                    new AgeDiscountRule { MinAge = 51, MaxAge = 150, DiscountPercentage = 30m }
                }
            };
            await _context.SystemConfigs.InsertOneAsync(config);
        }

        private async Task SeedBusesAndRoutes()
        {
            if (await _context.BusRoutes.CountDocumentsAsync(_ => true) > 0) return;

            var routeDefs = new List<(string F, string T, double D)> {
                ("Hanoi Head Office", "Da Nang Branch", 760.0),
                ("Da Nang Branch", "Hanoi Head Office", 760.0),
                ("Da Nang Branch", "Quy Nhon Branch", 300.0),
                ("Quy Nhon Branch", "Da Nang Branch", 300.0),
                ("Quy Nhon Branch", "Ho Chi Minh Head Office", 600.0),
                ("Ho Chi Minh Head Office", "Quy Nhon Branch", 600.0),
                ("Hanoi Head Office", "Ho Chi Minh Head Office", 1700.0),
                ("Ho Chi Minh Head Office", "Hanoi Head Office", 1700.0)
            };
            var routes = routeDefs.Select(r => new BusRoute { DeparturePoint = r.F, DestinationPoint = r.T, DistanceKm = r.D }).ToList();
            await _context.BusRoutes.InsertManyAsync(routes);

            var classes = await _context.BusClasses.Find(_ => true).ToListAsync();
            var branches = await _context.Branches.Find(_ => true).ToListAsync();
            var rand = new Random();

            foreach (var b in branches) {
                var randomClass = classes[rand.Next(classes.Count)];
                await _context.Buses.InsertOneAsync(new Bus { 
                    BusCode = $"BUS_{b.BranchCode}_{randomClass.Name.Substring(0, 3).ToUpper()}", 
                    BranchId = b.Id, 
                    BusType = randomClass.Name 
                });
            }
        }

        private async Task SeedTrips()
        {
            if (await _context.Trips.CountDocumentsAsync(_ => true) > 0) return;
            var routes = await _context.BusRoutes.Find(_ => true).ToListAsync();
            var buses = await _context.Buses.Find(_ => true).ToListAsync();
            var classes = await _context.BusClasses.Find(_ => true).ToListAsync();

            var trips = routes.Select(r => {
                var bus = buses[new Random().Next(buses.Count)];
                var cls = classes.FirstOrDefault(c => c.Name == bus.BusType);
                return new Trip { BusId = bus.Id, RouteId = r.Id, BaseFare = cls?.BasePrice ?? 300000 };
            }).ToList();
            await _context.Trips.InsertManyAsync(trips);
        }

        private async Task SeedBookings()
        {
            if (await _context.Bookings.CountDocumentsAsync(_ => true) > 0) return;
            var trips = await _context.Trips.Find(_ => true).ToListAsync();
            var config = await _context.SystemConfigs.Find(_ => true).FirstOrDefaultAsync();
            var rand = new Random();
            var bookings = new List<Booking>();

            for (int i = 1; i <= 30; i++)
            {
                var trip = trips[rand.Next(trips.Count)];
                int age = rand.Next(1, 70);
                var rule = config.AgeDiscountRules.FirstOrDefault(r => age >= r.MinAge && age <= r.MaxAge);
                decimal discount = rule?.DiscountPercentage ?? 0;
                decimal finalPrice = trip.BaseFare * (1 - (discount / 100));

                bookings.Add(new Booking {
                    BookingCode = $"BK{DateTime.UtcNow.Ticks.ToString().Substring(12)}",
                    TripId = trip.Id,
                    TotalPrice = trip.BaseFare,
                    FinalAmount = finalPrice,
                    BookingStatus = "Confirmed",
                    PaymentStatus = "Paid",
                    Passengers = new List<PassengerDetail> { new PassengerDetail { Name = $"Khách hàng {i}", FinalSeatPrice = finalPrice } },
                    Payment = new PaymentInfo { PaymentMethod = "Banking", AmountPaid = finalPrice }
                });
            }
            await _context.Bookings.InsertManyAsync(bookings);
        }
    }
}