using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bus_ticket.Interfaces;
using Bus_ticket.Helpers;
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
        public static readonly string BusClassLuxury22Id = "64f1a2b3c4d5e6f7a8b9c012";
        public static readonly string BusClassVolvoNonAc40Id = "64f1a2b3c4d5e6f7a8b9c013";
        public static readonly string BusClassVolvoAc40Id = "64f1a2b3c4d5e6f7a8b9c014";

        // Backward-compatible alias used by older seed references.
        public static readonly string BusClassLimousine22Id = BusClassLuxury22Id;

        // --- DYNAMIC ROLE ID ---
        public static readonly string RoleSuperAdminId = "64f1a2b3c4d5e6f7a8b9c099";
        public static readonly string RoleTicketAgentId = "64f1a2b3c4d5e6f7a8b9c098";
        public static readonly string RoleOperationsStaffId = "64f1a2b3c4d5e6f7a8b9c097";
        public static readonly string RoleAccountantId = "64f1a2b3c4d5e6f7a8b9c096";
        public static readonly string RoleBranchManagerId = "64f1a2b3c4d5e6f7a8b9c095";

        public static readonly string RoleAdminId = RoleSuperAdminId;

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

        private const string DefaultEmployeePassword = "Src@123456";
        private const string DefaultAdminPassword = "Admin@123";

        private static List<string> _allPermissionIds = new List<string>();

        public DataSeeder(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SeedAllAsync()
        {
            Console.WriteLine("--> Upsert permissions, dynamic roles và tài khoản mẫu...");
            await UpsertPermissionsAsync();
            await UpsertDynamicRolesAsync();
            await UpsertSampleUsersAsync();

            var isDataAlreadySeeded = _context.Branches != null && await _context.Branches.Find(_ => true).AnyAsync();

            /*if (isDataAlreadySeeded)
            {
                Console.WriteLine("--> [BỎ QUA] Dữ liệu nghiệp vụ đã tồn tại. Chỉ cập nhật role/permission/user.");
                await BackfillPaymentMethodsAsync();
                return;
            }*/
            
            Console.WriteLine("--> Bắt đầu seeding dữ liệu liên tỉnh chuẩn...");

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

        private async Task UpsertSampleUsersAsync()
        {
            var now = DateTime.UtcNow;
            var sampleUsers = new List<User>
            {
                BuildSampleUser("64f1a2b3c4d5e6f7a8b9d001", "ADM001", "000001", "System Admin", "admin@src.com", "admin", "Admin", RoleSuperAdminId, BranchHanoiId, DefaultAdminPassword, now),
                BuildSampleUser("64f1a2b3c4d5e6f7a8b9d002", "EMP002", "000002", "Ticket Agent Hà Nội", "ticketagent.hn@src.com", "ticketagent.hn", "Employee", RoleTicketAgentId, BranchHanoiId, DefaultEmployeePassword, now),
                BuildSampleUser("64f1a2b3c4d5e6f7a8b9d003", "EMP003", "000003", "Operations Staff Hà Nội", "operations.hn@src.com", "operations.hn", "Employee", RoleOperationsStaffId, BranchHanoiId, DefaultEmployeePassword, now),
                BuildSampleUser("64f1a2b3c4d5e6f7a8b9d004", "EMP004", "000004", "Accountant SRC", "accountant@src.com", "accountant", "Employee", RoleAccountantId, BranchHanoiId, DefaultEmployeePassword, now),
                BuildSampleUser("64f1a2b3c4d5e6f7a8b9d005", "EMP005", "000005", "Branch Manager Hà Nội", "manager.hn@src.com", "manager.hn", "Employee", RoleBranchManagerId, BranchHanoiId, DefaultEmployeePassword, now),
                BuildSampleUser("64f1a2b3c4d5e6f7a8b9d006", "EMP006", "000006", "Ticket Agent Đà Nẵng", "ticketagent.dn@src.com", "ticketagent.dn", "Employee", RoleTicketAgentId, BranchDanangId, DefaultEmployeePassword, now),
                BuildSampleUser("64f1a2b3c4d5e6f7a8b9d007", "EMP007", "000007", "Ticket Agent Sài Gòn", "ticketagent.sg@src.com", "ticketagent.sg", "Employee", RoleTicketAgentId, BranchSaigonId, DefaultEmployeePassword, now),
                BuildSampleUser("64f1a2b3c4d5e6f7a8b9d008", "EMP008", "000008", "Branch Manager Đà Nẵng", "manager.dn@src.com", "manager.dn", "Employee", RoleBranchManagerId, BranchDanangId, DefaultEmployeePassword, now),
                BuildSampleUser("64f1a2b3c4d5e6f7a8b9d009", "EMP009", "000009", "Branch Manager Sài Gòn", "manager.sg@src.com", "manager.sg", "Employee", RoleBranchManagerId, BranchSaigonId, DefaultEmployeePassword, now)
            };

            foreach (var user in sampleUsers)
            {
                var existingUser = await _context.Users
                    .Find(u => u.Email == user.Email)
                    .FirstOrDefaultAsync();

                if (existingUser == null)
                {
                    // Tài khoản chưa tồn tại: tạo mới cùng mật khẩu mặc định.
                    await _context.Users.InsertOneAsync(user);
                    continue;
                }

                // Tài khoản đã tồn tại: chỉ cập nhật thông tin role/chi nhánh,
                // tuyệt đối không ghi đè PasswordHash, Id và ActiveSessionId.
                var update = Builders<User>.Update
                    .Set(u => u.UserCode, user.UserCode)
                    .Set(u => u.EmployeeCode, user.EmployeeCode)
                    .Set(u => u.FullName, user.FullName)
                    .Set(u => u.Email, user.Email)
                    .Set(u => u.Username, user.Username)
                    .Set(u => u.Role, user.Role)
                    .Set(u => u.RoleId, user.RoleId)
                    .Set(u => u.BranchId, user.BranchId)
                    .Set(u => u.Status, user.Status)
                    .Set(u => u.UpdatedAt, DateTime.UtcNow)
                    .Set(u => u.UpdatedBy, "SystemSeeder");

                await _context.Users.UpdateOneAsync(
                    u => u.Id == existingUser.Id,
                    update);
            }

            Console.WriteLine($"--> [THÀNH CÔNG] Upsert {sampleUsers.Count} tài khoản mẫu theo role nghiệp vụ.");
        }

        private static User BuildSampleUser(
            string id,
            string userCode,
            string employeeCode,
            string fullName,
            string email,
            string username,
            string role,
            string roleId,
            string branchId,
            string password,
            DateTime now)
        {
            var user = new User
            {
                Id = id,
                UserCode = userCode,
                EmployeeCode = employeeCode,
                FullName = fullName,
                Dob = null,
                Email = email,
                PhoneNumber = "",
                Address = "",
                EducationLevel = "",
                Username = username,
                Role = role,
                Status = "Active",
                RoleId = roleId,
                BranchId = branchId,
                CreatedAt = now,
                CreatedBy = "SystemSeeder",
                UpdatedAt = now,
                UpdatedBy = "SystemSeeder"
            };
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            return user;
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
            else if (busType is "Luxury_Sleeper" or "Volvo_NonAC" or "Volvo_AC")
            {
                var seatType = busType == "Luxury_Sleeper"
                    ? "VIP_Sleeper"
                    : busType == "Volvo_AC"
                        ? "Volvo_AC_Sleeper"
                        : "Volvo_NonAC_Sleeper";

                for (int f = 1; f <= totalFloors; f++)
                {
                    int roomCounter = 1;
                    for (int r = 1; r <= 4; r++)
                    {
                        for (int c = 1; c <= 3; c++)
                        {
                            if (roomCounter > 11) break;

                            layout.Add(new SeatTemplate
                            {
                                SeatNumber = $"T{f}-{roomCounter:D2}",
                                Row = r,
                                Column = c,
                                Floor = f,
                                SeatType = seatType
                            });
                            roomCounter++;
                        }
                    }
                }
            }

            return layout;
        }

        private static List<FareConfig> BuildStandardFareConfigs(decimal expressPrice)
        {
            return new List<FareConfig>
            {
                new FareConfig { BusType = "Express_Seat", FlatPrice = expressPrice, VatPercentage = 10m },
                new FareConfig { BusType = "Luxury_Sleeper", FlatPrice = Math.Round(expressPrice * 1.45m), VatPercentage = 10m },
                new FareConfig { BusType = "Volvo_NonAC", FlatPrice = Math.Round(expressPrice * 1.65m), VatPercentage = 10m },
                new FareConfig { BusType = "Volvo_AC", FlatPrice = Math.Round(expressPrice * 1.85m), VatPercentage = 10m }
            };
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
                    Id = BusClassExpress45Id,
                    ClassName = "Express 45 (Ghế ngồi phổ thông)",
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
                    Id = BusClassLuxury22Id,
                    ClassName = "Luxury 22 (Giường phòng VIP)",
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
                },
                new BusClass
                {
                    Id = BusClassVolvoNonAc40Id,
                    ClassName = "Volvo 40 (Non A/C - Giường nằm thông khí tự nhiên)",
                    BusType = "Volvo_NonAC",
                    ImageUrl = "https://res.cloudinary.com/nguyenanhtu/image/upload/v1785673173/xe-bus-giuong-nam-thaco-resize_i6y6sb.jpg",
                    Status = "Active",
                    TotalRows = 4,
                    TotalColumns = 3,
                    TotalFloors = 2,
                    DefaultLayout = GenerateSeatLayout(4, 3, 2, "Volvo_NonAC"),
                    CreatedBy = "SystemSeeder",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new BusClass
                {
                    Id = BusClassVolvoAc40Id,
                    ClassName = "Volvo 40 (A/C Premium)",
                    BusType = "Volvo_AC",
                    ImageUrl = "https://res.cloudinary.com/nguyenanhtu/image/upload/v1785673076/images_czt00a.jpg",
                    Status = "Active",
                    TotalRows = 4,
                    TotalColumns = 3,
                    TotalFloors = 2,
                    DefaultLayout = GenerateSeatLayout(4, 3, 2, "Volvo_AC"),
                    CreatedBy = "SystemSeeder",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                }
            };

            foreach (var busClass in busClasses)
            {
                busClass.TotalSeats = busClass.DefaultLayout.Count;
            }

            await _context.BusClasses.InsertManyAsync(busClasses);
            Console.WriteLine(
                $"--> [THÀNH CÔNG] Seeding BusClass: Express, Luxury, Volvo Non A/C, Volvo A/C ({busClasses.Count} hạng xe).");
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
                },
                new Bus
                {
                    Id = "64f1a2b3c4d5e6f7a8b9c206", BusCode = "BUS-PT-VNAC01", LicensePlate = "51B-555.11",
                    Status = "Active",
                    BranchId = BranchSaigonId, OperatorId = OperatorPhuongTrangId, BusClassId = BusClassVolvoNonAc40Id,
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new Bus
                {
                    Id = "64f1a2b3c4d5e6f7a8b9c207", BusCode = "BUS-PT-VAC01", LicensePlate = "51B-555.22",
                    Status = "Active",
                    BranchId = BranchSaigonId, OperatorId = OperatorPhuongTrangId, BusClassId = BusClassVolvoAc40Id,
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new Bus
                {
                    Id = "64f1a2b3c4d5e6f7a8b9c208", BusCode = "BUS-HL-VNAC01", LicensePlate = "29B-666.11",
                    Status = "Active",
                    BranchId = BranchHanoiId, OperatorId = OperatorHoangLongId, BusClassId = BusClassVolvoNonAc40Id,
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new Bus
                {
                    Id = "64f1a2b3c4d5e6f7a8b9c209", BusCode = "BUS-HL-VAC01", LicensePlate = "29B-666.22",
                    Status = "Active",
                    BranchId = BranchHanoiId, OperatorId = OperatorHoangLongId, BusClassId = BusClassVolvoAc40Id,
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new Bus
                {
                    Id = "64f1a2b3c4d5e6f7a8b9c20a", BusCode = "BUS-TB-LUX02", LicensePlate = "51B-777.11",
                    Status = "Active",
                    BranchId = BranchSaigonId, OperatorId = OperatorThanhBuoiId, BusClassId = BusClassLuxury22Id,
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new Bus
                {
                    Id = "64f1a2b3c4d5e6f7a8b9c20b", BusCode = "BUS-HV-EXP03", LicensePlate = "43B-888.11",
                    Status = "Active",
                    BranchId = BranchDanangId, OperatorId = OperatorHaiVanId, BusClassId = BusClassExpress45Id,
                    CreatedBy = "SystemSeeder", CreatedAt = DateTime.UtcNow, UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new Bus
                {
                    Id = "64f1a2b3c4d5e6f7a8b9c20c", BusCode = "BUS-HV-VAC01", LicensePlate = "43B-888.22",
                    Status = "Active",
                    BranchId = BranchDanangId, OperatorId = OperatorHaiVanId, BusClassId = BusClassVolvoAc40Id,
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
                    FareConfigs = BuildStandardFareConfigs(750000m),
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
                    FareConfigs = BuildStandardFareConfigs(750000m),
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
                    FareConfigs = BuildStandardFareConfigs(450000m),
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
                    FareConfigs = BuildStandardFareConfigs(450000m),
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
                    FareConfigs = BuildStandardFareConfigs(500000m),
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
                    FareConfigs = BuildStandardFareConfigs(500000m),
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
                    FareConfigs = BuildStandardFareConfigs(150000m),
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
                    FareConfigs = BuildStandardFareConfigs(150000m),
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
                    FareConfigs = BuildStandardFareConfigs(180000m),
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
                    FareConfigs = BuildStandardFareConfigs(180000m),
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
                                PaymentMethod = random.Next(0, 4) switch
                                {
                                    0 => "Cash",
                                    1 => "PAYOS",
                                    2 => "VnPay",
                                    _ => "MOMO"
                                },
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

        private async Task UpsertPermissionsAsync()
        {
            Console.WriteLine("--> Upsert bảng Permission...");
            _allPermissionIds.Clear();

            foreach (var permission in BuildPermissionCatalog())
            {
                await _context.Permissions.ReplaceOneAsync(
                    p => p.Id == permission.Id,
                    permission,
                    new ReplaceOptions { IsUpsert = true });
                _allPermissionIds.Add(permission.Id);
            }

            Console.WriteLine($"--> [THÀNH CÔNG] Upsert {_allPermissionIds.Count} quyền hệ thống.");
        }

        private static List<Permission> BuildPermissionCatalog()
        {
            var permissions = new List<Permission>();

            void Add(string id, string name, string description, string link, string method)
            {
                permissions.Add(new Permission
                {
                    Id = id,
                    Name = name,
                    Description = description,
                    Link = link,
                    Method = method
                });
            }

            Add("64f1a2b3c4d5e6f7a8b9ca01", "View.BusRoute", "Xem danh sách và chi tiết tuyến xe", "Admin/PriceConfig", "GET");
            Add("64f1a2b3c4d5e6f7a8b9ca02", "Create.BusRoute", "Thêm tuyến xe chạy mới", "Admin/SaveTrip", "POST");
            Add("64f1a2b3c4d5e6f7a8b9ca03", "Update.BusRoute", "Cập nhật thông tin tuyến xe", "Admin/SaveTrip", "POST");
            Add("64f1a2b3c4d5e6f7a8b9ca04", "Delete.BusRoute", "Xóa tuyến xe khỏi hệ thống", "Admin/DeleteTrip", "POST");

            Add("64f1a2b3c4d5e6f7a8b9ca05", "View.Trip", "Xem lịch trình các chuyến xe chạy", "Admin/PriceConfig", "GET");
            Add("64f1a2b3c4d5e6f7a8b9ca06", "Create.Trip", "Thêm chuyến xe mới", "Admin/SaveTrip", "POST");
            Add("64f1a2b3c4d5e6f7a8b9ca07", "Update.Trip", "Thay đổi thông tin chuyến xe", "Admin/SaveTrip", "POST");
            Add("64f1a2b3c4d5e6f7a8b9ca08", "Delete.Trip", "Xóa chuyến xe khỏi hệ thống", "Admin/DeleteTrip", "POST");
            Add("64f1a2b3c4d5e6f7a8b9cc21", "Cancel.Trip", "Hủy chuyến xe (giữ lịch sử)", "Admin/CancelTrip", "POST");

            Add("64f1a2b3c4d5e6f7a8b9ca09", "View.Bus", "Xem danh sách xe và sơ đồ ghế", "Buses", "GET");
            Add("64f1a2b3c4d5e6f7a8b9ca10", "Create.Bus", "Thêm xe mới vào đội xe", "Buses/Create", "POST");
            Add("64f1a2b3c4d5e6f7a8b9ca11", "Update.Bus", "Sửa thông tin xe", "Buses/Edit", "POST");
            Add("64f1a2b3c4d5e6f7a8b9ca12", "Delete.Bus", "Xóa xe khỏi danh sách vận hành", "Buses/Delete", "POST");

            Add("64f1a2b3c4d5e6f7a8b9ca13", "View.BusClass", "Xem danh sách hạng xe", "BusClasses", "GET");
            Add("64f1a2b3c4d5e6f7a8b9ca14", "Create.BusClass", "Tạo hạng xe mới", "BusClasses/Create", "POST");
            Add("64f1a2b3c4d5e6f7a8b9ca15", "Update.BusClass", "Cập nhật hạng xe", "BusClasses/Edit", "POST");
            Add("64f1a2b3c4d5e6f7a8b9ca16", "Delete.BusClass", "Xóa hạng xe", "BusClasses/Delete", "POST");

            Add("64f1a2b3c4d5e6f7a8b9ca17", "View.Branch", "Xem danh sách chi nhánh", "Branches", "GET");
            Add("64f1a2b3c4d5e6f7a8b9ca18", "Create.Branch", "Thêm chi nhánh mới", "Branches/Create", "POST");
            Add("64f1a2b3c4d5e6f7a8b9ca19", "Update.Branch", "Cập nhật chi nhánh", "Branches/Edit", "POST");
            Add("64f1a2b3c4d5e6f7a8b9ca20", "Delete.Branch", "Xóa chi nhánh", "Branches/Delete", "POST");

            Add("64f1a2b3c4d5e6f7a8b9cc01", "View.BusOperator", "Xem danh sách nhà xe", "BusOperators", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cc02", "Create.BusOperator", "Thêm nhà xe mới", "BusOperators/Create", "POST");
            Add("64f1a2b3c4d5e6f7a8b9cc03", "Update.BusOperator", "Cập nhật nhà xe", "BusOperators/Edit", "POST");
            Add("64f1a2b3c4d5e6f7a8b9cc04", "Delete.BusOperator", "Xóa nhà xe", "BusOperators/Delete", "POST");

            Add("64f1a2b3c4d5e6f7a8b9cc05", "View.PriceConfig", "Xem bảng giá và cấu hình chuyến", "Admin/PriceConfig", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cc06", "Create.PriceConfig", "Thêm cấu hình giá", "Admin/SavePriceConfig", "POST");
            Add("64f1a2b3c4d5e6f7a8b9cc07", "Update.PriceConfig", "Cập nhật cấu hình giá", "Admin/SavePriceConfig", "POST");
            Add("64f1a2b3c4d5e6f7a8b9cc08", "Delete.PriceConfig", "Xóa cấu hình giá", "Admin/DeletePriceConfig", "POST");

            Add("64f1a2b3c4d5e6f7a8b9ca21", "View.Booking", "Xem danh sách đặt vé", "Booking", "GET");
            Add("64f1a2b3c4d5e6f7a8b9ca22", "Create.Booking", "Đặt vé mới cho khách hàng", "Booking/BookTicket", "POST");
            Add("64f1a2b3c4d5e6f7a8b9ca23", "Update.Booking", "Thay đổi thông tin vé", "Booking/BookTicket", "POST");
            Add("64f1a2b3c4d5e6f7a8b9ca24", "Delete.Booking", "Hủy vé trực tiếp (legacy)", "Booking/CancelBooking", "POST");
            Add("64f1a2b3c4d5e6f7a8b9cc09", "RequestCancel.Booking", "Tạo yêu cầu hủy vé", "Booking/RequestCancel", "POST");
            Add("64f1a2b3c4d5e6f7a8b9cc10", "ApproveCancel.Booking", "Duyệt yêu cầu hủy vé", "Booking/ApproveCancel", "POST");
            Add("64f1a2b3c4d5e6f7a8b9cc11", "RejectCancel.Booking", "Từ chối yêu cầu hủy vé", "Booking/RejectCancel", "POST");

            Add("64f1a2b3c4d5e6f7a8b9ca25", "View.Customer", "Xem thông tin khách hàng", "Booking/GetCustomerByPhone", "GET");
            Add("64f1a2b3c4d5e6f7a8b9ca26", "Create.Customer", "Tạo khách hàng khi đặt vé", "Booking/BookTicket", "POST");
            Add("64f1a2b3c4d5e6f7a8b9ca27", "Update.Customer", "Cập nhật khách hàng khi đặt vé", "Booking/BookTicket", "POST");
            Add("64f1a2b3c4d5e6f7a8b9ca28", "Delete.Customer", "Khóa/Xóa khách hàng", "Customer/Delete", "POST");

            Add("64f1a2b3c4d5e6f7a8b9cc19", "View.Manifest", "Xem danh sách hành khách", "Booking/GetManifest", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cc20", "Export.Manifest", "Xuất danh sách hành khách", "Booking/GetManifest", "GET");

            Add("64f1a2b3c4d5e6f7a8b9cc12", "View.RefundRequest", "Xem yêu cầu hoàn tiền", "Booking/RefundList", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cc13", "Create.RefundRequest", "Tạo yêu cầu hoàn tiền", "Booking/RequestCancel", "POST");
            Add("64f1a2b3c4d5e6f7a8b9cc14", "Approve.RefundRequest", "Duyệt yêu cầu hoàn tiền", "Booking/ApproveRefund", "POST");
            Add("64f1a2b3c4d5e6f7a8b9cc15", "Reject.RefundRequest", "Từ chối yêu cầu hoàn tiền", "Booking/RejectRefund", "POST");
            Add("64f1a2b3c4d5e6f7a8b9cc16", "Process.Refund", "Xử lý chuyển khoản hoàn tiền", "Booking/ConfirmRefund", "POST");
            Add("64f1a2b3c4d5e6f7a8b9cc17", "Complete.Refund", "Hoàn tất hoàn tiền", "Booking/ConfirmRefund", "POST");
            Add("64f1a2b3c4d5e6f7a8b9cc18", "View.OwnRefundRequest", "Xem yêu cầu hoàn do mình tạo", "Booking/RefundList", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cc30", "Reject.Refund", "Từ chối hoàn tiền", "Booking/RejectRefund", "POST");

            Add("64f1a2b3c4d5e6f7a8b9ca29", "View.User", "Xem danh sách nhân viên", "Admin/Users", "GET");
            Add("64f1a2b3c4d5e6f7a8b9ca30", "Create.User", "Tạo tài khoản nhân viên", "Admin/Users/Create", "POST");
            Add("64f1a2b3c4d5e6f7a8b9ca31", "Update.User", "Cập nhật nhân viên", "Admin/Users/Edit", "POST");
            Add("64f1a2b3c4d5e6f7a8b9ca32", "Delete.User", "Xóa/Vô hiệu hóa nhân viên", "Admin/Users/Delete", "POST");

            Add("64f1a2b3c4d5e6f7a8b9ca33", "View.Role", "Xem danh sách vai trò", "DynamicRoles", "GET");
            Add("64f1a2b3c4d5e6f7a8b9ca34", "Create.Role", "Tạo vai trò mới", "DynamicRoles/Create", "POST");
            Add("64f1a2b3c4d5e6f7a8b9ca35", "Update.Role", "Cập nhật vai trò", "DynamicRoles/Edit", "POST");
            Add("64f1a2b3c4d5e6f7a8b9ca36", "Delete.Role", "Xóa vai trò", "DynamicRoles/Delete", "POST");
            Add("64f1a2b3c4d5e6f7a8b9ca37", "View.Permission", "Xem danh sách quyền", "Permissions", "GET");

            Add("64f1a2b3c4d5e6f7a8b9cb01", "View.Dashboard", "Xem trang tổng quan Dashboard", "Dashboard", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cb02", "View.RouteRevenue", "Xem báo cáo doanh thu theo tuyến", "Dashboard/RouteRevenuePartial", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cb03", "Export.RouteRevenue", "Xuất Excel doanh thu theo tuyến", "Dashboard/ExportRouteRevenue", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cb04", "View.OperatorRevenue", "Xem báo cáo doanh thu nhà xe", "Dashboard/OperatorRevenuePartial", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cb05", "Export.OperatorRevenue", "Xuất Excel doanh thu nhà xe", "Dashboard/ExportOperatorRevenue", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cb06", "View.BranchCancellation", "Xem báo cáo tỷ lệ hủy chuyến", "Dashboard/BranchCancellationPartial", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cb07", "Export.BranchCancellation", "Xuất Excel tỷ lệ hủy chuyến", "Dashboard/ExportBranchCancellation", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cb08", "View.SeatAnalytics", "Xem báo cáo hiệu suất lấp đầy ghế", "Dashboard/SeatAnalyticsPartial", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cb09", "Export.SeatAnalytics", "Xuất Excel hiệu suất lấp đầy ghế", "Dashboard/ExportSeatAnalytics", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cb10", "View.TicketStatusStatistics", "Xem báo cáo vé thành công và vé hủy", "Dashboard/TicketStatusStatisticsPartial", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cb11", "Export.TicketStatusStatistics", "Xuất Excel báo cáo vé", "Dashboard/ExportTicketStatusStatistics", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cc22", "View.TotalRevenue", "Xem báo cáo tổng doanh thu", "Dashboard/TotalRevenuePartial", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cc23", "Export.TotalRevenue", "Xuất Excel tổng doanh thu", "Dashboard/ExportTotalRevenue", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cc24", "View.SoldOutStats", "Xem báo cáo cháy ghế", "Dashboard/SoldOutStatsPartial", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cc25", "Export.SoldOutStats", "Xuất Excel cháy ghế", "Dashboard/ExportSoldOutStats", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cc26", "View.VehicleRevenue", "Xem báo cáo doanh thu theo xe", "Dashboard/VehicleRevenueStatisticsPartial", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cc27", "Export.VehicleRevenue", "Xuất Excel doanh thu theo xe", "Dashboard/ExportVehicleRevenueStatistics", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cc28", "View.LowOccupancyTrips", "Xem báo cáo chuyến thiếu khách", "Dashboard/LowOccupancyTripsPartial", "GET");
            Add("64f1a2b3c4d5e6f7a8b9cc29", "Export.LowOccupancyTrips", "Xuất Excel chuyến thiếu khách", "Dashboard/ExportLowOccupancyTrips", "GET");

            return permissions;
        }

        private async Task UpsertDynamicRolesAsync()
        {
            Console.WriteLine("--> Upsert 5 dynamic role nghiệp vụ...");
            var permissions = await _context.Permissions.Find(_ => true).ToListAsync();
            var permissionMap = permissions
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

            string P(string name) => permissionMap[name];
            List<string> Ps(params string[] names) => names.Select(P).Distinct().ToList();

            var now = DateTime.UtcNow;
            var roles = new List<DynamicRole>
            {
                new DynamicRole
                {
                    Id = RoleSuperAdminId,
                    RoleName = "SuperAdmin",
                    PermissionIds = permissions.Select(p => p.Id).Distinct().ToList(),
                    CreatedBy = "SystemSeeder",
                    CreatedAt = now,
                    UpdatedBy = "SystemSeeder",
                    UpdatedAt = now
                },
                new DynamicRole
                {
                    Id = RoleTicketAgentId,
                    RoleName = "TicketAgent",
                    PermissionIds = Ps(
                        "View.BusRoute", "View.Trip", "View.Bus", "View.BusClass", "View.PriceConfig",
                        "View.Booking", "Create.Booking", "Update.Booking",
                        "View.Customer", "Create.Customer", "Update.Customer",
                        "View.Manifest", "Export.Manifest",
                        "Create.RefundRequest", "View.OwnRefundRequest", "RequestCancel.Booking"),
                    CreatedBy = "SystemSeeder",
                    CreatedAt = now,
                    UpdatedBy = "SystemSeeder",
                    UpdatedAt = now
                },
                new DynamicRole
                {
                    Id = RoleOperationsStaffId,
                    RoleName = "OperationsStaff",
                    PermissionIds = Ps(
                        "View.BusRoute", "Create.BusRoute", "Update.BusRoute", "Delete.BusRoute",
                        "View.Trip", "Create.Trip", "Update.Trip", "Cancel.Trip",
                        "View.Bus", "Create.Bus", "Update.Bus", "Delete.Bus",
                        "View.BusClass", "Create.BusClass", "Update.BusClass", "Delete.BusClass",
                        "View.Branch", "Create.Branch", "Update.Branch",
                        "View.BusOperator", "Create.BusOperator", "Update.BusOperator", "Delete.BusOperator",
                        "View.PriceConfig", "Create.PriceConfig", "Update.PriceConfig", "Delete.PriceConfig",
                        "View.Booking", "View.Customer", "View.Manifest", "Export.Manifest",
                        "View.SeatAnalytics", "Export.SeatAnalytics",
                        "View.BranchCancellation", "Export.BranchCancellation",
                        "View.VehicleRevenue", "Export.VehicleRevenue",
                        "View.LowOccupancyTrips", "Export.LowOccupancyTrips",
                        "View.SoldOutStats", "Export.SoldOutStats"),
                    CreatedBy = "SystemSeeder",
                    CreatedAt = now,
                    UpdatedBy = "SystemSeeder",
                    UpdatedAt = now
                },
                new DynamicRole
                {
                    Id = RoleAccountantId,
                    RoleName = "Accountant",
                    PermissionIds = Ps(
                        "View.Booking", "View.Customer",
                        "View.RefundRequest", "Process.Refund", "Complete.Refund", "Reject.Refund",
                        "View.PriceConfig",
                        "View.TotalRevenue", "Export.TotalRevenue",
                        "View.RouteRevenue", "Export.RouteRevenue",
                        "View.OperatorRevenue", "Export.OperatorRevenue",
                        "View.TicketStatusStatistics", "Export.TicketStatusStatistics"),
                    CreatedBy = "SystemSeeder",
                    CreatedAt = now,
                    UpdatedBy = "SystemSeeder",
                    UpdatedAt = now
                },
                new DynamicRole
                {
                    Id = RoleBranchManagerId,
                    RoleName = "BranchManager",
                    PermissionIds = Ps(
                        "View.Dashboard",
                        "View.BusRoute", "View.Trip", "View.Bus", "View.Branch",
                        "View.Booking", "View.Customer", "View.Manifest",
                        "View.RefundRequest", "Approve.RefundRequest", "Reject.RefundRequest",
                        "View.User",
                        "View.TotalRevenue", "Export.TotalRevenue",
                        "View.RouteRevenue", "Export.RouteRevenue",
                        "View.OperatorRevenue", "Export.OperatorRevenue",
                        "View.BranchCancellation", "Export.BranchCancellation",
                        "View.SeatAnalytics", "Export.SeatAnalytics",
                        "View.TicketStatusStatistics", "Export.TicketStatusStatistics",
                        "View.VehicleRevenue", "Export.VehicleRevenue",
                        "View.LowOccupancyTrips", "Export.LowOccupancyTrips"),
                    CreatedBy = "SystemSeeder",
                    CreatedAt = now,
                    UpdatedBy = "SystemSeeder",
                    UpdatedAt = now
                }
            };

            foreach (var role in roles)
            {
                await _context.DynamicRoles.ReplaceOneAsync(
                    r => r.RoleName == role.RoleName,
                    role,
                    new ReplaceOptions { IsUpsert = true });
            }

            Console.WriteLine($"--> [THÀNH CÔNG] Upsert {roles.Count} dynamic role.");
        }

        // --- HÀM BULK ĐÃ FIX LỖI COMPILE ---
        // --- HÀM BULK PHỦ KÍN 100% CHẶNG × NGÀY TRONG 1 THÁNG ---
        public async Task SeedBulkTripsAndBookings()
        {
            // Làm sạch dữ liệu nghiệp vụ để dashboard luôn có dataset mới, nhiều và đồng bộ.
            await _context.Customers.DeleteManyAsync(new BsonDocument());
            await _context.Trips.DeleteManyAsync(new BsonDocument());
            await _context.Bookings.DeleteManyAsync(new BsonDocument());

            var buses = await _context.Buses.Find(new BsonDocument()).ToListAsync();
            var routes = await _context.BusRoutes.Find(new BsonDocument()).ToListAsync();
            var busClasses = await _context.BusClasses.Find(new BsonDocument()).ToListAsync();

            if (!buses.Any() || !routes.Any() || !busClasses.Any())
            {
                Console.WriteLine("--> [LỖI] Cần chạy seed Bus, BusClass và BusRoute trước!");
                return;
            }

            var random = new Random(20260714);

            // 1. Seed 500 khách hàng với tên Việt Nam đa dạng.
            var customers = new List<Customer>();
            string[] firstNames = { "Nguyễn", "Trần", "Lê", "Phạm", "Vũ", "Đặng", "Hoàng", "Bùi", "Đỗ", "Hồ", "Ngô", "Dương", "Mai", "Tô" };
            string[] middleNames = { "Văn", "Thị", "Minh", "Hoàng", "Ngọc", "Tuấn", "Anh", "Đức", "Khánh", "Thúy", "Gia", "Thanh", "Quang" };
            string[] lastNames = { "An", "Bình", "Châu", "Dũng", "Hạnh", "Linh", "Nam", "Phúc", "Trang", "Yến", "Phát", "Tài", "Hưng", "Vy", "Quân" };
            string[] ranks = { "Standard", "Silver", "Gold", "Platinum" };
            string[] genders = { "Male", "Female" };

            for (int i = 1; i <= 500; i++)
            {
                var rank = ranks[random.Next(ranks.Length)];
                var fullName = $"{firstNames[random.Next(firstNames.Length)]} {middleNames[random.Next(middleNames.Length)]} {lastNames[random.Next(lastNames.Length)]}";
                var isBlocked = i % 85 == 0;

                customers.Add(new Customer
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    CustomerCode = $"KH-{1000 + i:D4}",
                    FullName = fullName,
                    Dob = new DateTime(random.Next(1975, 2006), random.Next(1, 13), random.Next(1, 28), 0, 0, 0, DateTimeKind.Utc),
                    Gender = genders[random.Next(genders.Length)],
                    PhoneNumber = $"09{random.Next(10000000, 99999999)}",
                    Email = $"customer.{1000 + i}@srcmail.vn",
                    MembershipRank = rank,
                    TotalPoints = rank == "Standard" ? random.Next(0, 100) :
                                  rank == "Silver" ? random.Next(101, 500) :
                                  rank == "Gold" ? random.Next(501, 1500) : random.Next(1501, 5000),
                    IsBlocked = isBlocked,
                    ConsecutiveUnpaidCount = isBlocked ? 10 : 0,
                    BlockReason = isBlocked ? "Hủy vé hoặc không thanh toán nhiều lần" : null,
                    Status = isBlocked ? "Blocked" : "Active",
                    CreatedBy = "SystemSeeder",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await _context.Customers.InsertManyAsync(customers);
            var activeCustomers = customers.Where(c => c.Status == "Active").ToList();

            // 2. Seed chuyến từ 29/06 -> 01/09, nhiều khung giờ để chart nhìn thật.
            var generatedTrips = new List<Trip>();
            var generatedBookings = new List<Booking>();

            var dateStart = new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc);
            var dateEnd = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
            int[] tripHours = { 6, 8, 12, 16, 19, 22 };
            var tripCounter = 1;
            var bookingCounter = 1;

            foreach (var route in routes)
            {
                var suitableBuses = buses.Where(b => b.Status == "Active").ToList();
                if (!suitableBuses.Any()) continue;

                for (var currentDay = dateStart; currentDay <= dateEnd; currentDay = currentDay.AddDays(1))
                {
                    var isWeekend = currentDay.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday;

                    foreach (var hour in tripHours)
                    {
                        var bus = suitableBuses[random.Next(suitableBuses.Count)];
                        var busClass = busClasses.FirstOrDefault(bc => bc.Id == bus.BusClassId);
                        if (busClass == null || busClass.DefaultLayout == null || !busClass.DefaultLayout.Any()) continue;

                        var departureTime = new DateTime(currentDay.Year, currentDay.Month, currentDay.Day, hour, 0, 0, DateTimeKind.Utc);
                        var fareConfig = route.FareConfigs.FirstOrDefault(f => f.BusType == busClass.BusType) ?? route.FareConfigs.FirstOrDefault();
                        var baseFare = fareConfig?.FlatPrice ?? 250000m;
                        var durationHours = Math.Max(2, route.DistanceKm / 60.0);

                        // Mỗi nhà xe có tỷ lệ hủy vận hành khác nhau để biểu đồ hủy chuyến có số liệu đẹp.
                        var operatorCancelRate = bus.OperatorId switch
                        {
                            var id when id == OperatorPhuongTrangId => 5,
                            var id when id == OperatorThanhBuoiId => 7,
                            var id when id == OperatorHoangLongId => 11,
                            var id when id == OperatorHaiVanId => 8,
                            _ => 8
                        };

                        var isCancelledByOperator = random.Next(1, 101) <= operatorCancelRate;
                        var tripStatus = isCancelledByOperator
                            ? "Cancelled"
                            : departureTime < DateTime.UtcNow ? "Completed" : "Scheduled";

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
                            RealtimeSeats = busClass.DefaultLayout.Select(s => new RealtimeSeat
                            {
                                SeatNumber = s.SeatNumber,
                                Status = "Available"
                            }).ToList(),
                            CreatedBy = "SystemSeeder",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedBy = "SystemSeeder",
                            UpdatedAt = DateTime.UtcNow
                        };

                        generatedTrips.Add(trip);
                        tripCounter++;

                        if (isCancelledByOperator)
                        {
                            continue;
                        }

                        var totalSeats = trip.RealtimeSeats.Count;
                        var isPeakHour = hour is 8 or 19;
                        var isLateHour = hour == 22;

                        int minPercent;
                        int maxPercent;

                        if (isPeakHour && isWeekend)
                        {
                            minPercent = 80; maxPercent = 100;
                        }
                        else if (isPeakHour)
                        {
                            minPercent = 65; maxPercent = 95;
                        }
                        else if (isLateHour)
                        {
                            minPercent = 8; maxPercent = 45;
                        }
                        else if (isWeekend)
                        {
                            minPercent = 45; maxPercent = 80;
                        }
                        else
                        {
                            minPercent = 20; maxPercent = 70;
                        }

                        // Một phần nhỏ chuyến rất vắng để test cảnh báo/lấp đầy thấp.
                        if (random.Next(1, 101) <= 18)
                        {
                            minPercent = 5; maxPercent = 28;
                        }

                        // Một phần nhỏ chuyến sold-out để test khung giờ cháy vé.
                        if (random.Next(1, 101) <= 10)
                        {
                            minPercent = 100; maxPercent = 100;
                        }

                        var seatsToFill = (int)Math.Round(totalSeats * (random.Next(minPercent, maxPercent + 1) / 100.0));
                        seatsToFill = Math.Clamp(seatsToFill, 0, totalSeats);

                        var filledCounter = 0;
                        while (filledCounter < seatsToFill)
                        {
                            var availableSeats = trip.RealtimeSeats
                                .Where(s => s.Status == "Available")
                                .ToList();

                            if (!availableSeats.Any()) break;

                            var partySize = Math.Min(random.Next(1, 5), Math.Min(availableSeats.Count, seatsToFill - filledCounter));
                            var selectedSeats = availableSeats
                                .OrderBy(_ => random.Next())
                                .Take(partySize)
                                .ToList();

                            foreach (var seat in selectedSeats)
                            {
                                seat.Status = "Booked";
                            }

                            var buyer = activeCustomers[random.Next(activeCustomers.Count)];
                            var passengers = selectedSeats.Select(seat => new PassengerDetail
                            {
                                SeatNumber = seat.SeatNumber,
                                Name = buyer.FullName,
                                PhoneNumber = buyer.PhoneNumber,
                                Email = buyer.Email,
                                Dob = buyer.Dob,
                                FinalSeatPrice = baseFare
                            }).ToList();

                            var totalPrice = baseFare * partySize;
                            var taxAmount = totalPrice * 0.1m;
                            var discountAmount = random.Next(1, 101) <= 18 ? random.Next(1, 5) * 10000m : 0m;
                            var finalAmount = totalPrice + taxAmount - discountAmount;

                            var bookingStatus = "Completed";
                            var paymentStatus = "Paid";
                            CancellationInfo cancellationInfo = null;
                            PaymentInfo paymentInfo = new PaymentInfo
                            {
                                PaymentMethod = random.Next(0, 4) switch
                                {
                                    0 => "Cash",
                                    1 => "PAYOS",
                                    2 => "VnPay",
                                    _ => "MOMO"
                                },
                                AmountPaid = finalAmount,
                                TransactionCode = $"TXN-{departureTime:yyyyMMdd}-{bookingCounter:D6}"
                            };

                            // Hủy vé cá nhân, khác với hủy chuyến vận hành.
                            if (departureTime < DateTime.UtcNow && random.Next(1, 101) <= 10)
                            {
                                bookingStatus = "Canceled";
                                paymentStatus = "Refunded";
                                var penalty = 10m;
                                cancellationInfo = new CancellationInfo
                                {
                                    CanceledAt = departureTime.AddHours(-random.Next(4, 72)),
                                    Reason = "Khách đổi lịch trình cá nhân",
                                    PenaltyPercentage = penalty,
                                    RefundAmount = finalAmount * (1 - penalty / 100m)
                                };

                                paymentInfo.AmountPaid = 0;

                                foreach (var seat in selectedSeats)
                                {
                                    seat.Status = "Available";
                                }
                            }

                            generatedBookings.Add(new Booking
                            {
                                Id = ObjectId.GenerateNewId().ToString(),
                                BookingCode = $"BKG-{departureTime:yyyyMMdd}-{bookingCounter:D6}",
                                CustomerId = buyer.Id,
                                CustomerPhone = buyer.PhoneNumber,
                                CustomerEmail = buyer.Email,
                                TripId = trip.Id,
                                UserId = "64f1a2b3c4d5e6f7a8b9c999",
                                BranchId = trip.BranchId ?? BranchHanoiId,
                                BookingTime = departureTime.AddDays(-random.Next(1, 14)).AddHours(-random.Next(0, 12)),
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
                            });

                            bookingCounter++;
                            filledCounter += partySize;
                        }
                    }
                }
            }

            if (generatedTrips.Any())
            {
                await _context.Trips.InsertManyAsync(generatedTrips);
            }

            if (generatedBookings.Any())
            {
                await _context.Bookings.InsertManyAsync(generatedBookings);
            }

            Console.WriteLine("--- SEEDING DASHBOARD DATA HOÀN TẤT ---");
            Console.WriteLine($"* Khách hàng: {customers.Count} Customers");
            Console.WriteLine($"* Chuyến xe vận hành: {generatedTrips.Count} Trips");
            Console.WriteLine($"* Booking sinh ra: {generatedBookings.Count} Bookings");
            Console.WriteLine($"* Chuyến bị hủy vận hành: {generatedTrips.Count(t => t.Status == "Cancelled")} Trips");
            Console.WriteLine($"* Chuyến sold-out: {generatedTrips.Count(t => t.RealtimeSeats.Any() && t.RealtimeSeats.All(s => s.Status == "Booked"))} Trips");
        }

        private static readonly string[] DashboardPaymentMethods = { "Cash", "PAYOS", "VnPay", "MOMO" };

        private async Task BackfillPaymentMethodsAsync()
        {
            var bookings = await _context.Bookings
                .Find(b => b.Payment != null
                           && b.PaymentStatus == "Paid"
                           && (b.BookingStatus == "Completed"
                               || b.BookingStatus == "Complete"
                               || b.BookingStatus == "Confirmed"))
                .ToListAsync();

            if (!bookings.Any())
            {
                Console.WriteLine("--> [BỎ QUA] Backfill payment methods: không có booking hợp lệ.");
                return;
            }

            var updates = new List<WriteModel<Booking>>();

            foreach (var booking in bookings)
            {
                var resolved = PaymentMethodDisplayHelper.ResolveRawMethod(booking.Payment);
                var displayName = PaymentMethodDisplayHelper.GetDisplayName(resolved);

                if (displayName == PaymentMethodDisplayHelper.VnpayPayment
                    || displayName == PaymentMethodDisplayHelper.MomoPayment)
                    continue;

                var index = Math.Abs(StringComparer.Ordinal.GetHashCode(booking.Id ?? string.Empty))
                    % DashboardPaymentMethods.Length;
                var targetMethod = DashboardPaymentMethods[index];

                if (string.Equals(booking.Payment!.PaymentMethod, targetMethod, StringComparison.OrdinalIgnoreCase))
                    continue;

                updates.Add(new UpdateOneModel<Booking>(
                    Builders<Booking>.Filter.Eq(b => b.Id, booking.Id),
                    Builders<Booking>.Update.Set(b => b.Payment!.PaymentMethod, targetMethod)));
            }

            if (updates.Any())
            {
                await _context.Bookings.BulkWriteAsync(updates);
                Console.WriteLine($"--> [THÀNH CÔNG] Backfill payment methods cho {updates.Count} booking.");
            }
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