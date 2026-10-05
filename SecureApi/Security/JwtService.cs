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
/// Importante: la criptografía NO está hecha a mano. El HMAC-SHA256 y la
/// comparación en tiempo constante salen de System.Security.Cryptography.
/// Lo implementado acá es el formato del token y las reglas de validación.
///
/// Para un sistema en producción, la recomendación real es usar la
/// librería estándar de Microsoft (más auditada, con soporte de rotación
/// de claves y JWKS). Ver README.
/// </summary>
public class JwtService
{
    /// <summary>
    /// Único algoritmo aceptado. Está fijo en el código a propósito: el
    /// campo "alg" del header NUNCA elige con qué algoritmo se verifica.
    /// Eso es lo que hace imposible el ataque de alg=none y la confusión
    /// de algoritmos (un HS256 firmado con una clave pública RSA).
    /// </summary>
    private const string SupportedAlgorithm = "HS256";

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
            ["alg"] = SupportedAlgorithm,
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

    /// <summary>
    /// Devuelve el principal si el token es válido, o null si no lo es.
    /// Nunca lanza: cualquier token malformado, vencido, con firma inválida
    /// o con claims faltantes se traduce en null, para que el endpoint
    /// conteste 401 y no un 500 que filtre detalles de implementación.
    /// </summary>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var parts = token.Split('.');
        if (parts.Length != 3) return null;

        string headerSegment = parts[0];
        string payloadSegment = parts[1];
        string signatureSegment = parts[2];

        // 1) Rechazo temprano por alg. Es defensa en profundidad: aunque el
        //    header declarara none o RS256, más abajo la firma se verifica
        //    con HS256 igual, así que el ataque no funcionaría. Pero un token
        //    que declara otro algoritmo no es un token nuestro y se descarta.
        //    Parsear el header antes de verificar la firma es seguro: ese JSON
        //    solo se usa para decir "no", nunca para confiar en él.
        if (!TryReadJsonObject(headerSegment, out var header)) return null;
        if (!TryGetString(header, "alg", out string? alg) || alg != SupportedAlgorithm) return null;

        // 2) Verificar la firma antes de confiar en cualquier dato del payload.
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

        // Comparación en tiempo constante: evita que se pueda distinguir una
        // firma casi correcta midiendo cuánto tarda la comparación.
        if (!CryptographicOperations.FixedTimeEquals(expectedSignature, actualSignature))
        {
            return null;
        }

        // 3) Recién ahora es seguro leer el payload.
        if (!TryReadJsonObject(payloadSegment, out var payload)) return null;

        // 4) Emisor: si lo generamos, también lo exigimos.
        if (!TryGetString(payload, "iss", out string? issuer) || issuer != _issuer) return null;

        // 5) Ventana de validez.
        long ahora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (!TryGetInt64(payload, "exp", out long expUnix)) return null;
        if (ahora >= expUnix) return null; // token vencido

        if (payload.ContainsKey("nbf"))
        {
            if (!TryGetInt64(payload, "nbf", out long nbfUnix)) return null;
            if (ahora < nbfUnix) return null; // todavía no es válido
        }

        // 6) Claims obligatorios. Si falta alguno, el token no sirve: mejor
        //    401 que armar un principal a medias.
        if (!TryGetString(payload, "sub", out string? sub)) return null;
        if (!TryGetString(payload, "unique_name", out string? username)) return null;
        if (!TryGetString(payload, "role", out string? role)) return null;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, sub!),
            new(ClaimTypes.Name, username!),
            new(ClaimTypes.Role, role!),
        };

        var identity = new ClaimsIdentity(claims, authenticationType: "JwtHs256");
        return new ClaimsPrincipal(identity);
    }

    private byte[] Sign(string input)
    {
        using var hmac = new HMACSHA256(_secretKey);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
    }

    private static bool TryReadJsonObject(string segment, out Dictionary<string, JsonElement> result)
    {
        result = new Dictionary<string, JsonElement>();
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Base64UrlDecode(segment));
            if (parsed is null) return false;
            result = parsed;
            return true;
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            return false;
        }
    }

    private static bool TryGetString(Dictionary<string, JsonElement> obj, string key, out string? value)
    {
        value = null;
        if (!obj.TryGetValue(key, out var element)) return false;
        if (element.ValueKind != JsonValueKind.String) return false;
        value = element.GetString();
        return !string.IsNullOrEmpty(value);
    }

    private static bool TryGetInt64(Dictionary<string, JsonElement> obj, string key, out long value)
    {
        value = 0;
        if (!obj.TryGetValue(key, out var element)) return false;
        if (element.ValueKind != JsonValueKind.Number) return false;
        return element.TryGetInt64(out value);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        string padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 1: throw new FormatException("Longitud Base64Url inválida.");
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
