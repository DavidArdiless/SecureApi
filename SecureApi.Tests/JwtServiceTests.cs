using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SecureApi.Security;
using Xunit;

namespace SecureApi.Tests;

public class JwtServiceTests
{
    private const string Secreto = "clave-secreta-de-pruebas-minimo-32-caracteres!!";
    private const string Emisor = "SecureApi.Tests";

    private static JwtService CrearServicio(TimeSpan? lifetime = null) =>
        new(Secreto, Emisor, lifetime ?? TimeSpan.FromMinutes(5));

    [Fact]
    public void GenerarYValidarToken_DevuelveLosClaimsCorrectos()
    {
        var jwt = CrearServicio();

        string token = jwt.GenerateToken(userId: 42, username: "admin", role: "Admin");
        ClaimsPrincipal? principal = jwt.ValidateToken(token);

        Assert.NotNull(principal);
        Assert.Equal("42", principal!.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("admin", principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("Admin", principal.FindFirstValue(ClaimTypes.Role));
    }

    [Fact]
    public void ValidarToken_ConFirmaAlterada_DevuelveNull()
    {
        var jwt = CrearServicio();
        string token = jwt.GenerateToken(1, "admin", "Admin");

        // Le cambio el último caracter de la firma a propósito.
        char ultimo = token[^1];
        char reemplazo = ultimo == 'a' ? 'b' : 'a';
        string tokenAlterado = token[..^1] + reemplazo;

        Assert.Null(jwt.ValidateToken(tokenAlterado));
    }

    [Fact]
    public void ValidarToken_ConTokenExpirado_DevuelveNull()
    {
        var jwt = CrearServicio(TimeSpan.FromSeconds(-1)); // ya vencido al generarlo
        string token = jwt.GenerateToken(1, "admin", "Admin");

        Assert.Null(jwt.ValidateToken(token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-jwt")]
    [InlineData("solo.dospartes")]
    public void ValidarToken_ConFormatoInvalido_DevuelveNull(string tokenInvalido)
    {
        var jwt = CrearServicio();

        Assert.Null(jwt.ValidateToken(tokenInvalido));
    }

    [Fact]
    public void ConstructorLanzaExcepcion_SiElSecretoEsMuyCorto()
    {
        Assert.Throws<ArgumentException>(() => new JwtService("corto", "SecureApi", TimeSpan.FromMinutes(5)));
    }

    // ---------- Ataques clásicos contra la validación de JWT ----------

    [Fact]
    public void ValidarToken_ConAlgNone_DevuelveNull()
    {
        // El ataque clásico: el atacante pone "alg": "none", borra la firma y
        // espera que el validador le crea al header.
        var jwt = CrearServicio();
        string header = Base64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        string payload = Base64Url(PayloadJson(exp: Ahora + 300));

        Assert.Null(jwt.ValidateToken($"{header}.{payload}."));
    }

    [Fact]
    public void ValidarToken_ConAlgDistintoDeHs256_DevuelveNull_AunqueLaFirmaSeaValida()
    {
        // Firma HMAC-SHA256 correcta, pero el header declara RS256. Se rechaza
        // igual: el algoritmo lo decide el código, no el token.
        var jwt = CrearServicio();
        string header = Base64Url("{\"alg\":\"RS256\",\"typ\":\"JWT\"}");
        string payload = Base64Url(PayloadJson(exp: Ahora + 300));

        Assert.Null(jwt.ValidateToken(Firmar(header, payload)));
    }

    [Fact]
    public void ValidarToken_ConEmisorDistinto_DevuelveNull()
    {
        var jwt = CrearServicio();
        string header = Base64Url("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        string payload = Base64Url(PayloadJson(exp: Ahora + 300, iss: "otro-emisor"));

        Assert.Null(jwt.ValidateToken(Firmar(header, payload)));
    }

    [Theory]
    [InlineData("sub")]
    [InlineData("unique_name")]
    [InlineData("role")]
    public void ValidarToken_SinUnClaimObligatorio_DevuelveNull(string claimFaltante)
    {
        // Antes esto tiraba KeyNotFoundException y el endpoint respondía 500.
        var jwt = CrearServicio();
        string header = Base64Url("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        string payload = Base64Url(PayloadJson(exp: Ahora + 300, omitir: claimFaltante));

        Assert.Null(jwt.ValidateToken(Firmar(header, payload)));
    }

    [Fact]
    public void ValidarToken_ConExpNoNumerico_DevuelveNull()
    {
        var jwt = CrearServicio();
        string header = Base64Url("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        string payload = Base64Url(
            "{\"sub\":\"1\",\"unique_name\":\"admin\",\"role\":\"Admin\",\"iss\":\"" + Emisor + "\",\"exp\":\"no-es-un-numero\"}");

        Assert.Null(jwt.ValidateToken(Firmar(header, payload)));
    }

    [Fact]
    public void ValidarToken_ConNbfEnElFuturo_DevuelveNull()
    {
        var jwt = CrearServicio();
        string header = Base64Url("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        string payload = Base64Url(PayloadJson(exp: Ahora + 600, nbf: Ahora + 300));

        Assert.Null(jwt.ValidateToken(Firmar(header, payload)));
    }

    // ---------- Helpers ----------

    private static long Ahora => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static string PayloadJson(long exp, string? iss = null, long? nbf = null, string? omitir = null)
    {
        var claims = new Dictionary<string, object>
        {
            ["sub"] = "1",
            ["unique_name"] = "admin",
            ["role"] = "Admin",
            ["iss"] = iss ?? Emisor,
            ["exp"] = exp,
        };

        if (nbf is not null) claims["nbf"] = nbf.Value;
        if (omitir is not null) claims.Remove(omitir);

        return JsonSerializer.Serialize(claims);
    }

    private static string Firmar(string headerSegment, string payloadSegment)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secreto));
        byte[] firma = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{headerSegment}.{payloadSegment}"));
        return $"{headerSegment}.{payloadSegment}.{Base64Url(firma)}";
    }

    private static string Base64Url(string texto) => Base64Url(Encoding.UTF8.GetBytes(texto));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
