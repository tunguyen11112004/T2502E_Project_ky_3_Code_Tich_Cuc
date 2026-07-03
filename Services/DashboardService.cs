using Bus_ticket.Data;
using Bus_ticket.Models;
using Bus_ticket.ViewModels;
using MongoDB.Driver;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bus_ticket.Services
{
    public class DashboardService
    {
        private readonly ApplicationDbContext _dbContext;
        
        public DashboardService(ApplicationDbContext dbContext) 
        {
            _dbContext = dbContext;
        }

        public async Task<DashboardSummaryDto> GetTotalRevenueStatsAsync(DateTime fromDate, DateTime toDate)
        {
            var start = fromDate.Date.ToUniversalTime();
            var end = toDate.Date.AddDays(1).ToUniversalTime(); 

            var bookings = await _dbContext.Bookings
                .Find(b => b.BookingTime >= start && b.BookingTime < end)
                .ToListAsync();
            
            var valid = bookings.Where(b => IsCompleted(b.BookingStatus) && IsPaid(b.PaymentStatus)).ToList();
            
            return new DashboardSummaryDto {
                TotalRevenue = valid.Sum(b => b.FinalAmount),
                SuccessfulBookings = valid.Count,
                TotalTickets = valid.Sum(b => b.Passengers?.Count ?? 0),
                RoutesWithRevenue = valid.Select(b => b.TripId).Distinct().Count()
            };
        }

        public async Task<List<RouteOccupancyDto>> GetRouteOccupancyAsync(DateTime fromDate, DateTime toDate)
        {
            var start = fromDate.Date.ToUniversalTime();
            var end = toDate.Date.AddDays(1).ToUniversalTime();

            var routes = await _dbContext.BusRoutes.Find(_ => true).ToListAsync();
            var trips = await _dbContext.Trips
                .Find(t => t.DepartureTime >= start && t.DepartureTime < end)
                .ToListAsync();
            
            return routes.Select(r => {
                var routeTrips = trips.Where(t => t.RouteId == r.Id).ToList();
                int totalCapacity = routeTrips.Sum(t => t.RealtimeSeats?.Count ?? 0);
                int sold = routeTrips.Sum(t => t.RealtimeSeats?.Count(s => s.Status != "Available") ?? 0);
                
                return new RouteOccupancyDto {
                    RouteName = $"{r.DeparturePoint} - {r.DestinationPoint}",
                    Occupancy = totalCapacity > 0 ? (int)((double)sold / totalCapacity * 100) : 0
                };
            }).Where(x => x.Occupancy > 0).OrderByDescending(x => x.Occupancy).ToList();
        }

        public async Task<List<SoldOutTimeFrameDto>> GetSoldOutTimeFramesAsync(DateTime from, DateTime to)
        {
            var start = from.Date.ToUniversalTime();
            var end = to.Date.AddDays(1).ToUniversalTime();

            var trips = await _dbContext.Trips
                .Find(t => t.DepartureTime >= start && t.DepartureTime < end)
                .ToListAsync();
            
            return trips.Where(t => t.RealtimeSeats != null && t.RealtimeSeats.Count > 0 && !t.RealtimeSeats.Any(s => s.Status == "Available"))
                .GroupBy(t => t.DepartureTime.ToLocalTime().ToString("HH:mm"))
                .Select(g => new SoldOutTimeFrameDto { TimeFrame = g.Key, SoldOutCount = g.Count() })
                .OrderByDescending(x => x.SoldOutCount).ToList();
        }

        public async Task<RouteRevenueReportViewModel> GetRouteRevenueReportAsync(DateTime from, DateTime to)
        {
            return new RouteRevenueReportViewModel(); 
        }

        private bool IsCompleted(string s) => new[] { "completed", "success", "confirmed" }.Contains(s?.ToLower());
        private bool IsPaid(string s) => new[] { "paid", "success", "completed" }.Contains(s?.ToLower());
    }

    public class DashboardSummaryDto 
    { 
        public decimal TotalRevenue { get; set; } 
        public int SuccessfulBookings { get; set; } 
        public int TotalTickets { get; set; } 
        public int RoutesWithRevenue { get; set; } 
    }
    
    public class RouteOccupancyDto 
    { 
        public string RouteName { get; set; } 
        public int Occupancy { get; set; } 
    }
    
    public class SoldOutTimeFrameDto 
    { 
        public string TimeFrame { get; set; } 
        public int SoldOutCount { get; set; } 
    }
}