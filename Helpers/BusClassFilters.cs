using Bus_ticket.Models;
using MongoDB.Driver;

namespace Bus_ticket.Helpers;

public static class BusClassFilters
{
    public static FilterDefinition<BusClass> NotDeleted =>
        Builders<BusClass>.Filter.Or(
            Builders<BusClass>.Filter.Eq(bc => bc.DeletedAt, null),
            Builders<BusClass>.Filter.Exists(bc => bc.DeletedAt, false));

    public static FilterDefinition<BusClass> IsDeleted =>
        Builders<BusClass>.Filter.And(
            Builders<BusClass>.Filter.Exists(bc => bc.DeletedAt, true),
            Builders<BusClass>.Filter.Ne(bc => bc.DeletedAt, null));
}
