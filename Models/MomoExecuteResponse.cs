namespace Bus_ticket.Models
{
    public class MomoExecuteResponse
    {
        public string PartnerCode { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public long Amount { get; set; }
        public string OrderInfo { get; set; } = string.Empty;
        public string OrderType { get; set; } = string.Empty;
        public string TransId { get; set; } = string.Empty;
        public int ResultCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string PayUrl { get; set; } = string.Empty;
        public string Deeplink { get; set; } = string.Empty;
        public string QrCodeUrl { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }
}