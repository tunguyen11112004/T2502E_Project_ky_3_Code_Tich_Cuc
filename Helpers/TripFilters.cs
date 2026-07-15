using Bus_ticket.Models;
using MongoDB.Driver;

namespace Bus_ticket.Helpers;

public static class TripFilters
{
    public static FilterDefinition<Trip> NotDeleted =>
        Builders<Trip>.Filter.Or(
            Builders<Trip>.Filter.Eq(t => t.DeletedAt, null),
            Builders<Trip>.Filter.Exists(t => t.DeletedAt, false));
}
