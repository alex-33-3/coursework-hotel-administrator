using System.Security.Cryptography;
using System.Text;

namespace Hotel.Core.Security;

/// <summary>
/// Хеширование паролей: PBKDF2 (SHA-256, 100 000 итераций) со случайной солью 16 байт.
/// В файле хранятся хеш и соль в Base64, сам пароль нигде не сохраняется.
/// </summary>
public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public static (string Hash, string Salt) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        return (Convert.ToBase64String(Derive(password, salt)), Convert.ToBase64String(salt));
    }

    public static bool Verify(string password, string hash, string salt)
    {
        try
        {
            var expected = Convert.FromBase64String(hash);
            var actual = Derive(password, Convert.FromBase64String(salt));
            // Сравнение за постоянное время: по длительности проверки нельзя подобрать хеш.
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] Derive(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, Algorithm, HashSize);
}
