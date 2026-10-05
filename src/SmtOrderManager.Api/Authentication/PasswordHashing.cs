using Microsoft.AspNetCore.Identity;

namespace SmtOrderManager.Api.Authentication;

/// <summary>
/// Hashes and verifies passwords with the password hasher of ASP.NET Core Identity (PBKDF2 with
/// a random salt), without the rest of Identity.
/// </summary>
internal static class PasswordHashing
{
    // The hasher takes a user object for custom implementations; the default ignores it.
    private static readonly object User = new();
    private static readonly PasswordHasher<object> Hasher = new();

    public static string Hash(string password) => Hasher.HashPassword(User, password);

    public static bool Verify(string passwordHash, string password) =>
        IsWellFormed(passwordHash)
        && Hasher.VerifyHashedPassword(User, passwordHash, password) != PasswordVerificationResult.Failed;

    /// <summary>
    /// Whether the text looks like a hash of the current format: Base64 with the version marker
    /// 0x01. Checked at startup, so a mistyped hash is reported instead of failing every login.
    /// </summary>
    public static bool IsWellFormed(string passwordHash)
    {
        var bytes = new byte[passwordHash.Length];

        return Convert.TryFromBase64String(passwordHash, bytes, out var length)
            && length > 13
            && bytes[0] == 0x01;
    }
}
