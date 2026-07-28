using System.ComponentModel.DataAnnotations;

namespace Bus_ticket.ViewModels
{
    public class BusRouteFormViewModel
    {
        public string? Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập điểm đi.")]
        [Display(Name = "Điểm đi")]
        public string DeparturePoint { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập điểm đến.")]
        [Display(Name = "Điểm đến")]
        public string DestinationPoint { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập khoảng cách.")]
        [Range(0.1, 100000, ErrorMessage = "Khoảng cách phải lớn hơn 0.")]
        [Display(Name = "Khoảng cách")]
        public double DistanceKm { get; set; }
    }
}