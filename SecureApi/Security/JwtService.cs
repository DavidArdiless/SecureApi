using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SecureApi.Security;

/// <summary>
/// Implementación propia y mínima de JWT (HS256), sin depender de
/// System.IdentityModel.Tokens.Jwt ni Microsoft.AspNetCore.Authentication.JwtBearer.
///
/// Por qué está hecho a mano: para este proyecto de portfolio, priorizo
/// no depender de paquetes NuGet externos y dejar a la vista exactamente
/// cómo funciona un JWT por dentro (header.payload.signature, todo en
/// Base64Url, firmado con HMAC-SHA256).
///
/// Para un sistema en producción, la recomendación real es usar la
/// librería estándar de Microsoft (más auditada, con más protecciones
/// contra ataques conocidos como algorithm confusion). Ver README.
/// </summary>
public class JwtService
{
    private readonly byte[] _secretKey;
    private readonly string _issuer;
    private readonly TimeSpan _tokenLifetime;

    public JwtService(string secret, string issuer, TimeSpan tokenLifetime)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            throw new ArgumentException("El secreto JWT debe tener al menos 32 caracteres.", nameof(secret));
        }

        _secretKey = Encoding.UTF8.GetBytes(secret);
        _issuer = issuer;
        _tokenLifetime = tokenLifetime;
    }

    public string GenerateToken(int userId, string username, string role)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.Add(_tokenLifetime);

        var header = new Dictionary<string, object>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT",
        };

        var payload = new Dictionary<string, object>
        {
            ["sub"] = userId.ToString(),
            ["unique_name"] = username,
            ["role"] = role,
            ["iss"] = _issuer,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["nbf"] = now.ToUnixTimeSeconds(),
            ["exp"] = expires.ToUnixTimeSeconds(),
        };

        string headerSegment = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header));
        string payloadSegment = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload));
        string signingInput = $"{headerSegment}.{payloadSegment}";
        string signature = Base64UrlEncode(Sign(signingInput));

        return $"{signingInput}.{signature}";
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var parts = token.Split('.');
        if (parts.Length != 3) return null;

        string headerSegment = parts[0];
        string payloadSegment = parts[1];
        string signatureSegment = parts[2];

        // 1) Verificar firma antes de confiar en cualquier dato del payload.
        byte[] expectedSignature = Sign($"{headerSegment}.{payloadSegment}");
        byte[] actualSignature;
        try
        {
            actualSignature = Base64UrlDecode(signatureSegment);
        }
        catch (FormatException)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(expectedSignature, actualSignature))
        {
            return null;
        }

        // 2) Recién ahora es seguro leer el payload.
        Dictionary<string, JsonElement>? payload;
        try
        {
            payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Base64UrlDecode(payloadSegment));
        }
        catch
        {
            return null;
        }

        if (payload is null) return null;

        if (!payload.TryGetValue("exp", out var expElement)) return null;
        long expUnix = expElement.GetInt64();
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= expUnix) return null; // token vencido

        if (payload.TryGetValue("nbf", out var nbfElement))
        {
            long nbfUnix = nbfElement.GetInt64();
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() < nbfUnix) return null; // todavía no es válido
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, payload["sub"].GetString() ?? string.Empty),
            new(ClaimTypes.Name, payload["unique_name"].GetString() ?? string.Empty),
            new(ClaimTypes.Role, payload["role"].GetString() ?? string.Empty),
        };

        var identity = new ClaimsIdentity(claims, authenticationType: "JwtHs256");
        return new ClaimsPrincipal(identity);
    }

    private byte[] Sign(string input)
    {
        using var hmac = new HMACSHA256(_secretKey);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        string padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
