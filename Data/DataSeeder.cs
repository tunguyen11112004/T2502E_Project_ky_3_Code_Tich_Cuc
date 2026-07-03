using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bus_ticket.Interfaces;
using Bus_ticket.Models;
using MongoDB.Driver;
using MongoDB.Bson;

namespace Bus_ticket.Data
{
    public class DataSeeder : IDbSeeder
    {
        private readonly ApplicationDbContext _context;

        // --- BRANCH ID ---
        public static readonly string BranchHanoiId = "64f1a2b3c4d5e6f7a8b9c001";
        public static readonly string BranchDanangId = "64f1a2b3c4d5e6f7a8b9c002";
        public static readonly string BranchSaigonId = "64f1a2b3c4d5e6f7a8b9c003";

        // --- BUS OPERATOR ID ---
        public static readonly string OperatorPhuongTrangId = "64f1a2b3c4d5e6f7a8b9c071";
        public static readonly string OperatorThanhBuoiId = "64f1a2b3c4d5e6f7a8b9c072";
        public static readonly string OperatorHoangLongId = "64f1a2b3c4d5e6f7a8b9c073";
        public static readonly string OperatorHaiVanId = "64f1a2b3c4d5e6f7a8b9c074";

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
        }

        public async Task SeedAllAsync()
        {
            Console.WriteLine("--> Bắt đầu seeding dữ liệu liên tỉnh chuẩn...");

            await SeedPermissions();
            await SeedDynamicRoles();
            await SeedUsers();
            await SeedBranches();
            await SeedBusOperators();
            await SeedBusClasses();
            await SeedSystemConfigs();
            await SeedBusesAndRoutes();
            await EnsureBusOperatorIdsForExistingBusesAsync();
            await SeedBusBranchesAsync();
            await SeedTrips();

            // Chạy bulk sinh chuyến xe toàn quốc sạch lỗi compile
            await SeedBulkTripsAndBookings();

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

        private async Task SeedBusOperators()
        {
            var count = await _context.BusOperators.CountDocumentsAsync(_ => true);
            if (count > 0) return;

            var operators = new List<BusOperator>
            {
                new BusOperator
                {
                    Id = OperatorPhuongTrangId,
                    OperatorCode = "OP-PT-01",
                    OperatorName = "Nhà xe Phương Trang",
                    PhoneNumber = "19006067",
                    Email = "phuongtrang@example.com",
                    Address = "TP. Hồ Chí Minh",
                    ContactPerson = "Phương Trang Admin",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "SystemSeeder"
                },
                new BusOperator
                {
                    Id = OperatorThanhBuoiId,
                    OperatorCode = "OP-TB-02",
                    OperatorName = "Nhà xe Thành Bưởi",
                    PhoneNumber = "19006079",
                    Email = "thanhbuoi@example.com",
                    Address = "TP. Hồ Chí Minh",
                    ContactPerson = "Thành Bưởi Admin",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "SystemSeeder"
                },
                new BusOperator
                {
                    Id = OperatorHoangLongId,
                    OperatorCode = "OP-HL-03",
                    OperatorName = "Nhà xe Hoàng Long",
                    PhoneNumber = "19009888",
                    Email = "hoanglong@example.com",
                    Address = "Hà Nội",
                    ContactPerson = "Hoàng Long Admin",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "SystemSeeder"
                },
                new BusOperator
                {
                    Id = OperatorHaiVanId,
                    OperatorCode = "OP-HV-04",
                    OperatorName = "Nhà xe Hải Vân",
                    PhoneNumber = "19006776",
                    Email = "haivan@example.com",
                    Address = "Đà Nẵng",
                    ContactPerson = "Hải Vân Admin",
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "SystemSeeder"
                }
            };

            await _context.BusOperators.InsertManyAsync(operators);
        }

        private List<SeatTemplate> GenerateSeatLayout(int totalRows, int totalColumns, int totalFloors, string busType)
        {
            var layout = new List<SeatTemplate>();

            if (busType == "Express_Seat") // Xe Ghế Ngồi 45 Chỗ (1 Tầng)
            {
                // Sinh tự động từ Hàng 1 đến Hàng 11
                for (int r = 1; r <= 11; r++)
                {
                    // Các ký tự cột A, B (Bên trái), C, D (Bên phải)
                    string[] colLetters = { "A", "B", "C", "D" };

                    // Hàng cuối (Hàng 11) thường có 5 ghế sát nhau (A, B, C, D, E)
                    int colsInRow = (r == 11) ? 5 : 4;
                    if (r == 11) colLetters = new string[] { "A", "B", "C", "D", "E" };

                    for (int c = 1; c <= colsInRow; c++)
                    {
                        layout.Add(new SeatTemplate
                        {
                            SeatNumber = $"{colLetters[c - 1]}{r:D2}", // Ví dụ: A01, B01, A11...
                            Row = r,
                            Column = c,
                            Floor = 1,
                            SeatType = "Standard"
                        });
                    }
                }
            }
            else if (busType == "Luxury_Sleeper") // Xe Giường Phòng VIP 22 Chỗ (2 Tầng - Mỗi tầng 11 phòng)
            {
                // Tầng 1 (Floor 1): 11 Phòng ký hiệu T1-01 đến T1-11
                // Tầng 2 (Floor 2): 11 Phòng ký hiệu T2-01 đến T2-11
                for (int f = 1; f <= totalFloors; f++)
                {
                    int roomCounter = 1;
                    // Thiết kế sơ đồ phân bổ hàng/cột cho giường phòng (4 hàng x 3 cột) để Frontend dễ vẽ bọc khung
                    for (int r = 1; r <= 4; r++)
                    {
                        for (int c = 1; c <= 3; c++)
                        {
                            if (roomCounter > 11) break; // Chỉ lấy đúng 11 phòng mỗi tầng

                            // Bỏ qua ô trống làm lối đi giữa nếu cần, ở đây xếp đều cấu hình lưới
                            layout.Add(new SeatTemplate
                            {
                                SeatNumber =
                                    $"T{f}-{roomCounter:D2}", // Ví dụ: T1-01 (Tầng 1 phòng 1), T2-05 (Tầng 2 phòng 5)
                                Row = r,
                                Column = c,
                                Floor = f,
                                SeatType = "VIP_Sleeper"
                            });
                            roomCounter++;
                        }
                    }
                }
            }

            return layout;
        }

        public async Task SeedBusClasses()
        {
            var count = await _context.BusClasses.CountDocumentsAsync(new BsonDocument());

            // Nếu đã chạy rồi thì xóa đi seed lại cho chuẩn cấu hình mới nhằm hiển thị thống kê đẹp nhất
            if (count > 0)
            {
                await _context.BusClasses.DeleteManyAsync(new BsonDocument());
            }

            var busClasses = new List<BusClass>
            {
                new BusClass
                {
                    Id = BusClassExpress45Id, // Sử dụng biến ID tĩnh sẵn có trong Seeder của bạn
                    ClassName = "Express Seat 45 (Xe Ghế Ngồi Phổ Thông)",
                    BusType = "Express_Seat",
                    ImageUrl =
                        "https://xetaibaoloc.com/images/stories/virtuemart/product/mercedes-benz-mb120s-47-ghe.jpg",
                    Status = "Active",
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
                    Id = BusClassLimousine22Id, // Sử dụng biến ID tĩnh sẵn có trong Seeder của bạn
                    ClassName = "Luxury Limousine Giường Phòng 22 (VIP)",
                    BusType = "Luxury_Sleeper",
                    ImageUrl = "https://vielimousine.com/wp-content/uploads/2021/12/DSC6090.jpg",
                    Status = "Active",
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

            // Đếm chính xác số lượng phần tử layout thực tế gán ngược lại cho thuộc tính TotalSeats
            busClasses[0].TotalSeats = busClasses[0].DefaultLayout.Count; // Sẽ tự động là 45
            busClasses[1].TotalSeats = busClasses[1].DefaultLayout.Count; // Sẽ tự động là 22

            await _context.BusClasses.InsertManyAsync(busClasses);
            Console.WriteLine(
                $"--> [THÀNH CÔNG] Đã Seeding xong bảng BusClass. Xe 45 chỗ: {busClasses[0].TotalSeats} ghế | Xe giường phòng: {busClasses[1].TotalSeats} phòng.");
        }

        private static List<string> GetAllowedBranchIdsForBus(Bus bus)
        {
            if (bus.OperatorId == OperatorPhuongTrangId)
            {
                return new List<string>
                {
                    BranchSaigonId,
                    BranchDanangId,
                    BranchHanoiId
                };
            }

            if (bus.OperatorId == OperatorThanhBuoiId)
            {
                return new List<string>
                {
                    BranchSaigonId,
                    BranchDanangId
                };
            }

            if (bus.OperatorId == OperatorHoangLongId)
            {
                return new List<string>
                {
                    BranchHanoiId,
                    BranchDanangId
                };
            }

            if (bus.OperatorId == OperatorHaiVanId)
            {
                return new List<string>
                {
                    BranchHanoiId,
                    BranchDanangId,
                    BranchSaigonId
                };
            }

            return !string.IsNullOrWhiteSpace(bus.BranchId)
                ? new List<string> { bus.BranchId }
                : new List<string>();
        }

        private async Task EnsureBusOperatorIdsForExistingBusesAsync()
        {
            async Task SetOperatorByBusCode(string busCode, string operatorId)
            {
                var update = Builders<Bus>.Update.Set(bus => bus.OperatorId, operatorId);

                await _context.Buses.UpdateOneAsync(
                    bus => bus.BusCode == busCode,
                    update
                );
            }

            await SetOperatorByBusCode("BUS-HN-EXP01", OperatorHoangLongId);
            await SetOperatorByBusCode("BUS-HN-LIMO02", OperatorHaiVanId);
            await SetOperatorByBusCode("BUS-SG-EXP03", OperatorPhuongTrangId);
            await SetOperatorByBusCode("BUS-SG-LIMO04", OperatorThanhBuoiId);

            await SetOperatorByBusCode("BUS-HN-EXP05", OperatorHoangLongId);
            await SetOperatorByBusCode("BUS-HN-LIMO06", OperatorHaiVanId);
            await SetOperatorByBusCode("BUS-HN-EXP07", OperatorHoangLongId);
            await SetOperatorByBusCode("BUS-HN-LIMO08", OperatorHaiVanId);

            await SetOperatorByBusCode("BUS-DN-EXP10", OperatorHaiVanId);
            await SetOperatorByBusCode("BUS-DN-LIMO11", OperatorHoangLongId);
            await SetOperatorByBusCode("BUS-DN-EXP12", OperatorHaiVanId);

            await SetOperatorByBusCode("BUS-SG-EXP15", OperatorPhuongTrangId);
            await SetOperatorByBusCode("BUS-SG-LIMO16", OperatorThanhBuoiId);
            await SetOperatorByBusCode("BUS-SG-EXP17", OperatorPhuongTrangId);
        }

        private async Task SeedBusBranchesAsync()
        {
            var existingBusBranches = await _context.BusBranches
                .CountDocumentsAsync(_ => true);

            if (existingBusBranches > 0)
            {
                return;
            }

            var buses = await _context.Buses
                .Find(_ => true)
                .ToListAsync();

            if (!buses.Any())
            {
                return;
            }

            var busBranches = new List<BusBranch>();

            foreach (var bus in buses)
            {
                var allowedBranchIds = GetAllowedBranchIdsForBus(bus);

                foreach (var branchId in allowedBranchIds.Distinct())
                {
                    busBranches.Add(new BusBranch
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        BusId = bus.Id,
                        BranchId = branchId,
                        Status = "Active",
                        RegisteredAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "SystemSeeder",
                        Note = "Seeded partner bus registration for SRC branch"
                    });
                }
            }

            if (busBranches.Any())
            {
                await _context.BusBranches.InsertManyAsync(busBranches);
            }
        }

        // ĐÃ SỬA: Loại bỏ các trường gây báo lỗi compile, chỉ giữ lại thuộc tính thực sự có trong Model Bus của bạn.
        public async Task SeedBusesAndRoutes()
        {
            // -----------------------------------------------------------------
            // 1. SEED DANH SÁCH XE (Đầy đủ cấu hình cho 4 hãng lớn, gán chuẩn BusClass)
            // -----------------------------------------------------------------
            var busCount = await _context.Buses.CountDocumentsAsync(new BsonDocument());

            // Luôn dọn dẹp để làm mới dữ liệu đồng bộ từ đầu
            if (busCount > 0)
            {
                await _context.Buses.DeleteManyAsync(new BsonDocument());
            }

            var buses = new List<Bus>
            {
                // === NHÀ XE PHƯƠNG TRANG (FUTA BUS LINES) ===
                new Bus
                {
                    Id = BusSGExpressId, BusCode = "BUS-PT-EXP01", LicensePlate = "51B-111.11", Status = "Active",
                    BranchId = BranchSaigonId, OperatorId = OperatorPhuongTrangId, BusClassId = BusClassExpress45Id,
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new Bus
                {
                    Id = "64f1a2b3c4d5e6f7a8b9c201", BusCode = "BUS-PT-LIMO02", LicensePlate = "51B-111.22",
                    Status = "Active",
                    BranchId = BranchSaigonId, OperatorId = OperatorPhuongTrangId, BusClassId = BusClassLimousine22Id,
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },

                // === NHÀ XE THÀNH BƯỞI ===
                new Bus
                {
                    Id = BusSGLimousineId, BusCode = "BUS-TB-LIMO01", LicensePlate = "51B-222.11", Status = "Active",
                    BranchId = BranchSaigonId, OperatorId = OperatorThanhBuoiId, BusClassId = BusClassLimousine22Id,
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new Bus
                {
                    Id = "64f1a2b3c4d5e6f7a8b9c202", BusCode = "BUS-TB-EXP02", LicensePlate = "51B-222.22",
                    Status = "Active",
                    BranchId = BranchSaigonId, OperatorId = OperatorThanhBuoiId, BusClassId = BusClassExpress45Id,
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },

                // === NHÀ XE HOÀNG LONG ===
                new Bus
                {
                    Id = BusHNExpressId, BusCode = "BUS-HL-EXP01", LicensePlate = "29B-333.11", Status = "Active",
                    BranchId = BranchHanoiId, OperatorId = OperatorHoangLongId, BusClassId = BusClassExpress45Id,
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new Bus
                {
                    Id = "64f1a2b3c4d5e6f7a8b9c203", BusCode = "BUS-HL-LIMO02", LicensePlate = "29B-333.22",
                    Status = "Active",
                    BranchId = BranchHanoiId, OperatorId = OperatorHoangLongId, BusClassId = BusClassLimousine22Id,
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },

                // === NHÀ XE HẢI VÂN ===
                new Bus
                {
                    Id = BusHNLimousineId, BusCode = "BUS-HV-LIMO01", LicensePlate = "29B-444.11", Status = "Active",
                    BranchId = BranchHanoiId, OperatorId = OperatorHaiVanId, BusClassId = BusClassLimousine22Id,
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new Bus
                {
                    Id = "64f1a2b3c4d5e6f7a8b9c204", BusCode = "BUS-HV-EXP02", LicensePlate = "43B-444.22",
                    Status = "Active",
                    BranchId = BranchDanangId, OperatorId = OperatorHaiVanId, BusClassId = BusClassExpress45Id,
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new Bus
                {
                    Id = "64f1a2b3c4d5e6f7a8b9c205", BusCode = "BUS-HV-LIMO03", LicensePlate = "43B-444.33",
                    Status = "Active",
                    BranchId = BranchDanangId, OperatorId = OperatorHaiVanId, BusClassId = BusClassLimousine22Id,
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                }
            };
            await _context.Buses.InsertManyAsync(buses);


            // -----------------------------------------------------------------
            // 2. SEED DANH SÁCH TUYẾN ĐƯỜNG ĐỐI LƯU (Đầy đủ 2 chiều đi - về)
            // -----------------------------------------------------------------
            var routeCount = await _context.BusRoutes.CountDocumentsAsync(new BsonDocument());
            if (routeCount > 0)
            {
                await _context.BusRoutes.DeleteManyAsync(new BsonDocument());
            }

            var routes = new List<BusRoute>
            {
                // --- CHẶNG 1: HÀ NỘI <--> SÀI GÒN ---
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
                        new FareConfig { BusType = "Express_Seat", FlatPrice = 750000m, VatPercentage = 10m },
                        new FareConfig { BusType = "Luxury_Sleeper", FlatPrice = 1100000m, VatPercentage = 10m }
                    },
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
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
                        new FareConfig { BusType = "Express_Seat", FlatPrice = 750000m, VatPercentage = 10m },
                        new FareConfig { BusType = "Luxury_Sleeper", FlatPrice = 1100000m, VatPercentage = 10m }
                    },
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },

                // --- CHẶNG 2: HÀ NỘI <--> ĐÀ NẴNG ---
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
                        new FareConfig { BusType = "Express_Seat", FlatPrice = 450000m, VatPercentage = 10m },
                        new FareConfig { BusType = "Luxury_Sleeper", FlatPrice = 650000m, VatPercentage = 10m }
                    },
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new BusRoute
                {
                    Id = "64f1a2b3c4d5e6f7a8b9c041", DeparturePoint = "Đà Nẵng", DestinationPoint = "Hà Nội",
                    DistanceKm = 760,
                    Stations = new List<Station>
                    {
                        new Station { StationName = "Bến xe Trung tâm Đà Nẵng", StopOrder = 1 },
                        new Station { StationName = "Bến xe Giáp Bát", StopOrder = 2 }
                    },
                    FareConfigs = new List<FareConfig>
                    {
                        new FareConfig { BusType = "Express_Seat", FlatPrice = 450000m, VatPercentage = 10m },
                        new FareConfig { BusType = "Luxury_Sleeper", FlatPrice = 650000m, VatPercentage = 10m }
                    },
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },

                // --- CHẶNG 3: ĐÀ NẴNG <--> SÀI GÒN ---
                new BusRoute
                {
                    Id = "64f1a2b3c4d5e6f7a8b9c034", DeparturePoint = "Đà Nẵng", DestinationPoint = "TP. Hồ Chí Minh",
                    DistanceKm = 960,
                    Stations = new List<Station>
                    {
                        new Station { StationName = "Bến xe Đà Nẵng", StopOrder = 1 },
                        new Station { StationName = "Bến xe Miền Đông", StopOrder = 2 }
                    },
                    FareConfigs = new List<FareConfig>
                    {
                        new FareConfig { BusType = "Express_Seat", FlatPrice = 500000m, VatPercentage = 10m },
                        new FareConfig { BusType = "Luxury_Sleeper", FlatPrice = 750000m, VatPercentage = 10m }
                    },
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new BusRoute
                {
                    Id = "64f1a2b3c4d5e6f7a8b9c042", DeparturePoint = "TP. Hồ Chí Minh", DestinationPoint = "Đà Nẵng",
                    DistanceKm = 960,
                    Stations = new List<Station>
                    {
                        new Station { StationName = "Bến xe Miền Đông", StopOrder = 1 },
                        new Station { StationName = "Bến xe Đà Nẵng", StopOrder = 2 }
                    },
                    FareConfigs = new List<FareConfig>
                    {
                        new FareConfig { BusType = "Express_Seat", FlatPrice = 500000m, VatPercentage = 10m },
                        new FareConfig { BusType = "Luxury_Sleeper", FlatPrice = 750000m, VatPercentage = 10m }
                    },
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },

                // --- CHẶNG 4: HÀ NỘI <--> HẢI PHÒNG ---
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
                    {
                        new FareConfig { BusType = "Express_Seat", FlatPrice = 150000m, VatPercentage = 10m }
                    },
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new BusRoute
                {
                    Id = "64f1a2b3c4d5e6f7a8b9c043", DeparturePoint = "Hải Phòng", DestinationPoint = "Hà Nội",
                    DistanceKm = 120,
                    Stations = new List<Station>
                    {
                        new Station { StationName = "Bến xe Niệm Nghĩa", StopOrder = 1 },
                        new Station { StationName = "Bến xe Gia Lâm", StopOrder = 2 }
                    },
                    FareConfigs = new List<FareConfig>
                    {
                        new FareConfig { BusType = "Express_Seat", FlatPrice = 150000m, VatPercentage = 10m }
                    },
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },

                // --- CHẶNG 5: SÀI GÒN <--> CẦN THƠ ---
                new BusRoute
                {
                    Id = "64f1a2b3c4d5e6f7a8b9c036", DeparturePoint = "TP. Hồ Chí Minh", DestinationPoint = "Cần Thơ",
                    DistanceKm = 170,
                    Stations = new List<Station>
                    {
                        new Station { StationName = "Bến xe Miền Tây", StopOrder = 1 },
                        new Station { StationName = "Bến xe Trung tâm Cần Thơ", StopOrder = 2 }
                    },
                    FareConfigs = new List<FareConfig>
                    {
                        new FareConfig { BusType = "Express_Seat", FlatPrice = 180000m, VatPercentage = 10m },
                        new FareConfig { BusType = "Luxury_Sleeper", FlatPrice = 280000m, VatPercentage = 10m }
                    },
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new BusRoute
                {
                    Id = "64f1a2b3c4d5e6f7a8b9c044", DeparturePoint = "Cần Thơ", DestinationPoint = "TP. Hồ Chí Minh",
                    DistanceKm = 170,
                    Stations = new List<Station>
                    {
                        new Station { StationName = "Bến xe Trung tâm Cần Thơ", StopOrder = 1 },
                        new Station { StationName = "Bến xe Miền Tây", StopOrder = 2 }
                    },
                    FareConfigs = new List<FareConfig>
                    {
                        new FareConfig { BusType = "Express_Seat", FlatPrice = 180000m, VatPercentage = 10m },
                        new FareConfig { BusType = "Luxury_Sleeper", FlatPrice = 280000m, VatPercentage = 10m }
                    },
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                }
            };
            await _context.BusRoutes.InsertManyAsync(routes);

            Console.WriteLine(
                $"--> [THÀNH CÔNG] Đã làm sạch và seeding lại {buses.Count} Xe & {routes.Count} Tuyến đường khứ hồi.");
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
                    RouteId = RouteHanoiSaigonId, BranchId = BranchHanoiId, BaseFare = 750000m,
                    DepartureTime = tomorrow.AddHours(8),
                    ArrivalTime = tomorrow.AddHours(38), Status = "Scheduled", RealtimeSeats = expressRealtimeSeats,
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new Trip
                {
                    Id = TripHanoiSaigonLimoId, TripCode = "TRP-2026-HN-SG02", BusId = BusHNLimousineId,
                    RouteId = RouteHanoiSaigonId, BranchId = BranchHanoiId, BaseFare = 1100000m,
                    DepartureTime = tomorrow.AddHours(20),
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
            // 1. SEED CUSTOMERS
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

            // 2. SEED BOOKINGS 
            var bookingCount = await _context.Bookings.CountDocumentsAsync(new BsonDocument());
            if (bookingCount == 0)
            {
                var bookings = new List<Booking>();
                var random = new Random();

                var allCustomers = await _context.Customers.Find(new BsonDocument()).ToListAsync();
                var allTrips = await _context.Trips.Find(new BsonDocument()).ToListAsync();

                if (allCustomers.Count == 0 || allTrips.Count == 0)
                {
                    return;
                }

                var futureTrips = allTrips.Where(t => t.DepartureTime > DateTime.UtcNow).ToList();
                var tripsToLeaveEmpty = futureTrips.Take(Math.Min(3, futureTrips.Count)).ToList();
                var tripsAvailableForBooking = allTrips.Except(tripsToLeaveEmpty).ToList();

                decimal seatPriceBase = 1100000m;

                // --- PHẦN 1: TẠO 200 VÉ ĐÃ ĐẶT ---
                int bookedCount = 0;
                int tripIndexForFullBooking = 0;

                while (bookedCount < 200 && tripsAvailableForBooking.Count > 0)
                {
                    var trip = tripsAvailableForBooking[tripIndexForFullBooking % tripsAvailableForBooking.Count];
                    tripIndexForFullBooking++;

                    for (int seatNum = 1; seatNum <= 20; seatNum++)
                    {
                        if (bookedCount >= 200) break;

                        var customer = allCustomers[random.Next(allCustomers.Count)];
                        string seatCode = $"A{seatNum:D2}";

                        decimal totalPrice = seatPriceBase;
                        decimal taxAmount = totalPrice * 0.1m;
                        decimal finalAmount = totalPrice + taxAmount;

                        bookings.Add(new Booking
                        {
                            BookingCode = $"BKG-SET-{1000 + bookedCount}",
                            CustomerId = customer.Id,
                            CustomerPhone = customer.PhoneNumber ?? "0912345678",
                            CustomerEmail = customer.Email ?? "customer@gmail.com",
                            TripId = trip.Id,
                            BranchId = BranchHanoiId,
                            BookingTime = DateTime.UtcNow.AddDays(-random.Next(1, 5)),
                            TotalPrice = totalPrice,
                            TaxAmount = taxAmount,
                            DiscountAmount = 0m,
                            FinalAmount = finalAmount,
                            BookingStatus = "Completed",
                            PaymentStatus = "Paid",
                            Passengers = new List<PassengerDetail>
                            {
                                new PassengerDetail
                                {
                                    SeatNumber = seatCode,
                                    Name = customer.FullName,
                                    // ĐÃ SỬA: Bỏ toán tử ?? vì Dob không thể null
                                    Dob = customer.Dob,
                                    FinalSeatPrice = seatPriceBase
                                }
                            },
                            Payment = new PaymentInfo
                            {
                                PaymentMethod = "Banking",
                                AmountPaid = finalAmount,
                                TransactionCode = $"VNPAY{random.Next(10000000, 99999999)}"
                            },
                            CreatedBy = "SystemSeeder",
                            CreatedAt = DateTime.UtcNow
                        });

                        bookedCount++;
                    }
                }

                // --- PHẦN 2: TẠO 100 VÉ ĐÃ HỦY ---
                for (int i = 0; i < 100; i++)
                {
                    var customer = allCustomers[random.Next(allCustomers.Count)];
                    var trip = tripsAvailableForBooking[random.Next(tripsAvailableForBooking.Count)];

                    decimal totalPrice = seatPriceBase;
                    decimal taxAmount = totalPrice * 0.1m;
                    decimal finalAmount = totalPrice + taxAmount;

                    bookings.Add(new Booking
                    {
                        BookingCode = $"BKG-CNC-{1000 + i}",
                        CustomerId = customer.Id,
                        CustomerPhone = customer.PhoneNumber ?? "0912345678",
                        CustomerEmail = customer.Email ?? "customer@gmail.com",
                        TripId = trip.Id,
                        BranchId = BranchHanoiId,
                        BookingTime = DateTime.UtcNow.AddDays(-random.Next(5, 10)),
                        TotalPrice = totalPrice,
                        TaxAmount = taxAmount,
                        DiscountAmount = 0m,
                        FinalAmount = finalAmount,
                        BookingStatus = "Cancelled",
                        PaymentStatus = random.Next(0, 2) == 0 ? "Refunded" : "Unpaid",
                        Passengers = new List<PassengerDetail>
                        {
                            new PassengerDetail
                            {
                                SeatNumber = $"B{random.Next(1, 10):D2}",
                                Name = customer.FullName,
                                // ĐÃ SỬA: Bỏ toán tử ?? ở cả dòng 858 này nữa
                                Dob = customer.Dob,
                                FinalSeatPrice = seatPriceBase
                            }
                        },
                        CreatedBy = "SystemSeeder",
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // 3. LƯU TẤT CẢ VÀO DATABASE
                if (bookings.Count > 0)
                {
                    await _context.Bookings.InsertManyAsync(bookings);
                }
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
        public async Task SeedBulkTripsAndBookings()
        {
            // 1. LÀM SẠCH TOÀN BỘ DATA CŨ
            await _context.Customers.DeleteManyAsync(new BsonDocument());
            await _context.Trips.DeleteManyAsync(new BsonDocument());
            await _context.Bookings.DeleteManyAsync(new BsonDocument());

            // Lấy dữ liệu cấu hình nền tảng
            var buses = await _context.Buses.Find(new BsonDocument()).ToListAsync();
            var routes = await _context.BusRoutes.Find(new BsonDocument()).ToListAsync();
            var busClasses = await _context.BusClasses.Find(new BsonDocument()).ToListAsync();

            if (!buses.Any() || !routes.Any())
            {
                Console.WriteLine("--> [LỖI] Cần chạy seed Bus và BusRoute trước!");
                return;
            }

            var random = new Random();

            // 2. SEED ĐỦ 200 CUSTOMERS
            var customers = new List<Customer>();
            string[] firstNames = { "Nguyễn", "Trần", "Lê", "Phạm", "Vũ", "Đặng", "Hoàng", "Bùi", "Đỗ", "Hồ", "Ngô" };
            string[] middleNames = { "Văn", "Thị", "Minh", "Hoàng", "Ngọc", "Tuấn", "Anh", "Đức", "Khánh", "Thúy" };
            string[] lastNames =
                { "Anh", "Bình", "Chương", "Dũng", "Em", "Hạnh", "Linh", "Nam", "Phúc", "Trang", "Yến", "Phát", "Tài" };
            string[] ranks = { "Standard", "Silver", "Gold", "Platinum" };
            string[] genders = { "Male", "Female" };

            for (int i = 1; i <= 200; i++)
            {
                var fullName =
                    $"{firstNames[random.Next(firstNames.Length)]} {middleNames[random.Next(middleNames.Length)]} {lastNames[random.Next(lastNames.Length)]}";
                bool isBlocked = (i % 45 == 0);
                var rank = ranks[random.Next(ranks.Length)];
                int points = rank == "Standard" ? random.Next(0, 100) :
                    rank == "Silver" ? random.Next(101, 500) :
                    rank == "Gold" ? random.Next(501, 1500) : random.Next(1501, 5000);

                customers.Add(new Customer
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    CustomerCode = $"KH-{1000 + i:D4}",
                    FullName = fullName,
                    Dob = new DateTime(random.Next(1980, 2005), random.Next(1, 13), random.Next(1, 28), 0, 0, 0,
                        DateTimeKind.Utc),
                    Gender = genders[random.Next(genders.Length)],
                    PhoneNumber = $"09{random.Next(10000000, 99999999)}",
                    Email = $"customer.{1000 + i}@futa.mail.com",
                    MembershipRank = rank,
                    TotalPoints = points,
                    IsBlocked = isBlocked,
                    ConsecutiveUnpaidCount = isBlocked ? 3 : 0,
                    BlockReason = isBlocked ? "Hủy chuyến liên tiếp không thanh toán" : null,
                    Status = isBlocked ? "Blocked" : "Active",
                    CreatedBy = "SystemSeeder",
                    UpdatedBy = "SystemSeeder"
                });
            }

            await _context.Customers.InsertManyAsync(customers);
            var activeCustomers = customers.Where(c => c.Status == "Active").ToList();

            // 3. TẠO TRIP VÀ BOOKING TRẢI DÀI (20/06 -> 01/09)
            var generatedTrips = new List<Trip>();
            var generatedBookings = new List<Booking>();

            DateTime dateStart = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc);
            DateTime dateEnd = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
            int[] tripHours = { 6, 12, 19 }; // Đủ 3 chuyến Sáng - Trưa - Tối
            int tripCounter = 1;
            int bookingCounter = 1;

            foreach (var route in routes)
            {
                var suitableBuses = buses.Where(b => b.Status == "Active").ToList();
                if (!suitableBuses.Any()) continue;

                for (var currentDay = dateStart; currentDay <= dateEnd; currentDay = currentDay.AddDays(1))
                {
                    foreach (var hour in tripHours)
                    {
                        var bus = suitableBuses[random.Next(suitableBuses.Count)];
                        var busClass = busClasses.FirstOrDefault(bc => bc.Id == bus.BusClassId);

                        var departureTime = new DateTime(currentDay.Year, currentDay.Month, currentDay.Day, hour, 0, 0,
                            DateTimeKind.Utc);

                        // Lấy giá vé từ FareConfig của tuyến đường, mặc định nếu không có là 200000
                        var fareConfig = route.FareConfigs.FirstOrDefault(f => f.BusType == busClass?.BusType) ??
                                         route.FareConfigs.FirstOrDefault();
                        decimal baseFare = fareConfig?.FlatPrice ?? 200000m;
                        double durationHours = route.DistanceKm / 60.0;

                        // --- 🎭 LOGIC HỦY CHUYẾN VẬN HÀNH (10% chuyến bị nhà xe hủy) ---
                        bool isCancelledByOperator = (random.Next(1, 11) == 1);
                        string tripStatus = isCancelledByOperator
                            ? "Cancelled"
                            : (departureTime < DateTime.UtcNow ? "Completed" : "Scheduled");

                        var trip = new Trip
                        {
                            Id = ObjectId.GenerateNewId().ToString(),
                            TripCode = $"TRIP-{tripCounter:D5}",
                            BusId = bus.Id,
                            RouteId = route.Id,
                            BranchId = bus.BranchId,
                            BaseFare = baseFare,
                            DepartureTime = departureTime,
                            ArrivalTime = departureTime.AddHours(durationHours),
                            Status = tripStatus,
                            RealtimeSeats = busClass?.DefaultLayout.Select(s => new RealtimeSeat
                            {
                                SeatNumber = s.SeatNumber,
                                Status = "Available"
                            }).ToList() ?? new List<RealtimeSeat>(),
                            CreatedBy = "SystemSeeder",
                            UpdatedBy = "SystemSeeder"
                        };

                        generatedTrips.Add(trip);
                        tripCounter++;

                        // Chỉ sinh Booking khi chuyến xe KHÔNG bị hủy vận hành và có cấu hình ghế
                        if (!isCancelledByOperator && trip.RealtimeSeats.Any())
                        {
                            int seatsToFill = 0;
                            bool isFuture = departureTime > DateTime.UtcNow;

                            if (!isFuture) // CHUYẾN TRONG QUÁ KHỨ (Đã chạy)
                            {
                                int scenario = random.Next(1, 4);
                                if (scenario == 1) // Kịch bản 1: Xe FULL 100% sạch ghế
                                {
                                    seatsToFill = trip.RealtimeSeats.Count;
                                }
                                else if (scenario == 2) // Kịch bản 2: Xe vừa đủ tầm (50% - 80% công suất)
                                {
                                    seatsToFill = (int)(trip.RealtimeSeats.Count * (random.Next(50, 81) / 100.0));
                                }
                                else // Kịch bản 3: Chuyến vắng khách ít chạy (15% - 30%)
                                {
                                    seatsToFill = (int)(trip.RealtimeSeats.Count * (random.Next(15, 31) / 100.0));
                                }
                            }
                            else // CHUYẾN TRONG TƯƠNG LAI (Mô tả đặt chỗ trước)
                            {
                                int scenario = random.Next(1, 4);
                                if (scenario == 1) // Kịch bản 1: TRỐNG NGUYÊN HOÀN TOÀN (0%)
                                {
                                    seatsToFill = 0;
                                }
                                else // Kịch bản 2: Đang mở bán trước lác đác vài ghế (5% - 15%)
                                {
                                    seatsToFill = (int)(trip.RealtimeSeats.Count * (random.Next(5, 16) / 100.0));
                                }
                            }

                            // Thực hiện tạo Booking dựa theo lượng ghế cần lấp đầy
                            int filledCounter = 0;
                            while (filledCounter < seatsToFill)
                            {
                                var availableSeats = trip.RealtimeSeats.Where(s => s.Status == "Available").ToList();
                                if (!availableSeats.Any()) break;

                                int partySize = random.Next(1, 3); // Đặt theo nhóm lẻ 1 hoặc 2 người
                                if (partySize > availableSeats.Count) partySize = availableSeats.Count;
                                if (filledCounter + partySize > seatsToFill) partySize = seatsToFill - filledCounter;

                                var bookedSeats = availableSeats.Take(partySize).ToList();
                                foreach (var seat in bookedSeats) seat.Status = "Booked";

                                var buyer = activeCustomers[random.Next(activeCustomers.Count)];
                                decimal totalPrice = baseFare * partySize;
                                decimal taxAmount = totalPrice * 0.1m;
                                decimal discountAmount = (random.Next(1, 11) > 8) ? 30000m : 0m;
                                decimal finalAmount = totalPrice + taxAmount - discountAmount;

                                var passengers = bookedSeats.Select(seat => new PassengerDetail
                                {
                                    SeatNumber = seat.SeatNumber,
                                    Name = buyer.FullName,
                                    PhoneNumber = buyer.PhoneNumber,
                                    Email = buyer.Email,
                                    Dob = new DateTime(1996, 5, 15, 0, 0, 0, DateTimeKind.Utc),
                                    FinalSeatPrice = baseFare
                                }).ToList();

                                string bookingStatus = "Completed";
                                string paymentStatus = "Paid";
                                var cancellationInfo = (CancellationInfo)null;
                                var paymentInfo = new PaymentInfo
                                {
                                    PaymentMethod = random.Next(0, 2) == 0 ? "Banking" : "Cash",
                                    AmountPaid = finalAmount,
                                    TransactionCode = $"TXN-{DateTime.UtcNow.Ticks}-{bookingCounter}"
                                };

                                // Nếu chuyến nằm trong quá khứ, cấu hình tỷ lệ Hủy Vé 15% ngẫu nhiên
                                if (!isFuture && random.Next(1, 101) <= 15)
                                {
                                    bookingStatus = "Canceled";
                                    paymentStatus = "Refunded";
                                    decimal penalty = 10m; // Thu phí hủy 10%
                                    decimal refund = finalAmount * (1 - (penalty / 100m));

                                    cancellationInfo = new CancellationInfo
                                    {
                                        CanceledAt = departureTime.AddHours(-random.Next(3, 12)),
                                        Reason = "Thay đổi kế hoạch di chuyển mùa hè",
                                        PenaltyPercentage = penalty,
                                        RefundAmount = refund
                                    };
                                    paymentInfo.AmountPaid = 0;

                                    // Đơn hủy -> Nhả lại ghế trống
                                    foreach (var seat in bookedSeats) seat.Status = "Available";
                                }
                                else if (isFuture) // Đơn tương lai: Thành công hoặc tạm Giữ chỗ (Reserved)
                                {
                                    if (random.Next(1, 11) > 8)
                                    {
                                        bookingStatus = "Reserved";
                                        paymentStatus = "Pending";
                                        paymentInfo = null;
                                        foreach (var seat in bookedSeats) seat.Status = "Holding";
                                    }
                                }

                                var booking = new Booking
                                {
                                    Id = ObjectId.GenerateNewId().ToString(),
                                    BookingCode = $"BKG-{departureTime:yyyyMMdd}-{bookingCounter:D6}",
                                    CustomerId = buyer.Id,
                                    CustomerPhone = buyer.PhoneNumber,
                                    CustomerEmail = buyer.Email,
                                    TripId = trip.Id,
                                    UserId = "64f1a2b3c4d5e6f7a8b9c999",
                                    BranchId = trip.BranchId ?? "64f1a2b3c4d5e6f7a8b9c001",
                                    BookingTime = departureTime.AddDays(-random.Next(1, 7)),
                                    TotalPrice = totalPrice,
                                    TaxAmount = taxAmount,
                                    DiscountAmount = discountAmount,
                                    FinalAmount = finalAmount,
                                    BookingStatus = bookingStatus,
                                    PaymentStatus = paymentStatus,
                                    Passengers = passengers,
                                    Payment = paymentInfo,
                                    Cancellation = cancellationInfo,
                                    CreatedAt = DateTime.UtcNow,
                                    CreatedBy = "SystemSeeder",
                                    UpdatedAt = DateTime.UtcNow,
                                    UpdatedBy = "SystemSeeder"
                                };

                                generatedBookings.Add(booking);
                                bookingCounter++;
                                filledCounter += partySize;
                            }
                        }
                    }
                }
            }

            // 4. LƯU HÀNG LOẠT VÀO MONGODB
            if (generatedTrips.Any())
                await _context.Trips.InsertManyAsync(generatedTrips);

            if (generatedBookings.Any())
                await _context.Bookings.InsertManyAsync(generatedBookings);

            Console.WriteLine($"--- SEEDING HOÀN TẤT VỚI BIỂU ĐỒ SIÊU ĐẸP ---");
            Console.WriteLine($"* Khách hàng: {customers.Count} Customers");
            Console.WriteLine($"* Chuyến xe vận hành: {generatedTrips.Count} Trips (Bao gồm cả các chuyến Cancelled)");
            Console.WriteLine($"* Tổng số vé bán ra: {generatedBookings.Count} Bookings");
        }

// Hàm Helper đóng gói tạo dữ liệu thực thể Booking
        private void GenerateBulkBooking(List<Booking> bulkBookings, List<string> seats, decimal baseFare,
            string tripId, string branchId, DateTime departureTime, int codeIndex, dynamic mockCustomers,
            string[] lastNames, string[] middleNames, string[] firstNames, Random random, string bookingStatus,
            string paymentStatus)
        {
            decimal totalPrice = baseFare * seats.Count;
            decimal taxAmount = totalPrice * 0.1m;
            decimal finalAmount = totalPrice + taxAmount;

            var passengers = seats.Select(seat => new PassengerDetail
            {
                SeatNumber = seat,
                Name =
                    $"{lastNames[random.Next(lastNames.Length)]} {middleNames[random.Next(middleNames.Length)]} {firstNames[random.Next(firstNames.Length)]}",
                Dob = DateTime.UtcNow.AddYears(-random.Next(20, 45)),
                FinalSeatPrice = baseFare
            }).ToList();

            var chosenCustomer = mockCustomers[random.Next(mockCustomers.Length)];
            string prefix = bookingStatus == "Cancelled" ? "CNC" : "BKG";

            bulkBookings.Add(new Booking
            {
                Id = ObjectId.GenerateNewId().ToString(),
                BookingCode = $"{prefix}-{departureTime:yyyyMMdd}-{codeIndex:D4}",
                CustomerId = chosenCustomer.Id,
                CustomerPhone = chosenCustomer.Phone,
                CustomerEmail = chosenCustomer.Email,
                TripId = tripId,
                BranchId = branchId,
                BookingTime = departureTime.AddHours(-random.Next(6, 48)),
                TotalPrice = totalPrice,
                TaxAmount = taxAmount,
                DiscountAmount = 0m,
                FinalAmount = finalAmount,
                BookingStatus = bookingStatus,
                PaymentStatus = paymentStatus,
                Passengers = passengers,
                Payment = bookingStatus == "Cancelled" && paymentStatus == "Unpaid"
                    ? null
                    : new PaymentInfo
                    {
                        PaymentMethod = random.Next(0, 2) == 0 ? "VNPAY" : "MOMO",
                        AmountPaid = finalAmount,
                        TransactionCode = $"TXN{departureTime:yyyyMMdd}{random.Next(10000, 99999)}"
                    },
                CreatedBy = "BulkDataAugustV5",
                CreatedAt = DateTime.UtcNow,
            });
        }
    }
}