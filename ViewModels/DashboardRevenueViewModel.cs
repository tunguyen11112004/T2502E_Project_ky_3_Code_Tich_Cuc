using System;
using System.Collections.Generic;

namespace Bus_ticket.ViewModels
{
    public class DashboardRevenueViewModel
    {
        public List<RevenueByCategoryDto> ChartData { get; set; } = new();
        public List<TransactionDetailDto> TableData { get; set; } = new();
    }

    public class RevenueByCategoryDto
    {
        public string Category { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
    }

    public class TransactionDetailDto
    {
        public string BookingCode { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string BusClass { get; set; } = string.Empty;
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
    }
}