using SecureApi.Security;
using Xunit;

namespace SecureApi.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void HashPassword_GeneraHashYSaltDistintosCadaVez()
    {
        var (hash1, salt1) = PasswordHasher.HashPassword("MiPassword123!");
        var (hash2, salt2) = PasswordHasher.HashPassword("MiPassword123!");

        // Misma contraseña, pero salt aleatoria distinta -> hashes distintos.
        Assert.NotEqual(salt1, salt2);
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void VerifyPassword_ConLaContraseñaCorrecta_DevuelveTrue()
    {
        var (hash, salt) = PasswordHasher.HashPassword("MiPassword123!");

        bool esValida = PasswordHasher.VerifyPassword("MiPassword123!", hash, salt);

        Assert.True(esValida);
    }

    [Fact]
    public void VerifyPassword_ConLaContraseñaIncorrecta_DevuelveFalse()
    {
        var (hash, salt) = PasswordHasher.HashPassword("MiPassword123!");

        bool esValida = PasswordHasher.VerifyPassword("OtraCosa", hash, salt);

        Assert.False(esValida);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("una-contraseña-bastante-mas-larga-que-lo-normal")]
    public void VerifyPassword_FuncionaSinImportarLaLongitud(string password)
    {
        var (hash, salt) = PasswordHasher.HashPassword(password);

        Assert.True(PasswordHasher.VerifyPassword(password, hash, salt));
        Assert.False(PasswordHasher.VerifyPassword(password + "x", hash, salt));
    }
}
