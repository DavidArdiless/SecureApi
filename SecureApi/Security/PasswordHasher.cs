using System.Security.Cryptography;

namespace SecureApi.Security;

/// <summary>
/// Hashing de contraseñas con PBKDF2-HMACSHA256, usando únicamente
/// System.Security.Cryptography (sin paquetes externos como
/// Microsoft.AspNetCore.Identity). Nunca se guarda la contraseña en
/// texto plano, ni siquiera en memoria más tiempo del necesario.
/// </summary>
public static class PasswordHasher
{
    private const int SaltSizeBytes = 16;
    private const int KeySizeBytes = 32;
    private const int Iterations = 210_000; // recomendación OWASP 2024+ para PBKDF2-SHA256

    public static (string Hash, string Salt) HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySizeBytes);
        return (Convert.ToBase64String(key), Convert.ToBase64String(salt));
    }

    public static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        byte[] salt = Convert.FromBase64String(storedSalt);
        byte[] expectedKey = Convert.FromBase64String(storedHash);
        byte[] actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySizeBytes);

        // Comparación en tiempo constante: evita timing attacks al comparar hashes.
        return CryptographicOperations.FixedTimeEquals(expectedKey, actualKey);
    }
}
