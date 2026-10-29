using System.Security.Cryptography;
using System.Text;

namespace HsClassifier.Infrastructure.Services;

/// <summary>
/// PBKDF2-SHA256 with a per-user random salt (stdlib only).
/// Format: pbkdf2${iterations}${saltB64}${hashB64}.
/// Legacy SHA-256 hashes (old AuthController) still verify so existing users can log in.
/// </summary>
public static class PasswordHashing
{
    private const int Iterations = 210_000; // OWASP 2023 guidance for PBKDF2-HMAC-SHA256
    private const int SaltSize = 16, HashSize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts is ["pbkdf2", var iter, var saltB64, var hashB64] && int.TryParse(iter, out var n))
        {
            var expected = Convert.FromBase64String(hashB64);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(saltB64), n,
                HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        // Legacy: SHA-256(password + static salt)
        var legacy = SHA256.HashData(Encoding.UTF8.GetBytes(password + "hs-salt-v2"));
        return CryptographicOperations.FixedTimeEquals(legacy, TryBase64(stored));
    }

    private static byte[] TryBase64(string s)
    {
        try { return Convert.FromBase64String(s); } catch (FormatException) { return []; }
    }
}
