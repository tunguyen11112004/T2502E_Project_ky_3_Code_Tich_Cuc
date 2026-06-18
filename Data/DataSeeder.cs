using System;
using System.Collections.Generic;
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
        private readonly IPasswordHasher<User> _passwordHasher;

        // Định nghĩa các ID cố định để các bảng sau bám vào làm Khóa Ngoại (Foreign Key)
        //Branc
        public static readonly string BranchHanoiId = "64f1a2b3c4d5e6f7a8b9c001";
        public static readonly string BranchDanangId = "64f1a2b3c4d5e6f7a8b9c002";
        public static readonly string BranchSaigonId = "64f1a2b3c4d5e6f7a8b9c003";

        //BusClass
        public static readonly string BusClassExpress45Id = "64f1a2b3c4d5e6f7a8b9c011";
        public static readonly string BusClassLimousine22Id = "64f1a2b3c4d5e6f7a8b9c012";

        //Bus
        public static readonly string BusHNExpressId = "64f1a2b3c4d5e6f7a8b9c021";
        public static readonly string BusHNLimousineId = "64f1a2b3c4d5e6f7a8b9c022";
        public static readonly string BusSGExpressId = "64f1a2b3c4d5e6f7a8b9c023";
        public static readonly string BusSGLimousineId = "64f1a2b3c4d5e6f7a8b9c024";

        // Định nghĩa sẵn ID cố định cho các Tuyến đường (Route)
        public static readonly string RouteHanoiSaigonId = "64f1a2b3c4d5e6f7a8b9c031";
        public static readonly string RouteSaigonHanoiId = "64f1a2b3c4d5e6f7a8b9c032";

        // Định nghĩa sẵn ID cố định cho các Chuyến xe (Trip)
        public static readonly string TripHanoiSaigonExpressId = "64f1a2b3c4d5e6f7a8b9c041";
        public static readonly string TripHanoiSaigonLimoId = "64f1a2b3c4d5e6f7a8b9c042";

        // Định nghĩa sẵn ID cố định cho Khách hàng và Đơn đặt vé
        public static readonly string CustomerNguyenVanAId = "64f1a2b3c4d5e6f7a8b9c051";
        public static readonly string BookingLimoId = "64f1a2b3c4d5e6f7a8b9c061";

        // Thêm ID cố định cho Role Admin vào đầu lớp DataSeeder
        public static readonly string RoleAdminId = "64f1a2b3c4d5e6f7a8b9c099";

        // Lưu tạm danh sách ID permission đã sinh để nạp tự động cho Role Admin
        private static List<string> _allPermissionIds = new List<string>();

        public DataSeeder(ApplicationDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
        }

        public async Task SeedAllAsync()
        {
            Console.WriteLine("--> Bắt đầu seeding toàn bộ dữ liệu...");

            await SeedPermissions();
            await SeedDynamicRoles();
            await SeedUsers();
            await SeedBranches();
            await SeedBusClasses();
            await SeedSystemConfigs();
            await SeedBusesAndRoutes();
            await SeedTrips();
            await SeedBookings();
            Console.WriteLine("--> Hoàn tất khởi tạo dữ liệu hệ thống!");
        }

        private async Task SeedUsers()
        {
            if (await _context.Users.CountDocumentsAsync(_ => true) > 0) return;
            var admin = new User { Username = "admin@src.com", FullName = "System Admin" };
            admin.PasswordHash = _passwordHasher.HashPassword(admin, "Admin@123");
            await _context.Users.InsertOneAsync(admin);
        }

        // 1. SEED BRANCHES
        public async Task SeedBranches()
        {
            Console.WriteLine("--> Kiểm tra và seeding dữ liệu bảng Chi nhánh (Branch)...");

            // Kiểm tra xem Collection đã có dữ liệu chưa (Hãy chắc chắn _context.Branches đã được định nghĩa trong ApplicationDbContext)
            var count = await _context.Branches.CountDocumentsAsync(new BsonDocument());
            if (count > 0)
            {
                Console.WriteLine("--> Bảng Branch đã có dữ liệu. Bỏ qua seeding.");
                return;
            }

            var branches = new List<Branch>
            {
                new Branch
                {
                    Id = BranchHanoiId,
                    BranchCode = "CN-HN-01",
                    BranchName = "Văn phòng Hà Nội (Bến xe Mỹ Đình)",
                    Address = "Số 20 Phạm Hùng, Mỹ Đình, Từ Liêm, Hà Nội",
                    PhoneNumber = "02437685555",
                    Status = "Active",
                    CreatedBy = "SystemSeeder",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new Branch
                {
                    Id = BranchDanangId,
                    BranchCode = "CN-DN-02",
                    BranchName = "Văn phòng Đà Nẵng (Bến xe Trung tâm)",
                    Address = "185 Tôn Đức Thắng, Hòa Minh, Liên Chiểu, Đà Nẵng",
                    PhoneNumber = "02363767676",
                    Status = "Active",
                    CreatedBy = "SystemSeeder",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                new Branch
                {
                    Id = BranchSaigonId,
                    BranchCode = "CN-HCM-03",
                    BranchName = "Văn phòng TP. Hồ Chí Minh (Bến xe Miền Đông)",
                    Address = "292 Đinh Bộ Lĩnh, Phường 26, Bình Thạnh, TP. HCM",
                    PhoneNumber = "02838991607",
                    Status = "Active",
                    CreatedBy = "SystemSeeder",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                }
            };

            await _context.Branches.InsertManyAsync(branches);
            Console.WriteLine($"--> Đã seeding thành công {branches.Count} chi nhánh hệ thống!");
        }

        // 2. SEED BUS CLASSES
        public async Task SeedBusClasses()
        {
            Console.WriteLine("--> Kiểm tra và seeding dữ liệu bảng Hạng Xe (BusClass)...");

            var count = await _context.BusClasses.CountDocumentsAsync(new BsonDocument());
            if (count > 0)
            {
                Console.WriteLine("--> Bảng BusClass đã có dữ liệu. Bỏ qua seeding.");
                return;
            }

            var busClasses = new List<BusClass>
            {
                new BusClass
                {
                    Id = BusClassExpress45Id,
                    ClassName = "Express Seat 45",
                    BusType = "Express_Seat",
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
            Console.WriteLine($"--> Đã seeding thành công {busClasses.Count} hạng xe kèm sơ đồ ghế tự động!");
        }

        // Hàm bổ trợ tự động sinh danh sách sơ đồ ghế (Layout) bằng vòng lặp
        private List<SeatTemplate> GenerateSeatLayout(int totalRows, int totalColumns, int totalFloors, string busType)
        {
            var layout = new List<SeatTemplate>();

            for (int floor = 1; floor <= totalFloors; floor++)
            {
                string floorPrefix = totalFloors > 1 ? (floor == 1 ? "A" : "B") : "";
                int seatCounter = 1;

                for (int row = 1; row <= totalRows; row++)
                {
                    for (int col = 1; col <= totalColumns; col++)
                    {
                        string seatNumber = totalFloors > 1 ? $"{floorPrefix}{seatCounter:D2}" : $"{seatCounter:D2}";
                        string seatType = busType == "Luxury_Sleeper" ? "Sleeper" : (row <= 2 ? "VIP" : "Standard");

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

        // 3. SEED BUSES AND ROUTES
        public async Task SeedBusesAndRoutes()
        {
            Console.WriteLine("--> Kiểm tra và seeding dữ liệu Xe (Bus) và Tuyến đường (Route)...");

            // PHẦN 1: SEEDING XE (BUS)
            var busCount = await _context.Buses.CountDocumentsAsync(new BsonDocument());
            if (busCount == 0)
            {
                var buses = new List<Bus>
                {
                    new Bus
                    {
                        Id = BusHNExpressId,
                        BusCode = "BUS-HN-EXP01",
                        LicensePlate = "29B-555.11",
                        Status = "Active",
                        BranchId = BranchHanoiId,
                        BusClassId = BusClassExpress45Id,
                        CreatedBy = "SystemSeeder",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedBy = "SystemSeeder",
                        UpdatedAt = DateTime.UtcNow
                    },
                    new Bus
                    {
                        Id = BusHNLimousineId,
                        BusCode = "BUS-HN-LIMO02",
                        LicensePlate = "29B-999.22",
                        Status = "Active",
                        BranchId = BranchHanoiId,
                        BusClassId = BusClassLimousine22Id,
                        CreatedBy = "SystemSeeder",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedBy = "SystemSeeder",
                        UpdatedAt = DateTime.UtcNow
                    },
                    new Bus
                    {
                        Id = BusSGExpressId,
                        BusCode = "BUS-SG-EXP03",
                        LicensePlate = "51B-111.33",
                        Status = "Active",
                        BranchId = BranchSaigonId,
                        BusClassId = BusClassExpress45Id,
                        CreatedBy = "SystemSeeder",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedBy = "SystemSeeder",
                        UpdatedAt = DateTime.UtcNow
                    },
                    new Bus
                    {
                        Id = BusSGLimousineId,
                        BusCode = "BUS-SG-LIMO04",
                        LicensePlate = "51B-888.44",
                        Status = "Active",
                        BranchId = BranchSaigonId,
                        BusClassId = BusClassLimousine22Id,
                        CreatedBy = "SystemSeeder",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedBy = "SystemSeeder",
                        UpdatedAt = DateTime.UtcNow
                    }
                };

                await _context.Buses.InsertManyAsync(buses);
                Console.WriteLine($"--> Đã seeding thành công {buses.Count} chiếc xe cụ thể vào hệ thống!");
            }
            else
            {
                Console.WriteLine("--> Bảng Bus đã có dữ liệu. Bỏ qua seeding xe.");
            }

            // PHẦN 2: SEEDING TUYẾN ĐƯỜNG (ROUTE)
            var routeCount =
                await _context.BusRoutes
                    .CountDocumentsAsync(
                        new BsonDocument()); // Hãy chắc chắn _context.BusRoutes khớp với tên DbSet/Property trong ApplicationDbContext của bạn
            if (routeCount == 0)
            {
                var routes = new List<BusRoute>
                {
                    // Tuyến đi: Hà Nội -> TP. Hồ Chí Minh
                    new BusRoute
                    {
                        Id = RouteHanoiSaigonId,
                        DeparturePoint = "Hà Nội",
                        DestinationPoint = "TP. Hồ Chí Minh",
                        DistanceKm = 1720,
                        Stations = new List<Station>
                        {
                            new Station { StationName = "Bến xe Mỹ Đình (Hà Nội)", StopOrder = 1 },
                            new Station { StationName = "Văn phòng Thanh Hóa", StopOrder = 2 },
                            new Station { StationName = "Bến xe Trung tâm Đà Nẵng", StopOrder = 3 },
                            new Station { StationName = "Bến xe Miền Đông (TP. HCM)", StopOrder = 4 }
                        },
                        FareConfigs = new List<FareConfig>
                        {
                            new FareConfig { BusType = "Express_Seat", FlatPrice = 750000m, VatPercentage = 10m },
                            new FareConfig { BusType = "Luxury_Sleeper", FlatPrice = 1100000m, VatPercentage = 10m }
                        },
                        CreatedBy = "SystemSeeder",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedBy = "SystemSeeder",
                        UpdatedAt = DateTime.UtcNow
                    },

                    // Tuyến về: TP. Hồ Chí Minh -> Hà Nội
                    new BusRoute
                    {
                        Id = RouteSaigonHanoiId,
                        DeparturePoint = "TP. Hồ Chí Minh",
                        DestinationPoint = "Hà Nội",
                        DistanceKm = 1720,
                        Stations = new List<Station>
                        {
                            new Station { StationName = "Bến xe Miền Đông (TP. HCM)", StopOrder = 1 },
                            new Station { StationName = "Bến xe Trung tâm Đà Nẵng", StopOrder = 2 },
                            new Station { StationName = "Văn phòng Thanh Hóa", StopOrder = 3 },
                            new Station { StationName = "Bến xe Mỹ Đình (Hà Nội)", StopOrder = 4 }
                        },
                        FareConfigs = new List<FareConfig>
                        {
                            new FareConfig { BusType = "Express_Seat", FlatPrice = 750000m, VatPercentage = 10m },
                            new FareConfig { BusType = "Luxury_Sleeper", FlatPrice = 1100000m, VatPercentage = 10m }
                        },
                        CreatedBy = "SystemSeeder",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedBy = "SystemSeeder",
                        UpdatedAt = DateTime.UtcNow
                    }
                };

                await _context.BusRoutes.InsertManyAsync(routes);
                Console.WriteLine($"--> Đã seeding thành công {routes.Count} tuyến đường liên tỉnh mẫu!");
            }
            else
            {
                Console.WriteLine("--> Bảng BusRoute đã có dữ liệu. Bỏ qua seeding tuyến đường.");
            }
        }

        // 4. SEED TRIPS
        public async Task SeedTrips()
        {
            Console.WriteLine("--> Kiểm tra và seeding dữ liệu Lịch trình Chuyến xe (Trip)...");

            var count = await _context.Trips.CountDocumentsAsync(new BsonDocument());
            if (count > 0)
            {
                Console.WriteLine("--> Bảng Trip đã có dữ liệu. Bỏ qua seeding.");
                return;
            }

            // Lấy thông tin cấu hình sơ đồ ghế mẫu từ database đã seed ở bước trước
            var expressClass = await _context.BusClasses.Find(bc => bc.Id == BusClassExpress45Id).FirstOrDefaultAsync();
            var limousineClass =
                await _context.BusClasses.Find(bc => bc.Id == BusClassLimousine22Id).FirstOrDefaultAsync();

            if (expressClass == null || limousineClass == null)
            {
                Console.WriteLine(
                    "[LỖI] Cần chạy SeedBusClasses trước khi chạy SeedTrips vì thiếu dữ liệu sơ đồ ghế mẫu!");
                return;
            }

            // Khởi tạo danh sách ghế thời gian thực ban đầu (Available) dựa trên cấu trúc mẫu
            var expressRealtimeSeats = new List<RealtimeSeat>();
            foreach (var seat in expressClass.DefaultLayout)
            {
                expressRealtimeSeats.Add(new RealtimeSeat { SeatNumber = seat.SeatNumber, Status = "Available" });
            }

            var limousineRealtimeSeats = new List<RealtimeSeat>();
            foreach (var seat in limousineClass.DefaultLayout)
            {
                limousineRealtimeSeats.Add(new RealtimeSeat { SeatNumber = seat.SeatNumber, Status = "Available" });
            }

            // Set thời gian chạy giả định (Ví dụ: Chuyến xe chạy vào ngày mai)
            DateTime tomorrow = DateTime.UtcNow.Date.AddDays(1);

            var trips = new List<Trip>
            {
                // 1. Chuyến xe Ghế ngồi Hà Nội -> Sài Gòn (Khởi hành 08:00 sáng mai, chạy 30 tiếng)
                new Trip
                {
                    Id = TripHanoiSaigonExpressId,
                    BusId = BusHNExpressId, // Xe ghế ngồi thuộc chi nhánh HN
                    RouteId = RouteHanoiSaigonId, // Tuyến đi HN -> SG
                    BaseFare = 750000m,
                    DepartureTime = tomorrow.AddHours(8),
                    ArrivalTime = tomorrow.AddHours(8).AddHours(30),
                    Status = "Scheduled",
                    RealtimeSeats = expressRealtimeSeats, // Clone 44-45 ghế trống
                    CreatedBy = "SystemSeeder",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },

                // 2. Chuyến xe Giường nằm VIP Hà Nội -> Sài Gòn (Khởi hành 20:00 tối mai, chạy 28 tiếng)
                new Trip
                {
                    Id = TripHanoiSaigonLimoId,
                    BusId = BusHNLimousineId, // Xe giường nằm VIP thuộc chi nhánh HN
                    RouteId = RouteHanoiSaigonId, // Tuyến đi HN -> SG
                    BaseFare = 1100000m,
                    DepartureTime = tomorrow.AddHours(20),
                    ArrivalTime = tomorrow.AddHours(20).AddHours(28),
                    Status = "Scheduled",
                    RealtimeSeats = limousineRealtimeSeats, // Clone 22 phòng nằm trống
                    CreatedBy = "SystemSeeder",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                }
            };

            // Tạo ngẫu nhiên một vài ghế đã được đặt trước ("Booked") ở chuyến Limo để giao diện trông sinh động hơn
            if (trips[1].RealtimeSeats.Count > 3)
            {
                trips[1].RealtimeSeats[0].Status = "Booked"; // Ghế A01 đã có người mua
                trips[1].RealtimeSeats[1].Status = "Booked"; // Ghế A02 đã có người mua
                trips[1].RealtimeSeats[2].Status = "Holding"; // Ghế A03 đang có người giữ chỗ thanh toán
                trips[1].RealtimeSeats[2].HeldUntil = DateTime.UtcNow.AddMinutes(10);
            }

            await _context.Trips.InsertManyAsync(trips);
            Console.WriteLine(
                $"--> Đã seeding thành công {trips.Count} chuyến xe chạy thực tế kèm trạng thái ghế realtime!");
        }

        // 5. SEED BOOKINGS (KÈM CUSTOMER)
        public async Task SeedBookings()
        {
            Console.WriteLine("--> Kiểm tra và seeding dữ liệu Khách hàng (Customer) và Đặt vé (Booking)...");

            // -----------------------------------------------------------------
            // PHẦN 1: SEEDING KHÁCH HÀNG (CUSTOMER)
            // -----------------------------------------------------------------
            var customerCount =
                await _context.Customers
                    .CountDocumentsAsync(new BsonDocument()); // Đảm bảo đúng tên thuộc tính trong ApplicationDbContext
            if (customerCount == 0)
            {
                var customers = new List<Customer>
                {
                    new Customer
                    {
                        Id = CustomerNguyenVanAId,
                        CustomerCode = "KH-0001",
                        FullName = "Nguyễn Văn A",
                        Dob = new DateTime(1995, 05, 20),
                        Gender = "Nam",
                        PhoneNumber = "0987654321",
                        Email = "nguyenvana@gmail.com",
                        MembershipRank = "Gold",
                        TotalPoints = 150,
                        CustomerNotes = "Khách hàng thân thiết, thường đi chuyến đêm",
                        Status = "Active",
                        CreatedBy = "SystemSeeder",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedBy = "SystemSeeder",
                        UpdatedAt = DateTime.UtcNow
                    },
                    new Customer
                    {
                        Id = "64f1a2b3c4d5e6f7a8b9c052",
                        CustomerCode = "KH-0002",
                        FullName = "Trần Thị B",
                        Dob = new DateTime(1998, 10, 15),
                        Gender = "Nữ",
                        PhoneNumber = "0912345678",
                        Email = "tranthib@gmail.com",
                        MembershipRank = "Standard",
                        TotalPoints = 0,
                        Status = "Active",
                        CreatedBy = "SystemSeeder",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedBy = "SystemSeeder",
                        UpdatedAt = DateTime.UtcNow
                    }
                };

                await _context.Customers.InsertManyAsync(customers);
                Console.WriteLine($"--> Đã seeding thành công {customers.Count} khách hàng mẫu!");
            }
            else
            {
                Console.WriteLine("--> Bảng Customer đã có dữ liệu. Bỏ qua seeding khách hàng.");
            }

            // -----------------------------------------------------------------
            // PHẦN 2: SEEDING ĐƠN ĐẶT VÉ (BOOKING)
            // -----------------------------------------------------------------
            var bookingCount = await _context.Bookings.CountDocumentsAsync(new BsonDocument());
            if (bookingCount == 0)
            {
                // Tính toán tiền vé mẫu cho 2 ghế VIP giường nằm (1.100.000đ / ghế)
                decimal seatPrice = 1100000m;
                decimal totalPrice = seatPrice * 2; // 2.200.000đ
                decimal taxAmount = totalPrice * 0.1m; // 220.000đ (VAT 10%)
                decimal finalAmount = totalPrice + taxAmount; // 2.420.000đ

                var bookings = new List<Booking>
                {
                    // Đơn đặt vé thành công cho 2 người đi chung (Ghế A01, A02 đã set Booked ở bảng Trip trước đó)
                    new Booking
                    {
                        Id = BookingLimoId,
                        BookingCode = "BKG-2026-0001",
                        CustomerId = CustomerNguyenVanAId, // Khớp với ID khách hàng Nguyễn Văn A bên trên
                        TripId = TripHanoiSaigonLimoId, // Khớp với chuyến xe Giường nằm HN -> SG đêm mai
                        UserId = null, // Khách tự đặt online nên không cần ID nhân viên (UserId)
                        BranchId = BranchHanoiId, // Điểm xuất phát thuộc chi nhánh Hà Nội
                        BookingTime = DateTime.UtcNow.AddHours(-2), // Giả định đặt trước đó 2 tiếng
                        TotalPrice = totalPrice,
                        TaxAmount = taxAmount,
                        DiscountAmount = 0m,
                        FinalAmount = finalAmount,
                        BookingStatus = "Completed", // Trạng thái đơn hoàn thành
                        PaymentStatus = "Paid", // Đã thanh toán tiền
                        Passengers = new List<PassengerDetail>
                        {
                            new PassengerDetail
                            {
                                SeatNumber = "A01",
                                Name = "Nguyễn Văn A",
                                Dob = new DateTime(1995, 05, 20),
                                FinalSeatPrice = seatPrice
                            },
                            new PassengerDetail
                            {
                                SeatNumber = "A02",
                                Name = "Nguyễn Văn Long", // Người đi cùng
                                Dob = new DateTime(1996, 02, 12),
                                FinalSeatPrice = seatPrice
                            }
                        },
                        Payment = new PaymentInfo
                        {
                            PaymentMethod = "Banking",
                            AmountPaid = finalAmount,
                            TransactionCode = "VNPAY12345678"
                        },
                        Cancellation = null, // Đơn đi bình thường, không hủy
                        CreatedBy = "SystemSeeder",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedBy = "SystemSeeder",
                        UpdatedAt = DateTime.UtcNow
                    }
                };

                await _context.Bookings.InsertManyAsync(bookings);
                Console.WriteLine($"--> Đã seeding thành công lịch sử đặt vé mẫu trùng khớp sơ đồ ghế Realtime!");
            }
            else
            {
                Console.WriteLine("--> Bảng Booking đã có dữ liệu. Bỏ qua seeding đặt vé.");
            }
        }

        // 6. SEED SYSTEM CONFIGS
        public async Task SeedSystemConfigs()
        {
            Console.WriteLine("--> Kiểm tra và seeding dữ liệu Cấu hình Hệ thống (SystemConfig)...");

            // Khớp tên thuộc tính _context.SystemConfigs với DBContext của bạn
            var config = await _context.SystemConfigs.Find(c => c.Id == "global_system_configuration")
                .FirstOrDefaultAsync();

            if (config != null)
            {
                Console.WriteLine("--> Cấu hình hệ thống global đã tồn tại. Bỏ qua seeding.");
                return;
            }

            // Khởi tạo bộ cấu hình hệ thống tiêu chuẩn theo đề bài
            var globalConfig = new SystemConfig
            {
                Id = "global_system_configuration", // Fix cứng ID duy nhất theo model của bạn

                // 1. Cấu hình quy tắc giảm giá theo độ tuổi (Age Discount Rules)
                AgeDiscountRules = new List<AgeDiscountRule>
                {
                    new AgeDiscountRule
                    {
                        MinAge = 0,
                        MaxAge = 5,
                        DiscountPercentage =
                            100m // Trẻ em nhỏ tuổi (dưới 6 tuổi) được miễn phí vé hoàn toàn (Giảm 100%)
                    },
                    new AgeDiscountRule
                    {
                        MinAge = 6,
                        MaxAge = 10,
                        DiscountPercentage = 25m // Trẻ em từ 6 đến 10 tuổi được giảm giá 25% (Vé trẻ em)
                    },
                    new AgeDiscountRule
                    {
                        MinAge = 18,
                        MaxAge = 23,
                        DiscountPercentage = 10m // Độ tuổi Sinh viên (từ 18 đến 23) ưu đãi giảm 10% giá vé cơ bản
                    },
                    new AgeDiscountRule
                    {
                        MinAge = 60,
                        MaxAge = 120,
                        DiscountPercentage =
                            15m // Người cao tuổi (từ 60 tuổi trở lên) được giảm giá 15% theo chính sách xã hội
                    }
                },

                // 2. Cấu hình quy định phạt tiền khi hoàn/hủy vé (Cancellation Policies)
                // Luật thường là: Hủy càng sát giờ chạy phạt càng nặng
                CancellationPolicies = new List<CancellationPolicy>
                {
                    new CancellationPolicy
                    {
                        HoursBeforeDeparture = 24,
                        PenaltyPercentage =
                            10m // Hủy trước khi xe chạy trên 24 giờ: Chỉ phạt 10% (Hoàn lại 90% tiền vé)
                    },
                    new CancellationPolicy
                    {
                        HoursBeforeDeparture = 12,
                        PenaltyPercentage =
                            30m // Hủy trong khoảng từ 12 đến 24 giờ trước khởi hành: Phạt 30% (Hoàn 70%)
                    },
                    new CancellationPolicy
                    {
                        HoursBeforeDeparture = 4,
                        PenaltyPercentage = 50m // Hủy từ 4 đến 12 giờ trước khi chạy: Phạt 50% (Hoàn 50%)
                    },
                    new CancellationPolicy
                    {
                        HoursBeforeDeparture = 0,
                        PenaltyPercentage =
                            100m // Hủy dưới 4 tiếng hoặc xe đã chạy: Phạt 100% (Mất trắng tiền vé, không hoàn lại)
                    }
                },

                UpdatedBy = "SystemSeeder",
                UpdatedAt = DateTime.UtcNow
            };

            await _context.SystemConfigs.InsertOneAsync(globalConfig);
            Console.WriteLine(
                "--> Đã khởi tạo cấu hình hệ thống global (Quy tắc giảm tuổi + Chính sách hủy vé) thành công!");
        }

        // 7. SEED PERMISSIONS (Tổng cộng 37 Quyền Hệ Thống)
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

        // 5. SEED ROLES
        public async Task SeedDynamicRoles()
        {
            Console.WriteLine("--> Kiểm tra và seeding dữ liệu bảng Vai trò (DynamicRole)...");

            var count = await _context.DynamicRoles.CountDocumentsAsync(new BsonDocument());
            if (count > 0)
            {
                Console.WriteLine("--> Bảng DynamicRole đã có dữ liệu. Bỏ qua seeding.");
                return;
            }

            var roles = new List<DynamicRole>
            {
                // Vai trò Quản trị tối cao: Được gán toàn bộ 37 quyền hệ thống tự động
                new DynamicRole
                {
                    Id = RoleAdminId,
                    RoleName = "SuperAdmin",
                    PermissionIds = _allPermissionIds, // Copy toàn bộ danh sách Id phân quyền vừa seed
                    CreatedBy = "SystemSeeder",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                },
                // Vai trò Nhân viên bán vé tại quầy (Ví dụ chỉ được xem và xử lý liên quan Booking & Customer)
                new DynamicRole
                {
                    Id = "64f1a2b3c4d5e6f7a8b9c098",
                    RoleName = "TicketAgent",
                    PermissionIds = new List<string>
                    {
                        "64f1a2b3c4d5e6f7a8b9ca05", // View.Trip
                        "64f1a2b3c4d5e6f7a8b9ca21", // View.Booking
                        "64f1a2b3c4d5e6f7a8b9ca22", // Create.Booking
                        "64f1a2b3c4d5e6f7a8b9ca23", // Update.Booking
                        "64f1a2b3c4d5e6f7a8b9ca25", // View.Customer
                        "64f1a2b3c4d5e6f7a8b9ca26" // Create.Customer
                    },
                    CreatedBy = "SystemSeeder",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedBy = "SystemSeeder",
                    UpdatedAt = DateTime.UtcNow
                }
            };

            await _context.DynamicRoles.InsertManyAsync(roles);
            Console.WriteLine($"--> Đã seeding thành công {roles.Count} vai trò cốt lõi!");
        }
    }
}