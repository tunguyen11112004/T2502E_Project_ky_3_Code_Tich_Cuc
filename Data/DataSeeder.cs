using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bus_ticket.Interfaces;
using Bus_ticket.Models;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;
using MongoDB.Bson;

namespace Bus_ticket.Data
{
    public class DataSeeder : IDbSeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly IMongoCollection<User> _users;
        private readonly IPasswordHasher<User> _passwordHasher;

        public DataSeeder(IMongoDatabase database, IPasswordHasher<User> passwordHasher)
        {
            // Lấy collection từ database (giả sử tên collection là "Users")
            _users = database.GetCollection<User>("Users");
            _passwordHasher = passwordHasher;
        }

        // --- BRANCH ID ---
        public static readonly string BranchHanoiId = "64f1a2b3c4d5e6f7a8b9c001";
        public static readonly string BranchDanangId = "64f1a2b3c4d5e6f7a8b9c002";
        public static readonly string BranchSaigonId = "64f1a2b3c4d5e6f7a8b9c003";

        // --- BUS CLASS ID ---
        public static readonly string BusClassExpress45Id = "64f1a2b3c4d5e6f7a8b9c011";
        public static readonly string BusClassLimousine22Id = "64f1a2b3c4d5e6f7a8b9c012";

        // --- 4 XE GỐC CỐ ĐỊNH ---
        public static readonly string BusHNExpressId = "64f1a2b3c4d5e6f7a8b9c021";
        public static readonly string BusHNLimousineId = "64f1a2b3c4d5e6f7a8b9c022";
        public static readonly string BusSGExpressId = "64f1a2b3c4d5e6f7a8b9c023";
        public static readonly string BusSGLimousineId = "64f1a2b3c4d5e6f7a8b9c024";

        // --- CÁC TUYẾN ĐƯỜNG CỐ ĐỊNH ---
        public static readonly string RouteHanoiSaigonId = "64f1a2b3c4d5e6f7a8b9c031";
        public static readonly string RouteSaigonHanoiId = "64f1a2b3c4d5e6f7a8b9c032";

        // --- CHUYẾN XE & ĐẶT VÉ MẪU ---
        public static readonly string TripHanoiSaigonExpressId = "64f1a2b3c4d5e6f7a8b9c041";
        public static readonly string TripHanoiSaigonLimoId = "64f1a2b3c4d5e6f7a8b9c042";
        public static readonly string CustomerNguyenVanAId = "64f1a2b3c4d5e6f7a8b9c051";
        public static readonly string BookingLimoId = "64f1a2b3c4d5e6f7a8b9c061";
        public static readonly string RoleAdminId = "64f1a2b3c4d5e6f7a8b9c099";

        private static List<string> _allPermissionIds = new List<string>();

        public DataSeeder(ApplicationDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
        }

        public async Task SeedAllAsync()
        {
            Console.WriteLine("--> Bắt đầu seeding dữ liệu liên tỉnh chuẩn...");

            await SeedPermissions();
            await SeedDynamicRoles();
            await SeedUsers();
            await SeedBranches();
            await SeedBusClasses();
            await SeedSystemConfigs();
            await SeedBusesAndRoutes();
            await BackfillBusSeatLayouts();
            await SeedTrips();
            await SeedBookings();

            // Chạy bulk sinh chuyến xe toàn quốc sạch lỗi compile
            await SeedBulkTripsAndBookings(200);

            Console.WriteLine("--> Hoàn tất khởi tạo dữ liệu hệ thống!");
        }

        private async Task SeedUsers()
        {
            if (await _context.Users.CountDocumentsAsync(_ => true) > 0) return;

            var users = new List<User>();

            // 1. Tạo tài khoản Admin chính
            var admin = new User
            {
                UserCode = "ADM001",
                EmployeeCode = "000001",
                FullName = "System Admin",
                Dob = null, // Phải đảm bảo kiểu dữ liệu là DateTime? hoặc nullable
                Email = "admin@src.com",
                PhoneNumber = "",
                Address = "",
                EducationLevel = "",
                Username = "admin",
                Role = "Admin",
                Status = "Active",
                RoleId = RoleAdminId,
                BranchId = BranchHanoiId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = ""
            };
            admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123");
            users.Add(admin);

            // 2. Tạo thêm 10 tài khoản nhân viên khác nhau
            for (int i = 1; i <= 10; i++)
            {
                var user = new User
                {
                    UserCode = $"EMP{i:D3}",
                    EmployeeCode = $"{i:D6}",
                    FullName = $"Staff {i}",
                    Dob = null,
                    Email = $"staff{i}@src.com",
                    PhoneNumber = $"090000000{i}",
                    Address = "Default Address",
                    EducationLevel = "University",
                    Username = $"employee01{i}",
                    Role = "Employee",
                    Status = "Active",
                    RoleId = null,
                    BranchId = BranchHanoiId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System",
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = ""
                };
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123");
                users.Add(user);
            }

            // Chèn danh sách vào database
            await _context.Users.InsertManyAsync(users);
        }

