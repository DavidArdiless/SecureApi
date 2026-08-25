using System.Security.Claims;
using SecureApi.Security;
using Xunit;

namespace SecureApi.Tests;

public class JwtServiceTests
{
    private static JwtService CrearServicio(TimeSpan? lifetime = null) =>
        new("clave-secreta-de-pruebas-minimo-32-caracteres!!", "SecureApi.Tests", lifetime ?? TimeSpan.FromMinutes(5));

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
}
