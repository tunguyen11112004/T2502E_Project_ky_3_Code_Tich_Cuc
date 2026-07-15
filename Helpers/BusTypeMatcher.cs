using Bus_ticket.Models;

namespace Bus_ticket.Helpers;

public static class BusTypeMatcher
{
    public static bool Matches(string configBusType, BusClass? busClass, Bus? bus = null)
    {
        if (string.IsNullOrWhiteSpace(configBusType))
        {
            return false;
        }

        var key = Normalize(configBusType);

        if (busClass != null)
        {
            if (key == Normalize(busClass.ClassName) || key == Normalize(busClass.BusType))
            {
                return true;
            }

            var className = Normalize(busClass.ClassName);
            var busType = Normalize(busClass.BusType);

            if (!string.IsNullOrEmpty(className) && (key.Contains(className) || className.Contains(key)))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(busType) && (key.Contains(busType) || busType.Contains(key)))
            {
                return true;
            }
        }

        if (bus != null && !string.IsNullOrWhiteSpace(bus.LegacyBusType))
        {
            var legacy = Normalize(bus.LegacyBusType);
            return key == legacy || key.Contains(legacy) || legacy.Contains(key);
        }

        return false;
    }

    private static string Normalize(string? value)
    {
        return (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace("_", " ")
            .Replace("-", " ")
            .Replace("(", " ")
            .Replace(")", " ");
    }
}