        public async Task SeedBranches()
        {
            var count = await _context.Branches.CountDocumentsAsync(new BsonDocument());
            if (count > 0) return;

            var branches = new List<Branch>
            {
                new Branch
                {
                    Id = BranchHanoiId, BranchCode = "CN-HN-01", BranchName = "Văn phòng Hà Nội (Bến xe Mỹ Đình)",
                    Address = "Số 20 Phạm Hùng, Mỹ Đình, Từ Liêm, Hà Nội", PhoneNumber = "02437685555",
                    Status = "Active", CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow,
                    UpdatedBy = "SystemSeeder", UpdatedAt = DateTime.UtcNow
                },
                new Branch
                {
                    Id = BranchDanangId, BranchCode = "CN-DN-02", BranchName = "Văn phòng Đà Nẵng (Bến xe Trung tâm)",
                    Address = "185 Tôn Đức Thắng, Hòa Minh, Liên Chiểu, Đà Nẵng", PhoneNumber = "02363767676",
                    Status = "Active", CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow,
                    UpdatedBy = "SystemSeeder", UpdatedAt = DateTime.UtcNow
                },
                new Branch
                {
                    Id = BranchSaigonId, BranchCode = "CN-HCM-03",
                    BranchName = "Văn phòng TP. Hồ Chí Minh (Bến xe Miền Đông)",
                    Address = "292 Đinh Bộ Lĩnh, Phường 26, Bình Thạnh, TP. HCM", PhoneNumber = "02838991607",
                    Status = "Active", CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow,
                    UpdatedBy = "SystemSeeder", UpdatedAt = DateTime.UtcNow
                }
            };
            await _context.Branches.InsertManyAsync(branches);
        }

