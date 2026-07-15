using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Bus_ticket.Models;
using Microsoft.EntityFrameworkCore;

namespace Bus_ticket.Data
{
    public class ApplicationDbContext
    {
        private readonly IMongoDatabase _database;

        public ApplicationDbContext(IConfiguration configuration)
        {
            var connectionString = configuration.GetSection("MongoDbSettings:ConnectionString").Value;
            var databaseName = configuration.GetSection("MongoDbSettings:DatabaseName").Value;

            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(databaseName);
        }

        public IMongoCollection<Branch> Branches => _database.GetCollection<Branch>("branches");
        public IMongoCollection<User> Users => _database.GetCollection<User>("users");
        public IMongoCollection<DynamicRole> DynamicRoles => _database.GetCollection<DynamicRole>("dynamicRoles");
        public IMongoCollection<Permission> Permissions => _database.GetCollection<Permission>("permissions");
        public IMongoCollection<Customer> Customers => _database.GetCollection<Customer>("customers");
        public IMongoCollection<Bus> Buses => _database.GetCollection<Bus>("buses");
        public IMongoCollection<BusOperator> BusOperators => _database.GetCollection<BusOperator>("busoperators");
        public IMongoCollection<BusClass> BusClasses => _database.GetCollection<BusClass>("busclasses");
        public IMongoCollection<BusRoute> BusRoutes => _database.GetCollection<BusRoute>("busroutes");
        public IMongoCollection<BusBranch> BusBranches => _database.GetCollection<BusBranch>("busbranches");
        public IMongoCollection<Trip> Trips => _database.GetCollection<Trip>("trips");
        public IMongoCollection<Booking> Bookings => _database.GetCollection<Booking>("bookings");
        public IMongoCollection<SystemConfig> SystemConfigs => _database.GetCollection<SystemConfig>("systemconfigs");
        public IMongoCollection<News> News => _database.GetCollection<News>("news");
        public IMongoCollection<PriceConfig> PriceConfigs => _database.GetCollection<PriceConfig>("priceconfigs");
    }
}