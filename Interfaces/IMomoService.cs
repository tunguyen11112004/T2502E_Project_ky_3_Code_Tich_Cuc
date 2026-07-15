using Bus_ticket.Models;

namespace Bus_ticket.Interfaces
{
    public interface IMomoService
    {
        Task<MomoExecuteResponse> CreatePaymentAsync(string orderId, string orderInfo, long amount);
    }
}