        public async Task SeedBusClasses()
        {
            var count = await _context.BusClasses.CountDocumentsAsync(new BsonDocument());
            if (count > 0) return;

            var busClasses = new List<BusClass>
            {
                new BusClass
                {
                    Id = BusClassExpress45Id,
                    ClassName = "Express Seat 45",
                    BusType = "Express_Seat",
                    ImageUrl = "https://xetaibaoloc.com/images/stories/virtuemart/product/mercedes-benz-mb120s-47-ghe.jpg",
                    Status = "Active",
                    TotalSeats = 45,
                    TotalRows = 11,
                    TotalColumns = 4,
                    TotalFloors = 1,
                    DefaultLayout = GenerateSeatLayout(11, 4, 1, "Express_Seat"),
                    CreatedBy = "SystemSeeder",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new BusClass
                {
                    Id = BusClassLimousine22Id,
                    ClassName = "Luxury Limousine Giường Phòng 22",
                    BusType = "Luxury_Sleeper",
                    ImageUrl = "https://vielimousine.com/wp-content/uploads/2021/12/DSC6090.jpg",
                    Status = "Active",
                    TotalSeats = 22,
                    TotalRows = 4,
                    TotalColumns = 3,
                    TotalFloors = 2,
                    DefaultLayout = GenerateSeatLayout(4, 3, 2, "Luxury_Sleeper"),
                    CreatedBy = "SystemSeeder",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                }
            };
            busClasses[0].TotalSeats = busClasses[0].DefaultLayout.Count;
            busClasses[1].TotalSeats = busClasses[1].DefaultLayout.Count;

            await _context.BusClasses.InsertManyAsync(busClasses);
        }

        private List<SeatTemplate> GenerateSeatLayout(int totalRows, int totalColumns, int totalFloors, string busType)
        {
            var layout = new List<SeatTemplate>();

            if (totalRows <= 0) totalRows = 1;
            if (totalColumns <= 0) totalColumns = 1;
            if (totalFloors <= 0) totalFloors = 1;

            for (int floor = 1; floor <= totalFloors; floor++)
            {
                string floorPrefix = floor == 1 ? "A" : "B";
                int seatCounter = 1;

                for (int row = 1; row <= totalRows; row++)
                {
                    for (int col = 1; col <= totalColumns; col++)
                    {
                        string seatNumber = $"{floorPrefix}{seatCounter:D2}";
                        string seatType = busType == "Luxury_Sleeper"
                            ? "Sleeper"
                            : row <= 2 ? "VIP" : "Standard";

                        layout.Add(new SeatTemplate
                        {
                            SeatNumber = seatNumber,
                            Row = row,
                            Column = col,
                            Floor = floor,
                            SeatType = seatType
                        });

                        seatCounter++;
                    }
                }
            }

            return layout;
        }

        // Bổ sung ma trận ghế cho các xe seed cũ chưa có seatsLayout để MongoDB Compass khớp với UI đặt vé.
        private async Task BackfillBusSeatLayouts()
        {
            var missingLayoutFilter = Builders<Bus>.Filter.Or(
                Builders<Bus>.Filter.Exists("seatsLayout", false),
                Builders<Bus>.Filter.Eq("seatsLayout", BsonNull.Value),
                Builders<Bus>.Filter.Size("seatsLayout", 0)
            );

            var buses = await _context.Buses.Find(missingLayoutFilter).ToListAsync();
            if (!buses.Any()) return;

            var classIds = buses
                .Select(bus => bus.BusClassId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .Distinct()
                .ToList();

            if (!classIds.Any()) return;

            var busClasses = await _context.BusClasses
                .Find(Builders<BusClass>.Filter.In(busClass => busClass.Id, classIds))
                .ToListAsync();

            var classMap = busClasses.ToDictionary(busClass => busClass.Id, busClass => busClass);

            foreach (var bus in buses)
            {
                if (string.IsNullOrWhiteSpace(bus.BusClassId) || !classMap.TryGetValue(bus.BusClassId, out var busClass))
                {
                    continue;
                }

                var layout = busClass.DefaultLayout != null && busClass.DefaultLayout.Any()
                    ? busClass.DefaultLayout.Select(seat => new SeatTemplate
                    {
                        SeatNumber = seat.SeatNumber,
                        Row = seat.Row,
                        Column = seat.Column,
                        Floor = seat.Floor,
                        SeatType = seat.SeatType
                    }).ToList()
                    : GenerateSeatLayout(busClass.TotalRows, busClass.TotalColumns, busClass.TotalFloors, busClass.BusType);

                var update = Builders<Bus>.Update
                    .Set(item => item.SeatsLayout, layout)
                    .Set(item => item.LegacyBusType, busClass.BusType)
                    .Set(item => item.LegacyTotalSeats, layout.Count)
                    .Set(item => item.UpdatedAt, DateTime.UtcNow)
                    .Set(item => item.UpdatedBy, "SystemBackfill");

                await _context.Buses.UpdateOneAsync(item => item.Id == bus.Id, update);
            }
        }

        // Seed xe và tuyến nền tảng. SeatsLayout sẽ được backfill ngay sau đó nếu document cũ còn thiếu.
        public async Task SeedBusesAndRoutes()
        {
            var busCount = await _context.Buses.CountDocumentsAsync(new BsonDocument());
            if (busCount == 0)
            {
                var buses = new List<Bus>
                {
                    new Bus
                    {
                        Id = BusHNExpressId, BusCode = "BUS-HN-EXP01", LicensePlate = "29B-555.11", Status = "Active",
                        BranchId = BranchHanoiId, BusClassId = BusClassExpress45Id, CreatedBy = "SystemSeeder",
                        CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder", UpdatedAt = DateTime.UtcNow
                    },
                    new Bus
                    {
                        Id = BusHNLimousineId, BusCode = "BUS-HN-LIMO02", LicensePlate = "29B-999.22",
                        Status = "Active", BranchId = BranchHanoiId, BusClassId = BusClassLimousine22Id,
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                        UpdatedAt = DateTime.UtcNow
                    },
                    new Bus
                    {
                        Id = BusSGExpressId, BusCode = "BUS-SG-EXP03", LicensePlate = "51B-111.33", Status = "Active",
                        BranchId = BranchSaigonId, BusClassId = BusClassExpress45Id, CreatedBy = "SystemSeeder",
                        CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder", UpdatedAt = DateTime.UtcNow
                    },
                    new Bus
                    {
                        Id = BusSGLimousineId, BusCode = "BUS-SG-LIMO04", LicensePlate = "51B-888.44",
                        Status = "Active", BranchId = BranchSaigonId, BusClassId = BusClassLimousine22Id,
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                        UpdatedAt = DateTime.UtcNow
                    },

                    new Bus
                    {
                        Id = "64f1a2b3c4d5e6f7a8b9c025", BusCode = "BUS-HN-EXP05", LicensePlate = "29B-123.45",
                        Status = "Active", BranchId = BranchHanoiId, BusClassId = BusClassExpress45Id,
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow
                    },
                    new Bus
                    {
                        Id = "64f1a2b3c4d5e6f7a8b9c026", BusCode = "BUS-HN-LIMO06", LicensePlate = "29B-678.90",
                        Status = "Active", BranchId = BranchHanoiId, BusClassId = BusClassLimousine22Id,
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow
                    },
                    new Bus
                    {
                        Id = "64f1a2b3c4d5e6f7a8b9c027", BusCode = "BUS-HN-EXP07", LicensePlate = "29B-333.44",
                        Status = "Active", BranchId = BranchHanoiId, BusClassId = BusClassExpress45Id,
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow
                    },
                    new Bus
                    {
                        Id = "64f1a2b3c4d5e6f7a8b9c028", BusCode = "BUS-HN-LIMO08", LicensePlate = "29B-777.88",
                        Status = "Active", BranchId = BranchHanoiId, BusClassId = BusClassLimousine22Id,
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow
                    },

                    new Bus
                    {
                        Id = "64f1a2b3c4d5e6f7a8b9c02a", BusCode = "BUS-DN-EXP10", LicensePlate = "43B-111.22",
                        Status = "Active", BranchId = BranchDanangId, BusClassId = BusClassExpress45Id,
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow
                    },
                    new Bus
                    {
                        Id = "64f1a2b3c4d5e6f7a8b9c02b", BusCode = "BUS-DN-LIMO11", LicensePlate = "43B-333.44",
                        Status = "Active", BranchId = BranchDanangId, BusClassId = BusClassLimousine22Id,
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow
                    },
                    new Bus
                    {
                        Id = "64f1a2b3c4d5e6f7a8b9c02c", BusCode = "BUS-DN-EXP12", LicensePlate = "43B-555.66",
                        Status = "Active", BranchId = BranchDanangId, BusClassId = BusClassExpress45Id,
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow
                    },

                    new Bus
                    {
                        Id = "64f1a2b3c4d5e6f7a8b9c02f", BusCode = "BUS-SG-EXP15", LicensePlate = "51B-222.33",
                        Status = "Active", BranchId = BranchSaigonId, BusClassId = BusClassExpress45Id,
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow
                    },
                    new Bus
                    {
                        Id = "64f1a2b3c4d5e6f7a8b9c03a", BusCode = "BUS-SG-LIMO16", LicensePlate = "51B-444.55",
                        Status = "Active", BranchId = BranchSaigonId, BusClassId = BusClassLimousine22Id,
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow
                    },
                    new Bus
                    {
                        Id = "64f1a2b3c4d5e6f7a8b9c03b", BusCode = "BUS-SG-EXP17", LicensePlate = "51B-666.77",
                        Status = "Active", BranchId = BranchSaigonId, BusClassId = BusClassExpress45Id,
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow
                    }
                };
                await _context.Buses.InsertManyAsync(buses);
            }

            var routeCount = await _context.BusRoutes.CountDocumentsAsync(new BsonDocument());
            if (routeCount == 0)
            {
                var routes = new List<BusRoute>
                {
                    new BusRoute
                    {
                        Id = RouteHanoiSaigonId, DeparturePoint = "Hà Nội", DestinationPoint = "TP. Hồ Chí Minh",
                        DistanceKm = 1720,
                        Stations = new List<Station>
                        {
                            new Station { StationName = "Bến xe Mỹ Đình", StopOrder = 1 },
                            new Station { StationName = "Bến xe Miền Đông", StopOrder = 2 }
                        },
                        FareConfigs = new List<FareConfig>
                        {
                            new FareConfig { BusType = "Express_Seat", FlatPrice = 750000m },
                            new FareConfig { BusType = "Luxury_Sleeper", FlatPrice = 1100000m }
                        },
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow
                    },
                    new BusRoute
                    {
                        Id = RouteSaigonHanoiId, DeparturePoint = "TP. Hồ Chí Minh", DestinationPoint = "Hà Nội",
                        DistanceKm = 1720,
                        Stations = new List<Station>
                        {
                            new Station { StationName = "Bến xe Miền Đông", StopOrder = 1 },
                            new Station { StationName = "Bến xe Mỹ Đình", StopOrder = 2 }
                        },
                        FareConfigs = new List<FareConfig>
                        {
                            new FareConfig { BusType = "Express_Seat", FlatPrice = 750000m },
                            new FareConfig { BusType = "Luxury_Sleeper", FlatPrice = 1100000m }
                        },
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow
                    },

                    new BusRoute
                    {
                        Id = "64f1a2b3c4d5e6f7a8b9c033", DeparturePoint = "Hà Nội", DestinationPoint = "Đà Nẵng",
                        DistanceKm = 760,
                        Stations = new List<Station>
                        {
                            new Station { StationName = "Bến xe Giáp Bát", StopOrder = 1 },
                            new Station { StationName = "Bến xe Trung tâm Đà Nẵng", StopOrder = 2 }
                        },
                        FareConfigs = new List<FareConfig>
                        {
                            new FareConfig { BusType = "Express_Seat", FlatPrice = 450000m },
                            new FareConfig { BusType = "Luxury_Sleeper", FlatPrice = 650000m }
                        },
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow
                    },
                    new BusRoute
                    {
                        Id = "64f1a2b3c4d5e6f7a8b9c034", DeparturePoint = "Đà Nẵng",
                        DestinationPoint = "TP. Hồ Chí Minh", DistanceKm = 960,
                        Stations = new List<Station>
                        {
                            new Station { StationName = "Bến xe Đà Nẵng", StopOrder = 1 },
                            new Station { StationName = "Bến xe Miền Đông", StopOrder = 2 }
                        },
                        FareConfigs = new List<FareConfig>
                        {
                            new FareConfig { BusType = "Express_Seat", FlatPrice = 500000m },
                            new FareConfig { BusType = "Luxury_Sleeper", FlatPrice = 750000m }
                        },
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow
                    },
                    new BusRoute
                    {
                        Id = "64f1a2b3c4d5e6f7a8b9c035", DeparturePoint = "Hà Nội", DestinationPoint = "Hải Phòng",
                        DistanceKm = 120,
                        Stations = new List<Station>
                        {
                            new Station { StationName = "Bến xe Gia Lâm", StopOrder = 1 },
                            new Station { StationName = "Bến xe Niệm Nghĩa", StopOrder = 2 }
                        },
                        FareConfigs = new List<FareConfig>
                            { new FareConfig { BusType = "Express_Seat", FlatPrice = 150000m } },
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow
                    },
                    new BusRoute
                    {
                        Id = "64f1a2b3c4d5e6f7a8b9c036", DeparturePoint = "TP. Hồ Chí Minh",
                        DestinationPoint = "Cần Thơ", DistanceKm = 170,
                        Stations = new List<Station>
                        {
                            new Station { StationName = "Bến xe Miền Tây", StopOrder = 1 },
                            new Station { StationName = "Bến xe Trung tâm Cần Thơ", StopOrder = 2 }
                        },
                        FareConfigs = new List<FareConfig>
                        {
                            new FareConfig { BusType = "Express_Seat", FlatPrice = 180000m },
                            new FareConfig { BusType = "Luxury_Sleeper", FlatPrice = 280000m }
                        },
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow
                    }
                };
                await _context.BusRoutes.InsertManyAsync(routes);
            }
        }

        // ĐÃ SỬA: Thêm đầy đủ thuộc tính `TripCode` cho bản ghi chạy mẫu
        public async Task SeedTrips()
        {
            var count = await _context.Trips.CountDocumentsAsync(new BsonDocument());
            if (count > 0) return;

            var expressClass = await _context.BusClasses.Find(bc => bc.Id == BusClassExpress45Id).FirstOrDefaultAsync();
            var limousineClass =
                await _context.BusClasses.Find(bc => bc.Id == BusClassLimousine22Id).FirstOrDefaultAsync();
            if (expressClass == null || limousineClass == null) return;

            var expressRealtimeSeats = expressClass.DefaultLayout
                .Select(s => new RealtimeSeat { SeatNumber = s.SeatNumber, Status = "Available" }).ToList();
            var limousineRealtimeSeats = limousineClass.DefaultLayout
                .Select(s => new RealtimeSeat { SeatNumber = s.SeatNumber, Status = "Available" }).ToList();

            DateTime tomorrow = DateTime.UtcNow.Date.AddDays(1);
            var trips = new List<Trip>
            {
                new Trip
                {
                    Id = TripHanoiSaigonExpressId, TripCode = "TRP-2026-HN-SG01", BusId = BusHNExpressId,
                    RouteId = RouteHanoiSaigonId, BaseFare = 750000m, DepartureTime = tomorrow.AddHours(8),
                    ArrivalTime = tomorrow.AddHours(38), Status = "Scheduled", RealtimeSeats = expressRealtimeSeats,
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new Trip
                {
                    Id = TripHanoiSaigonLimoId, TripCode = "TRP-2026-HN-SG02", BusId = BusHNLimousineId,
                    RouteId = RouteHanoiSaigonId, BaseFare = 1100000m, DepartureTime = tomorrow.AddHours(20),
                    ArrivalTime = tomorrow.AddHours(48), Status = "Scheduled", RealtimeSeats = limousineRealtimeSeats,
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                }
            };
            await _context.Trips.InsertManyAsync(trips);
        }

        // ĐÃ SỬA: Đảm bảo trường customerPhone và customerEmail hoạt động tốt không dính lỗi
        public async Task SeedBookings()
        {
            var customerCount = await _context.Customers.CountDocumentsAsync(new BsonDocument());
            if (customerCount == 0)
            {
                var customers = new List<Customer>
                {
                    new Customer
                    {
                        Id = CustomerNguyenVanAId, CustomerCode = "KH-0001", FullName = "Nguyễn Văn A",
                        Dob = new DateTime(1963, 05, 20), Gender = "Nam", PhoneNumber = "0987654123",
                        Email = "nguyenvana@gmail.com", MembershipRank = "Gold", TotalPoints = 150, Status = "Active",
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow
                    },
                    new Customer
                    {
                        Id = "64f1a2b3c4d5e6f7a8b9c052", CustomerCode = "KH-0002", FullName = "Trần Thị B",
                        Dob = new DateTime(1998, 10, 15), Gender = "Nữ", PhoneNumber = "0912345678",
                        Email = "tranthib@gmail.com", MembershipRank = "Standard", TotalPoints = 0, Status = "Active",
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow
                    }
                };
                await _context.Customers.InsertManyAsync(customers);
            }

            var bookingCount = await _context.Bookings.CountDocumentsAsync(new BsonDocument());
            if (bookingCount == 0)
            {
                decimal seatPrice = 1100000m;
                decimal totalPrice = seatPrice * 2;
                decimal taxAmount = totalPrice * 0.1m;
                decimal finalAmount = totalPrice + taxAmount;

                var bookings = new List<Booking>
                {
                    new Booking
                    {
                        Id = BookingLimoId, BookingCode = "BKG-2026-0001", CustomerId = CustomerNguyenVanAId,
                        CustomerPhone = "0987654321", CustomerEmail = "nguyenvana@gmail.com",
                        TripId = TripHanoiSaigonLimoId, BranchId = BranchHanoiId,
                        BookingTime = DateTime.UtcNow.AddHours(-2), TotalPrice = totalPrice, TaxAmount = taxAmount,
                        DiscountAmount = 0m, FinalAmount = finalAmount, BookingStatus = "Completed",
                        PaymentStatus = "Paid",
                        Passengers = new List<PassengerDetail>
                        {
                            new PassengerDetail
                            {
                                SeatNumber = "A01", Name = "Nguyễn Văn A", Dob = new DateTime(1995, 05, 20),
                                FinalSeatPrice = seatPrice
                            },
                            new PassengerDetail
                            {
                                SeatNumber = "A02", Name = "Nguyễn Văn Long", Dob = new DateTime(1996, 02, 12),
                                FinalSeatPrice = seatPrice
                            }
                        },
                        Payment = new PaymentInfo
                            { PaymentMethod = "Banking", AmountPaid = finalAmount, TransactionCode = "VNPAY12345678" },
                        CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow
                    }
                };
                await _context.Bookings.InsertManyAsync(bookings);
            }
        }

        public async Task SeedSystemConfigs()
        {
            var config = await _context.SystemConfigs.Find(c => c.Id == "global_system_configuration")
                .FirstOrDefaultAsync();
            if (config != null) return;

            var globalConfig = new SystemConfig
            {
                Id = "global_system_configuration",
                AgeDiscountRules = new List<AgeDiscountRule>
                {
                    // Dưới 5 tuổi: Miễn phí (Giảm 100%)
                    new AgeDiscountRule { MinAge = 0, MaxAge = 4, DiscountPercentage = 100m },

                    // Từ 5 đến 12 tuổi: 50% giá vé (Giảm 50%)
                    new AgeDiscountRule { MinAge = 5, MaxAge = 12, DiscountPercentage = 50m },

                    // Trên 12 đến 50 tuổi: Không giảm giá (Giảm 0%)
                    new AgeDiscountRule { MinAge = 13, MaxAge = 50, DiscountPercentage = 0m },

                    // Trên 50 tuổi: Giảm 30%
                    new AgeDiscountRule { MinAge = 51, MaxAge = int.MaxValue, DiscountPercentage = 30m }
                },
                CancellationPolicies = new List<CancellationPolicy>
                {
                    new CancellationPolicy { HoursBeforeDeparture = 24, PenaltyPercentage = 10m },
                    new CancellationPolicy { HoursBeforeDeparture = 0, PenaltyPercentage = 100m }
                },
                UpdatedBy = "SystemSeeder", UpdatedAt = DateTime.UtcNow
            };
            await _context.SystemConfigs.InsertOneAsync(globalConfig);
        }

        public async Task SeedPermissions()
        {
            Console.WriteLine("--> Kiểm tra và seeding dữ liệu bảng Quyền hệ thống (Permission)...");

            var count =
                await _context.Permissions
                    .CountDocumentsAsync(new BsonDocument()); // Hãy chắc chắn _context.Permissions khớp với DbContext
            if (count > 0)
            {
                Console.WriteLine("--> Bảng Permission đã có dữ liệu. Bỏ qua seeding.");
                // Lấy lại danh sách ID hiện tại từ DB để nếu các hàm sau chạy vẫn có data liên kết
                var existingPermissions = await _context.Permissions.Find(new BsonDocument()).ToListAsync();
                _allPermissionIds = existingPermissions.Select(p => p.Id).ToList();
                return;
            }

            var permissions = new List<Permission>();

            // Hàm tiện ích nội bộ giúp sinh nhanh đối tượng Permission và gom ID
            void AddPermission(string id, string name, string description, string link, string method)
            {
                permissions.Add(new Permission
                {
                    Id = id,
                    Name = name,
                    Description = description,
                    Link = link,
                    Method = method
                });
                _allPermissionIds.Add(id); // Gom ID lại để dùng cho bảng Role ở bước sau
            }

            // --- NHÓM 1: QUẢN LÝ TUYẾN XE & CHUYẾN XE ---
            AddPermission("64f1a2b3c4d5e6f7a8b9ca01", "View.BusRoute", "Xem danh sách và chi tiết tuyến xe",
                "BusRoutes/Index", "GET");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca02", "Create.BusRoute", "Thêm tuyến xe chạy mới", "BusRoutes/Create",
                "POST");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca03", "Update.BusRoute", "Cập nhật thông tin tuyến xe",
                "BusRoutes/Edit", "POST");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca04", "Delete.BusRoute", "Xóa tuyến xe khỏi hệ thống",
                "BusRoutes/Delete", "POST");

            AddPermission("64f1a2b3c4d5e6f7a8b9ca05", "View.Trip", "Xem lịch trình các chuyến xe chạy", "Trips/Index",
                "GET");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca06", "Create.Trip", "Thêm chuyến xe mới (gán tài xế, xe, giờ chạy)",
                "Trips/Create", "POST");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca07", "Update.Trip", "Thay đổi thông tin, giờ khởi hành chuyến",
                "Trips/Edit", "POST");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca08", "Delete.Trip", "Hủy/Xóa chuyến xe", "Trips/Delete", "POST");

            // --- NHÓM 2: QUẢN LÝ XE, HẠNG XE & CHI NHÁNH (ĐÃ UPDATE TÁCH BUSCLASS) ---
