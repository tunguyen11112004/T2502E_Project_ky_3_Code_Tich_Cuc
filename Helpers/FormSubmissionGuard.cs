using System;
using System.Collections.Concurrent;

namespace Bus_ticket.Helpers;

public static class FormSubmissionGuard
{
    private static readonly ConcurrentDictionary<string, byte> ActiveTokens = new();
    private static readonly ConcurrentDictionary<string, byte> CompletedTokens = new();

    public static string CreateToken() => Guid.NewGuid().ToString("N");

    public static bool IsCompleted(string? token) =>
        !string.IsNullOrWhiteSpace(token) && CompletedTokens.ContainsKey(token.Trim());

    public static bool TryAcquire(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var normalized = token.Trim();
        if (CompletedTokens.ContainsKey(normalized))
        {
            return false;
        }

        return ActiveTokens.TryAdd(normalized, 0);
    }

    public static void MarkCompleted(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var normalized = token.Trim();
        ActiveTokens.TryRemove(normalized, out _);
        CompletedTokens.TryAdd(normalized, 0);
    }

    public static void Release(string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            ActiveTokens.TryRemove(token.Trim(), out _);
        }
    }
}
