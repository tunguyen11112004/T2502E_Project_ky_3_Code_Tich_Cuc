using Bus_ticket.Models;

namespace Bus_ticket.Services;

public interface IBusService
{
    Task<List<Bus>> GetAllAsync();

    Task<Bus?> GetByIdAsync(string id);

    Task<bool> BusNumberExistsAsync(string busNumber);

    Task<bool> BusCodeExistsAsync(string busCode);

    Task CreateAsync(Bus bus);

    Task CancelManyAsync(List<string> ids);
}