AddPermission("64f1a2b3c4d5e6f7a8b9ca09", "View.Bus", "Xem danh sách xe và sơ đồ ghế", "Buses/Index",
                "GET");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca10", "Create.Bus", "Thêm xe mới vào đội xe", "Buses/Create", "POST");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca11", "Update.Bus", "Sửa thông tin xe (biển số, loại ghế)",
                "Buses/Edit", "POST");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca12", "Delete.Bus", "Xóa xe khỏi danh sách vận hành", "Buses/Delete",
                "POST");

            AddPermission("64f1a2b3c4d5e6f7a8b9ca13", "View.BusClass",
                "Xem danh sách hạng xe và cấu trúc sơ đồ ghế mẫu", "BusClasses/Index", "GET");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca14", "Create.BusClass", "Tạo hạng xe mới (Định nghĩa hàng, cột, tầng)",
                "BusClasses/Create", "POST");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca15", "Update.BusClass", "Cập nhật cấu hình hạng xe và sơ đồ mẫu",
                "BusClasses/Edit", "POST");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca16", "Delete.BusClass", "Xóa hạng xe khỏi hệ thống cấu hình",
                "BusClasses/Delete", "POST");

            AddPermission("64f1a2b3c4d5e6f7a8b9ca17", "View.Branch", "Xem danh sách văn phòng/chi nhánh nhà xe",
                "Branches/Index", "GET");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca18", "Create.Branch", "Thêm chi nhánh hoặc văn phòng mới",
                "Branches/Create", "POST");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca19", "Update.Branch", "Cập nhật địa chỉ, hotline chi nhánh",
                "Branches/Edit", "POST");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca20", "Delete.Branch", "Xóa chi nhánh", "Branches/Delete", "POST");

            // --- NHÓM 3: QUẢN LÝ VÉ & KHÁCH HÀNG ---
            AddPermission("64f1a2b3c4d5e6f7a8b9ca21", "View.Booking", "Xem danh sách lịch sử đặt vé của hệ thống",
                "Bookings/Index", "GET");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca22", "Create.Booking", "Đặt vé mới cho khách hàng", "Bookings/Create",
                "POST");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca23", "Update.Booking", "Thay đổi thông tin vé (đổi ghế, đổi chuyến)",
                "Bookings/Edit", "POST");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca24", "Delete.Booking", "Hủy vé/Hoàn trả vé", "Bookings/Delete",
                "POST");

            AddPermission("64f1a2b3c4d5e6f7a8b9ca25", "View.Customer", "Xem thông tin danh sách khách hàng",
                "Customers/Index", "GET");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca26", "Create.Customer", "Tạo mới tài khoản khách hàng",
                "Customers/Create", "POST");
