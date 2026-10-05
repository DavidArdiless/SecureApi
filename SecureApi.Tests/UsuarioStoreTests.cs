using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SecureApi.Data;
using Xunit;

namespace SecureApi.Tests;

public class UsuarioStoreTests
{
    [Fact]
    public void Constructor_FueraDeDevelopmentSinContrasenaConfigurada_NoArranca()
    {
        // Arrancar en producción con la contraseña de desarrollo (que está en un
        // repo público) es peor que no arrancar.
        var ex = Assert.Throws<InvalidOperationException>(
            () => new UsuarioStore(Config(), new EntornoFalso("Production")));

        Assert.Contains("Seed__AdminPassword", ex.Message);
    }

    [Fact]
    public void Constructor_FueraDeDevelopmentConContrasenaConfigurada_Arranca()
    {
        var store = new UsuarioStore(
            Config(("Seed:AdminPassword", "una-contrasena-de-produccion")),
            new EntornoFalso("Production"));

        Assert.NotNull(store.BuscarPorUsuario("admin"));
    }

    [Fact]
    public void Constructor_EnDevelopmentSinContrasena_UsaElDefaultYArranca()
    {
        var store = new UsuarioStore(Config(), new EntornoFalso("Development"));

        Assert.True(store.VerificarPassword(store.BuscarPorUsuario("admin"), "CambiarEsta123!"));
    }

    [Fact]
    public void VerificarPassword_ConUsuarioInexistente_DevuelveFalse()
    {
        var store = CrearStore();

        Assert.False(store.VerificarPassword(null, "cualquier-cosa"));
    }

    [Fact]
    public void VerificarPassword_ConContrasenaIncorrecta_DevuelveFalse()
    {
        var store = CrearStore();

        Assert.False(store.VerificarPassword(store.BuscarPorUsuario("admin"), "incorrecta"));
    }

    [Fact]
    public void VerificarPassword_ConUsuarioInexistente_IgualCorrePbkdf2()
    {
        // Este test es el que impide volver al cortocircuito: si alguien saca el
        // hash señuelo, el camino del usuario inexistente se vuelve instantáneo
        // y el login pasa a ser un oráculo de enumeración de usuarios.
        //
        // La aserción es una cota inferior de un solo lado, y es a propósito.
        // Comparar contra el tiempo del usuario existente parece más directo,
        // pero es frágil: en un runner cargado esa medición se infla y el test
        // falla sin que haya ninguna regresión. El ruido solo puede hacer las
        // mediciones MÁS lentas, nunca más rápidas, así que un piso no da
        // falsos positivos.
        //
        // Márgenes: 210.000 iteraciones de PBKDF2 tardan ~20 ms acá y más en
        // CI; un cortocircuito tardaría microsegundos. El piso de 2 ms queda
        // 10x por debajo del costo real y ~100x por encima del cortocircuito.
        var store = CrearStore();

        // Warmup: la primera llamada paga el JIT.
        store.VerificarPassword(null, "x");

        TimeSpan masRapida = MedirMasRapida(() => store.VerificarPassword(null, "incorrecta"));

        Assert.True(
            masRapida > TimeSpan.FromMilliseconds(2),
            $"El camino del usuario inexistente tardó {masRapida.TotalMilliseconds:F3} ms: " +
            "demasiado poco para haber corrido PBKDF2, así que el tiempo de " +
            "respuesta del login revela qué usuarios existen.");
    }

    // ---------- Helpers ----------

    private static UsuarioStore CrearStore() =>
        new(Config(("Seed:AdminPassword", "CambiarEsta123!")), new EntornoFalso("Development"));

    /// <summary>
    /// Devuelve la más rápida de varias corridas. El mínimo es el estimador
    /// correcto acá: filtra el ruido del scheduler, que solo puede sumar
    /// tiempo, y deja la cota más conservadora para la aserción.
    /// </summary>
    private static TimeSpan MedirMasRapida(Action accion, int repeticiones = 3)
    {
        var masRapida = TimeSpan.MaxValue;

        for (int i = 0; i < repeticiones; i++)
        {
            var reloj = Stopwatch.StartNew();
            accion();
            reloj.Stop();
            if (reloj.Elapsed < masRapida) masRapida = reloj.Elapsed;
        }

        return masRapida;
    }

    private static IConfiguration Config(params (string Clave, string Valor)[] valores) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(valores.Select(v => new KeyValuePair<string, string?>(v.Clave, v.Valor)))
            .Build();

    private sealed class EntornoFalso : IHostEnvironment
    {
        public EntornoFalso(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "SecureApi.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
