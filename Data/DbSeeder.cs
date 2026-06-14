using Bogus;
using Bus_ticket.Models;
using Bus_ticket.Services;

namespace Bus_ticket.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        var busService = scope.ServiceProvider.GetRequiredService<IBusService>();

        var existingBuses = await busService.GetAllAsync();

        if (existingBuses.Any())
        {
            return;
        }

        var routes = new[]
        {
            "Hà Nội - Hải Phòng",
            "Hà Nội - Sapa",
            "Hà Nội - Đà Nẵng",
            "TP.HCM - Đà Lạt",
            "TP.HCM - Cần Thơ",
            "Đà Nẵng - Huế",
            "Nha Trang - TP.HCM",
            "Quy Nhơn - Đà Nẵng"
        };

        var busTypes = new[]
        {
            BusType.Express,
            BusType.Luxury,
            BusType.VolvoNonAC,
            BusType.VolvoAC
        };

        var usedCodes = new HashSet<string>();
        var usedBusNumbers = new HashSet<string>();

        string GenerateUniqueBusCode()
        {
            string code;

            do
            {
                code = Random.Shared.Next(10000, 100000).ToString();
            }
            while (!usedCodes.Add(code));

            return code;
        }

        string GenerateUniqueBusNumber(Faker faker)
        {
            string busNumber;

            do
            {
                busNumber = $"{faker.Random.Int(10, 99)}B-{faker.Random.Int(10000, 99999)}";
            }
            while (!usedBusNumbers.Add(busNumber));

            return busNumber;
        }

        var faker = new Faker<Bus>("vi")
            .RuleFor(x => x.BusCode, _ => GenerateUniqueBusCode())
            .RuleFor(x => x.BusNumber, f => GenerateUniqueBusNumber(f))
            .RuleFor(x => x.BusType, f => f.PickRandom(busTypes))
            .RuleFor(x => x.Route, f => f.PickRandom(routes))
            .RuleFor(x => x.Distance, f => Math.Round(f.Random.Decimal(50, 1500), 1))
            .RuleFor(x => x.DepartureTime, f => f.Date.Soon(30))
            .RuleFor(x => x.Description, f => $"<p>{f.Lorem.Sentence(12)}</p>")
            .RuleFor(x => x.ImageUrl, _ => null)
            .RuleFor(x => x.Status, _ => BusStatus.Active);

        var buses = faker.Generate(Random.Shared.Next(50, 101));

        foreach (var bus in buses)
        {
            await busService.CreateAsync(bus);
        }
    }
}