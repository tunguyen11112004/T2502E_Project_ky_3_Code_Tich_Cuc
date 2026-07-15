namespace Bus_ticket.Interfaces;

public interface IRabbitMQService
{
    Task PublishOrderAsync(string orderId);
}