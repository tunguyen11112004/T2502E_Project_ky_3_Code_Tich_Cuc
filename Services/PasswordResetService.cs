using System.Security.Cryptography;
using System.Text;
using Bus_ticket.Data;
using Bus_ticket.Models;
using Microsoft.AspNetCore.WebUtilities;
using MongoDB.Driver;

namespace Bus_ticket.Services;

public sealed class PasswordResetService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);
    private readonly ApplicationDbContext _context;

    public PasswordResetService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> CreateTokenAsync(User user)
    {
        if (string.IsNullOrWhiteSpace(user.Id))
        {
            throw new InvalidOperationException("User does not have a valid id.");
        }

        var now = DateTime.UtcNow;

        // Chỉ token mới nhất được dùng. Token cũ chưa dùng sẽ bị vô hiệu hóa.
        var revokeUpdate = Builders<PasswordResetToken>.Update
            .Set(token => token.Status, "Revoked")
            .Set(token => token.RevokedAt, now);

        await _context.PasswordResetTokens.UpdateManyAsync(
            token => token.UserId == user.Id
                     && token.UsedAt == null
                     && token.RevokedAt == null,
            revokeUpdate);

        var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        var resetToken = new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            CreatedAt = now,
            ExpiresAt = now.Add(TokenLifetime),
            Status = "Active"
        };

        await _context.PasswordResetTokens.InsertOneAsync(resetToken);
        return rawToken;
    }

    public async Task<bool> IsTokenValidAsync(string email, string rawToken)
    {
        var user = await FindActiveUserAsync(email);
        if (user?.Id == null || string.IsNullOrWhiteSpace(rawToken))
        {
            return false;
        }

        var tokenHash = HashToken(rawToken);
        var now = DateTime.UtcNow;

        return await _context.PasswordResetTokens
            .Find(token => token.UserId == user.Id
                           && token.TokenHash == tokenHash
                           && token.Status == "Active"
                           && token.UsedAt == null
                           && token.RevokedAt == null
                           && token.ExpiresAt > now)
            .AnyAsync();
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(
        string email,
        string rawToken,
        string newPassword)
    {
        var user = await FindActiveUserAsync(email);
        if (user?.Id == null || string.IsNullOrWhiteSpace(rawToken))
        {
            return PasswordResetResult.Fail("Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");
        }

        if (BCrypt.Net.BCrypt.Verify(newPassword, user.PasswordHash))
        {
            return PasswordResetResult.Fail("Mật khẩu mới không được trùng với mật khẩu hiện tại.");
        }

        var now = DateTime.UtcNow;
        var tokenHash = HashToken(rawToken);

        // Đánh dấu token đã dùng bằng điều kiện nguyên tử để token không thể dùng hai lần.
        var tokenFilter = Builders<PasswordResetToken>.Filter.And(
            Builders<PasswordResetToken>.Filter.Eq(token => token.UserId, user.Id),
            Builders<PasswordResetToken>.Filter.Eq(token => token.TokenHash, tokenHash),
            Builders<PasswordResetToken>.Filter.Eq(token => token.Status, "Active"),
            Builders<PasswordResetToken>.Filter.Eq(token => token.UsedAt, null),
            Builders<PasswordResetToken>.Filter.Eq(token => token.RevokedAt, null),
            Builders<PasswordResetToken>.Filter.Gt(token => token.ExpiresAt, now));

        var consumeTokenUpdate = Builders<PasswordResetToken>.Update
            .Set(token => token.Status, "Used")
            .Set(token => token.UsedAt, now);

        var consumedToken = await _context.PasswordResetTokens.FindOneAndUpdateAsync(
            tokenFilter,
            consumeTokenUpdate,
            new FindOneAndUpdateOptions<PasswordResetToken>
            {
                ReturnDocument = ReturnDocument.After
            });

        if (consumedToken == null)
        {
            return PasswordResetResult.Fail("Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(newPassword, 10);
        var userUpdate = Builders<User>.Update
            .Set(item => item.PasswordHash, passwordHash)
            .Set(item => item.ActiveSessionId, null)
            .Set(item => item.UpdatedAt, now)
            .Set(item => item.UpdatedBy, "PasswordReset");

        var updateResult = await _context.Users.UpdateOneAsync(
            item => item.Id == user.Id && item.Status == "Active",
            userUpdate);

        if (updateResult.MatchedCount == 0)
        {
            return PasswordResetResult.Fail("Không thể cập nhật mật khẩu cho tài khoản này.");
        }

        // Sau khi đổi thành công, vô hiệu hóa toàn bộ token chưa dùng còn lại của user.
        var revokeOthersUpdate = Builders<PasswordResetToken>.Update
            .Set(token => token.Status, "Revoked")
            .Set(token => token.RevokedAt, now);

        await _context.PasswordResetTokens.UpdateManyAsync(
            token => token.UserId == user.Id
                     && token.Id != consumedToken.Id
                     && token.UsedAt == null
                     && token.RevokedAt == null,
            revokeOthersUpdate);

        return PasswordResetResult.Ok();
    }

    private async Task<User?> FindActiveUserAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        return await _context.Users
            .Find(user => user.Email == normalizedEmail && user.Status == "Active")
            .FirstOrDefaultAsync();
    }

    private static string HashToken(string rawToken)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(rawToken);
        return Convert.ToHexString(SHA256.HashData(tokenBytes));
    }
}

public sealed record PasswordResetResult(bool Success, string Message)
{
    public static PasswordResetResult Ok() => new(true, string.Empty);

    public static PasswordResetResult Fail(string message) => new(false, message);
}
