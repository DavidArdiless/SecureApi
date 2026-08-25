using System.ComponentModel.DataAnnotations;
using SecureApi.Dtos;
using Xunit;

namespace SecureApi.Tests;

public class ValidationTests
{
    private static bool EsValido(object dto, out List<ValidationResult> errores)
    {
        errores = new List<ValidationResult>();
        var context = new ValidationContext(dto);
        return Validator.TryValidateObject(dto, context, errores, validateAllProperties: true);
    }

    [Theory]
    [InlineData("30-71234567-4", true)]
    [InlineData("30712345674", false)]     // sin guiones
    [InlineData("XX-XXXXXXXX-X", false)]   // no son dígitos
    [InlineData("", false)]                // vacío
    public void ProveedorRequest_ValidaElFormatoDeCuit(string cuit, bool esperadoValido)
    {
        var dto = new ProveedorRequest
        {
            RazonSocial = "Empresa de Prueba",
            Cuit = cuit,
            Email = "contacto@empresa.com",
            Rubro = "Test",
        };

        bool esValido = EsValido(dto, out _);

        Assert.Equal(esperadoValido, esValido);
    }

    [Fact]
    public void ProveedorRequest_RechazaEmailConFormatoInvalido()
    {
        var dto = new ProveedorRequest
        {
            RazonSocial = "Empresa de Prueba",
            Cuit = "30-71234567-4",
            Email = "esto-no-es-un-email",
            Rubro = "Test",
        };

        bool esValido = EsValido(dto, out var errores);

        Assert.False(esValido);
        Assert.Contains(errores, e => e.MemberNames.Contains(nameof(ProveedorRequest.Email)));
    }

    [Fact]
    public void LoginRequest_RechazaUsuarioVacio()
    {
        var dto = new LoginRequest { NombreUsuario = "", Password = "algo" };

        bool esValido = EsValido(dto, out _);

        Assert.False(esValido);
    }
}
