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
    public void VerificarPassword_ConUsuarioInexistente_TardaLoMismoQueConUnoExistente()
    {
        // Este test es el que impide volver al cortocircuito: si alguien saca el
        // hash señuelo, el camino del usuario inexistente se vuelve instantáneo
        // y el login pasa a ser un oráculo de enumeración de usuarios.
        var store = CrearStore();
        var existente = store.BuscarPorUsuario("admin");

        // Warmup: la primera llamada paga el JIT.
        store.VerificarPassword(existente, "x");
        store.VerificarPassword(null, "x");

        TimeSpan conUsuario = Medir(() => store.VerificarPassword(existente, "incorrecta"));
        TimeSpan sinUsuario = Medir(() => store.VerificarPassword(null, "incorrecta"));

        // Comparación relativa, no un umbral en ms: así no depende de lo rápida
        // que sea la máquina donde corre el test.
        Assert.True(
            sinUsuario > conUsuario * 0.5,
            $"El camino del usuario inexistente tardó {sinUsuario.TotalMilliseconds:F1} ms " +
            $"contra {conUsuario.TotalMilliseconds:F1} ms del existente: la diferencia " +
            "permite enumerar usuarios.");
    }

    // ---------- Helpers ----------

    private static UsuarioStore CrearStore() =>
        new(Config(("Seed:AdminPassword", "CambiarEsta123!")), new EntornoFalso("Development"));

    private static TimeSpan Medir(Action accion)
    {
        const int repeticiones = 3;
        var reloj = Stopwatch.StartNew();
        for (int i = 0; i < repeticiones; i++) accion();
        reloj.Stop();
        return reloj.Elapsed / repeticiones;
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
