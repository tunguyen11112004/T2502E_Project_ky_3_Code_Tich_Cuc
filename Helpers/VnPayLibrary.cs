using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Bus_ticket.Helpers;

public class VnPayLibrary
{
    private readonly SortedList<string, string> _requestData = new(new VnPayCompare());

    public void AddRequestData(string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrEmpty(value))
        {
            _requestData[key] = value;
        }
    }

    public string CreateRequestUrl(string baseUrl, string vnpHashSecret)
    {
        var requestData = BuildQueryString(_requestData);
        var secureHash = HmacSHA512(vnpHashSecret.Trim(), requestData);

        return $"{baseUrl}?{requestData}&vnp_SecureHash={secureHash}";
    }

    public bool ValidateSignature(SortedList<string, string> inputData, string vnpHashSecret)
    {
        if (!inputData.TryGetValue("vnp_SecureHash", out var receivedHash))
        {
            return false;
        }

        var dataToSign = new SortedList<string, string>(new VnPayCompare());

        foreach (var item in inputData)
        {
            if (item.Key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase)
                || item.Key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(item.Value))
            {
                dataToSign[item.Key] = item.Value;
            }
        }

        var signData = BuildQueryString(dataToSign);
        var calculatedHash = HmacSHA512(vnpHashSecret.Trim(), signData);

        return receivedHash.Equals(calculatedHash, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildQueryString(SortedList<string, string> data)
    {
        var query = new StringBuilder();

        foreach (var item in data)
        {
            if (string.IsNullOrEmpty(item.Value))
            {
                continue;
            }

            query.Append(WebUtility.UrlEncode(item.Key));
            query.Append('=');
            query.Append(WebUtility.UrlEncode(item.Value));
            query.Append('&');
        }

        if (query.Length > 0)
        {
            query.Length--;
        }

        return query.ToString();
    }

    private static string HmacSHA512(string key, string inputData)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var inputBytes = Encoding.UTF8.GetBytes(inputData);

        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(inputBytes);

        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}

public class VnPayCompare : IComparer<string>
{
    public int Compare(string? x, string? y)
    {
        return string.Compare(x, y, StringComparison.Ordinal);
    }
}