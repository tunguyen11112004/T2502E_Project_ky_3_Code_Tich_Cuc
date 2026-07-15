namespace Bus_ticket.Helpers;

public static class BusClassNameHelper
{
    public static string NormalizeKey(string className) =>
        className.Trim().ToLowerInvariant();
}
