using Bus_ticket.Models;
using Microsoft.EntityFrameworkCore;

namespace Bus_ticket.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Bus> Buses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Bus>()
            .HasIndex(x => x.BusCode)
            .IsUnique();

        modelBuilder.Entity<Bus>()
            .HasIndex(x => x.BusNumber)
            .IsUnique();

        modelBuilder.Entity<Bus>()
            .Property(x => x.Distance)
            .HasPrecision(10, 2);
    }
}