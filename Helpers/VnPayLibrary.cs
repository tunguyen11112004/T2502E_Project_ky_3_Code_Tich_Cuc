using System.Net;
using System.Security.Cryptography;
using System.Text;
namespace Bus_ticket.Helpers;

public class VnPayLibrary
{
    private SortedList<string, string> _requestData = new SortedList<string, string>(new VnPayCompare());

    public void AddRequestData(string key, string value)
    {
        if (!string.IsNullOrEmpty(value)) _requestData.Add(key, value);
    }

    public string CreateRequestUrl(string baseUrl, string vnp_HashSecret)
    {
        // 1. Tạo chuỗi băm (RAW): Không UrlEncode
        StringBuilder data = new StringBuilder();
        foreach (var key in _requestData.Keys)
        {
            data.Append(key + "=" + _requestData[key] + "&");
        }
        string queryStringRaw = data.ToString().TrimEnd('&');
    
        // 2. Băm HMACSHA512
        string sign = HmacSHA512(vnp_HashSecret, queryStringRaw);
    
        // 3. Tạo URL chuyển hướng: Dùng UrlEncode
        StringBuilder urlData = new StringBuilder();
        foreach (var key in _requestData.Keys)
        {
            urlData.Append(WebUtility.UrlEncode(key) + "=" + WebUtility.UrlEncode(_requestData[key]) + "&");
        }
    
        // Nối chuỗi an toàn: fullUrl = baseUrl + ? + [các tham số đã encode] + vnp_SecureHash=...
        string fullUrl = baseUrl + "?" + urlData.ToString() + "vnp_SecureHash=" + sign;
    
        return fullUrl;
    }

    public bool ValidateSignature(SortedList<string, string> inputData, string vnp_HashSecret)
    {
        // Lấy hash gửi về
        var vnp_SecureHash = inputData["vnp_SecureHash"];
    
        // Tạo danh sách mới để băm (đảm bảo đúng thứ tự A-Z)
        StringBuilder data = new StringBuilder();
        var sortedKeys = inputData.Keys.OrderBy(k => k, StringComparer.Ordinal);
    
        foreach (var key in sortedKeys)
        {
            // VNPAY không bao gồm SecureHash trong chuỗi băm
            if (key != "vnp_SecureHash" && !string.IsNullOrEmpty(inputData[key]))
            {
                // QUAN TRỌNG: Chuỗi băm gửi về PHẢI là chuỗi thô (raw) 
                // Nếu VNPAY gửi về có mã hóa, bạn phải xử lý dấu cách thành '+'
                data.Append(key + "=" + inputData[key] + "&");
            }
        }
    
        string queryString = data.ToString().TrimEnd('&');
        string sign = HmacSHA512(vnp_HashSecret, queryString);
    
        // So sánh chữ ký
        return vnp_SecureHash.Equals(sign, StringComparison.OrdinalIgnoreCase);
    }
    
    private string HmacSHA512(string key, string inputData)
    {
        // Sử dụng khởi tạo trực tiếp, không qua .Create()
        using (var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key)))
        {
            byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(inputData));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }
    }
}

public class VnPayCompare : IComparer<string>
{
    public int Compare(string x, string y) => string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
}