using System.Security.Cryptography;
using System.Text;
using Bus_ticket.Interfaces;
using Bus_ticket.Models;
using Bus_ticket.Settings;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Bus_ticket.Services
{
    public class MomoService : IMomoService
    {
        private readonly MomoSettings _momoSettings;
        private readonly HttpClient _httpClient;

        public MomoService(IOptions<MomoSettings> momoSettings)
        {
            _momoSettings = momoSettings.Value;
            _httpClient = new HttpClient();
        }

        public async Task<MomoExecuteResponse> CreatePaymentAsync(string orderId, string orderInfo, long amount)
        {
            var requestId = Guid.NewGuid().ToString("N"); // Chuỗi ID sạch không dấu gạch ngang
            var returnUrl = "https://localhost:5280/Booking/MomoReturn"; 
            var ipnUrl = "https://localhost:5280/Booking/MomoNotify"; 
            var extraData = ""; 
            var requestType = "captureWallet";

            // 1. CHUỖI BĂM RAWHASH: Sắp xếp theo chuẩn Alphabet tuyệt đối của MoMo v2
            var rawHash = $"accessKey={_momoSettings.AccessKey}&amount={amount}&extraData={extraData}&ipnUrl={ipnUrl}&orderId={orderId}&orderInfo={orderInfo}&partnerCode={_momoSettings.PartnerCode}&redirectUrl={returnUrl}&requestId={requestId}&requestType={requestType}";

            var signature = ComputeHmacSha256(rawHash, _momoSettings.SecretKey);

            // 2. BODY JSON GỬI ĐI: Đóng gói đồng bộ hoàn toàn với chuỗi đã băm
            var requestBody = new
            {
                partnerCode = _momoSettings.PartnerCode,
                requestId = requestId,
                amount = amount,
                orderId = orderId,
                orderInfo = orderInfo,
                redirectUrl = returnUrl,
                ipnUrl = ipnUrl,
                extraData = extraData,
                requestType = requestType,
                signature = signature,
                lang = "vi"
            };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
    
            var response = await _httpClient.PostAsync(_momoSettings.PaymentUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();
    
            return JsonConvert.DeserializeObject<MomoExecuteResponse>(responseContent) ?? new MomoExecuteResponse();
        }


        // Hàm băm SHA256 để bảo mật giao dịch
        private string ComputeHmacSha256(string message, string secretKey)
        {
            var keyByte = Encoding.UTF8.GetBytes(secretKey);
            var messageBytes = Encoding.UTF8.GetBytes(message);
            using (var hmacsha256 = new System.Security.Cryptography.HMACSHA256(keyByte))
            {
                var hashmessage = hmacsha256.ComputeHash(messageBytes);
                // Bắt buộc sử dụng ToLower() vì MoMo chỉ chấp nhận chữ ký viết thường
                return BitConverter.ToString(hashmessage).Replace("-", "").ToLower();
            }
        }
    }
}