using System.Threading.Tasks;

namespace Bus_ticket.Interfaces;

public interface IDbSeeder
{
    Task SeedAllAsync();
}