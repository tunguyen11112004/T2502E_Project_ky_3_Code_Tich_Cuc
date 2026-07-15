using System;

namespace Bus_ticket.ViewModels;

public class BranchCancellationViewModel
{
    public string BranchId { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public int TotalTrips { get; set; } // Tổng số chuyến xe đã chạy/lên lịch
    public int CanceledTrips { get; set; } // Số chuyến bị hủy
    
    // Tỷ lệ hủy chuyến (%)
    public double CancellationRate => TotalTrips > 0 
        ? Math.Round(((double)CanceledTrips / TotalTrips) * 100, 2) 
        : 0;
}