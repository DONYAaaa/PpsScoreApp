using System.Security.Cryptography;

namespace PpsScoreApp.Services;

/// <summary>Хеширование паролей по PBKDF2 (SHA-256). Без внешних зависимостей.</summary>
public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algo = HashAlgorithmName.SHA256;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algo, KeySize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string hash)
    {
        var parts = hash.Split('.', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations)) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[1]);
            var key = Convert.FromBase64String(parts[2]);
            var test = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algo, key.Length);
            return CryptographicOperations.FixedTimeEquals(test, key);
        }
        catch
        {
            return false;
        }
    }
}
