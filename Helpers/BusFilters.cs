using Bus_ticket.Models;
using MongoDB.Driver;

namespace Bus_ticket.Helpers;

public static class BusFilters
{
    public static FilterDefinition<Bus> NotDeleted =>
        Builders<Bus>.Filter.Or(
            Builders<Bus>.Filter.Eq(b => b.DeletedAt, null),
            Builders<Bus>.Filter.Exists(b => b.DeletedAt, false));

    public static FilterDefinition<Bus> IsDeleted =>
        Builders<Bus>.Filter.And(
            Builders<Bus>.Filter.Exists(b => b.DeletedAt, true),
            Builders<Bus>.Filter.Ne(b => b.DeletedAt, null));
}