AddPermission("64f1a2b3c4d5e6f7a8b9ca27", "Update.Customer", "Sửa thông tin thành viên khách hàng",
                "Customers/Edit", "POST");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca28", "Delete.Customer", "Khóa/Xóa tài khoản khách hàng",
                "Customers/Delete", "POST");

            // --- NHÓM 4: TÀI KHOẢN NHÂN VIÊN & PHÂN QUYỀN ---
            AddPermission("64f1a2b3c4d5e6f7a8b9ca29", "View.User", "Xem danh sách tài khoản nhân viên quản trị",
                "Users/Index", "GET");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca30", "Create.User", "Tạo tài khoản cho nhân viên/tài xế mới",
                "Users/Create", "POST");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca31", "Update.User", "Cập nhật thông tin nhân viên hoặc đổi mật khẩu",
                "Users/Edit", "POST");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca32", "Delete.User", "Xóa/Vô hiệu hóa tài khoản nhân viên",
                "Users/Delete", "POST");

            AddPermission("64f1a2b3c4d5e6f7a8b9ca33", "View.Role", "Xem danh sách vai trò (Nhóm quyền)",
                "DynamicRoles/Index", "GET");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca34", "Create.Role", "Tạo vai trò mới và chọn ma trận quyền",
                "DynamicRoles/Create", "POST");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca35", "Update.Role", "Sửa vai trò và cập nhật lại mảng quyền",
                "DynamicRoles/Edit", "POST");
            AddPermission("64f1a2b3c4d5e6f7a8b9ca36", "Delete.Role", "Xóa vai trò khỏi hệ thống", "DynamicRoles/Delete",
                "POST");

            AddPermission("64f1a2b3c4d5e6f7a8b9ca37", "View.Permission", "Xem danh sách quyền hệ thống có phân trang",
                "Permissions/Index", "GET");

            await _context.Permissions.InsertManyAsync(permissions);
            Console.WriteLine($"--> Đã seeding thành công trọn bộ {permissions.Count} quyền hệ thống!");
        }
        public async Task SeedDynamicRoles()
        {
            var count = await _context.DynamicRoles.CountDocumentsAsync(new BsonDocument());
            if (count > 0) return;

            var roles = new List<DynamicRole>
            {
                new DynamicRole
                {
                    Id = RoleAdminId,
                    RoleName = "SuperAdmin",
                    PermissionIds = _allPermissionIds,
                    CreatedBy = "SystemSeeder",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new DynamicRole
                {
                    Id = "64f1a2b3c4d5e6f7a8b9c098",
                    RoleName = "TicketAgent",
                    PermissionIds = new List<string>
                    {
                        "64f1a2b3c4d5e6f7a8b9ca05",
                        "64f1a2b3c4d5e6f7a8b9ca13",
                        "64f1a2b3c4d5e6f7a8b9ca21",
                        "64f1a2b3c4d5e6f7a8b9ca22",
                        "64f1a2b3c4d5e6f7a8b9ca25"
                    },
                    CreatedBy = "SystemSeeder",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                }
            };

            await _context.DynamicRoles.InsertManyAsync(roles);
        }

        // --- HÀM BULK ĐÃ FIX LỖI COMPILE ---
        // --- HÀM BULK PHỦ KÍN 100% CHẶNG × NGÀY TRONG 1 THÁNG ---
        public async Task SeedBulkTripsAndBookings(int count = 200)
        {
            Console.WriteLine("--> Bắt đầu sinh dữ liệu: Đảm bảo TẤT CẢ các chặng đều có chuyến trong MỌI NGÀY...");

            // Đổi tên check để tránh dính cache seeder cũ
            var existingBulkCount = await _context.Trips.CountDocumentsAsync(t => t.CreatedBy == "BulkDataSeederV4");
            if (existingBulkCount > 0)
            {
                Console.WriteLine("--> Dữ liệu phủ kín V4 đã tồn tại. Bỏ qua.");
                return;
            }

            var allRoutes = await _context.BusRoutes.Find(new BsonDocument()).ToListAsync();
            var allBuses = await _context.Buses.Find(new BsonDocument()).ToListAsync();
            var expressClass = await _context.BusClasses.Find(bc => bc.Id == BusClassExpress45Id).FirstOrDefaultAsync();
            var limousineClass =
                await _context.BusClasses.Find(bc => bc.Id == BusClassLimousine22Id).FirstOrDefaultAsync();

            if (!allRoutes.Any() || !allBuses.Any() || expressClass == null || limousineClass == null) return;

            var random = new Random();
            var bulkTrips = new List<Trip>();
            var bulkBookings = new List<Booking>();

            var mockCustomers = new[]
            {
                new { Id = CustomerNguyenVanAId, Phone = "0987654321", Email = "nguyenvana@gmail.com" },
                new { Id = "64f1a2b3c4d5e6f7a8b9c052", Phone = "0912345678", Email = "tranthib@gmail.com" }
            };

            var lastNames = new[] { "Nguyễn", "Trần", "Lê", "Phạm", "Hoàng" };
            var middleNames = new[] { "Văn", "Thị", "Anh", "Minh", "Đức" };
            var firstNames = new[] { "Hùng", "Lan", "Nam", "Vy", "Bình" };

            // Lấy mốc từ hôm nay và chạy liên tục 32 ngày tiếp theo để phủ kín hơn 1 tháng
            DateTime startDate = DateTime.UtcNow.Date;
            int totalDays = 32;
            int tripCounter = 1;

            for (int day = 0; day < totalDays; day++)
            {
                DateTime currentDay = startDate.AddDays(day);

                // VÒNG LẶP BẮT BUỘC: Duyệt qua từng chặng một để ngày nào cũng có đủ các chặng
                foreach (var route in allRoutes)
                {
                    // Mỗi chặng trong ngày sinh 2 chuyến: Sáng (08:00) và Tối (20:00) cho thoải mái lựa chọn
                    int[] departureHours = { 8, 20 };

                    foreach (var hour in departureHours)
                    {
                        DateTime departureTime = currentDay.AddHours(hour);
                        DateTime arrivalTime = departureTime.AddHours(12); // Giả định thời gian chạy cố định

                        // Chọn xe ngẫu nhiên để phân bổ
                        var selectedBus = allBuses[random.Next(allBuses.Count)];
                        var currentClass = selectedBus.BusClassId == BusClassExpress45Id
                            ? expressClass
                            : limousineClass;
                        decimal baseFare = selectedBus.BusClassId == BusClassExpress45Id ? 750000m : 1100000m;

                        var tripRealtimeSeats = currentClass.DefaultLayout
                            .Select(s => new RealtimeSeat { SeatNumber = s.SeatNumber, Status = "Available" })
                            .ToList();

                        // Sinh trạng thái ghế đã đặt ngẫu nhiên (để test sơ đồ hiển thị từ Frontend)
                        bool hasBookings = random.Next(1, 101) <= 60; // 60% chuyến có sẵn người đặt
                        var chosenSeatsForBooking = new List<string>();

                        if (hasBookings)
                        {
                            int bookedCount = random.Next(1, 3); // Giả lập đặt trước 1-2 ghế
                            for (int s = 0; s < bookedCount && s < tripRealtimeSeats.Count; s++)
                            {
                                tripRealtimeSeats[s].Status = "Booked";
                                chosenSeatsForBooking.Add(tripRealtimeSeats[s].SeatNumber);
                            }
                        }

                        string tripId = ObjectId.GenerateNewId().ToString();

                        // Đồng bộ TripCode chuẩn theo ngày tìm kiếm, sạch lỗi compile
                        var newTrip = new Trip
                        {
                            Id = tripId,
                            TripCode = $"TRP-{departureTime:yyyyMMdd}-{hour:D2}{tripCounter:D2}",
                            BusId = selectedBus.Id,
                            RouteId = route.Id,
                            BaseFare = baseFare,
                            DepartureTime = departureTime,
                            ArrivalTime = arrivalTime,
                            Status = "Scheduled",
                            RealtimeSeats = tripRealtimeSeats,
                            CreatedBy = "BulkDataSeederV4",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedBy = "BulkDataSeederV4",
                            UpdatedAt = DateTime.UtcNow
                        };
                        bulkTrips.Add(newTrip);

                        // Sinh Booking đi kèm để khớp dữ liệu Phone/Email phẳng
                        if (chosenSeatsForBooking.Any())
                        {
                            decimal totalPrice = baseFare * chosenSeatsForBooking.Count;
                            decimal taxAmount = totalPrice * 0.1m;
                            decimal finalAmount = totalPrice + taxAmount;

                            var passengers = chosenSeatsForBooking.Select(seat => new PassengerDetail
                            {
                                SeatNumber = seat,
                                Name =
                                    $"{lastNames[random.Next(lastNames.Length)]} {middleNames[random.Next(middleNames.Length)]} {firstNames[random.Next(firstNames.Length)]}",
                                Dob = DateTime.UtcNow.AddYears(-random.Next(20, 35)),
                                FinalSeatPrice = baseFare
                            }).ToList();

                            var chosenCustomer = mockCustomers[random.Next(mockCustomers.Length)];

                            var newBooking = new Booking
                            {
                                Id = ObjectId.GenerateNewId().ToString(),
                                BookingCode = $"BKG-{departureTime:yyyyMMdd}-{tripCounter:D4}",
                                CustomerId = chosenCustomer.Id,
                                CustomerPhone = chosenCustomer.Phone,
                                CustomerEmail = chosenCustomer.Email,
                                TripId = tripId,
                                UserId = null,
                                BranchId = selectedBus.BranchId,
                                BookingTime = departureTime.AddHours(-5),
                                TotalPrice = totalPrice,
                                TaxAmount = taxAmount,
                                DiscountAmount = 0m,
                                FinalAmount = finalAmount,
                                BookingStatus = "Completed",
                                PaymentStatus = "Paid",
                                Passengers = passengers,
                                Payment = new PaymentInfo
                                {
                                    PaymentMethod = "Banking",
                                    AmountPaid = finalAmount,
                                    TransactionCode = $"TXN{departureTime:yyyyMMdd}{random.Next(1000, 9999)}"
                                },
                                CreatedBy = "BulkDataSeederV4",
                                CreatedAt = DateTime.UtcNow,
                                UpdatedBy = "BulkDataSeederV4",
                                UpdatedAt = DateTime.UtcNow
                            };
                            bulkBookings.Add(newBooking);
                        }

                        tripCounter++;
                    }
                }
            }

            await _context.Trips.InsertManyAsync(bulkTrips);
            if (bulkBookings.Any())
            {
                await _context.Bookings.InsertManyAsync(bulkBookings);
            }

            Console.WriteLine(
                $"--> [THÀNH CÔNG V4] Đã phủ kín lịch trình {totalDays} ngày liên tiếp cho toàn bộ các chặng!");
        }
    }
}