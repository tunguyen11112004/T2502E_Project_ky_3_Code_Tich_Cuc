namespace Bus_ticket.Settings;

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = string.Empty;

    public string UsersCollectionName { get; set; } = string.Empty;
}