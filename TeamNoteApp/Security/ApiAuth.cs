using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using TeamNoteApp.Data;
using TeamNoteApp.Models;

namespace TeamNoteApp.Security;

public static class ApiAuth
{
    public static async Task<User?> GetCurrentUserAsync(
        HttpContext context,
        AppDbContext db)
    {
        var authorization = context.Request.Headers.Authorization.ToString();

        if (!authorization.StartsWith(
            "Bearer ",
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authorization["Bearer ".Length..].Trim();

        // Tokens are 32 random bytes represented as 64 hex characters.
        if (token.Length != 64 || !token.All(Uri.IsHexDigit))
            return null;

        // These high-entropy random tokens are not passwords. SHA-256 hashing
        // allows lookup without storing a reusable bearer credential. This is
        // hashing, not encryption; there is no decryption operation.
        var tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        return await db.Users
            .AsNoTracking() // We don't plan to modify the user, so no need to track it.
            .SingleOrDefaultAsync(u => u.ApiTokenHash == tokenHash);
    }
}