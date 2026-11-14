using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using TeamNotesApp.Data;
using TeamNotesApp.Models;

namespace TeamNotesApp.Security;

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

        // Tokens are machine-generated, not human input, so we don't need a
        //  computationally expensive hasing algorithm, SHA-256 is fine but
        //  can be improved upon in the future.
        // Also, we only supply the token once. We store the hash to make
        //  the encryption one-way.
        var tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        return await db.Users
            .AsNoTracking() // We don't plan to modify the user, so no need to track it.
            .SingleOrDefaultAsync(u => u.ApiTokenHash == tokenHash);
    }
}