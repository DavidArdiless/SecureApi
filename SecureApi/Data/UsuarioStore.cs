using SecureApi.Models;
using SecureApi.Security;

namespace SecureApi.Data;

/// <summary>
/// Store de usuarios en memoria, con un admin seedeado al arrancar.
/// La contraseña se lee de configuración y nunca se guarda en texto plano:
/// se hashea una sola vez al construir el store. Fuera de Development es
/// obligatoria; si falta, la app no arranca.
/// </summary>
public class UsuarioStore
{
    private readonly List<Usuario> _usuarios = new();

    // Hash señuelo: se usa para gastar el mismo tiempo de CPU cuando el
    // usuario pedido no existe. Se genera con una contraseña aleatoria, así
    // que ninguna entrada del cliente puede coincidir con él.
    private readonly string _hashSenuelo;
    private readonly string _saltSenuelo;

    public UsuarioStore(IConfiguration configuration, IHostEnvironment environment)
    {
        string seedUser = configuration["Seed:AdminUsername"] ?? "admin";
        string? seedPasswordConfigurada = configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(seedPasswordConfigurada) && !environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Falta la contraseña del admin. Configurá SecureApi__Seed__AdminPassword " +
                "(o Seed__AdminPassword). La API no arranca con la contraseña de " +
                "desarrollo fuera del entorno Development.");
        }

        string seedPassword = seedPasswordConfigurada ?? "CambiarEsta123!";

        var (hash, salt) = PasswordHasher.HashPassword(seedPassword);
        _usuarios.Add(new Usuario
        {
            Id = 1,
            NombreUsuario = seedUser,
            PasswordHash = hash,
            PasswordSalt = salt,
            Rol = "Admin",
        });

        (_hashSenuelo, _saltSenuelo) = PasswordHasher.HashPassword(Guid.NewGuid().ToString());
    }

    public Usuario? BuscarPorUsuario(string nombreUsuario) =>
        _usuarios.FirstOrDefault(u => string.Equals(u.NombreUsuario, nombreUsuario, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Verifica la contraseña de un usuario. Si el usuario es null igual corre
    /// PBKDF2, contra el hash señuelo, y recién después devuelve false.
    ///
    /// Por qué: cortar antes cuando el usuario no existe convierte el login en
    /// un oráculo de enumeración de usuarios. Con 210.000 iteraciones la
    /// diferencia medida era ~1,5 ms contra ~22 ms, perfectamente distinguible
    /// desde afuera aunque el mensaje de error sea el mismo.
    /// </summary>
    public bool VerificarPassword(Usuario? usuario, string password)
    {
        if (usuario is null)
        {
            PasswordHasher.VerifyPassword(password, _hashSenuelo, _saltSenuelo);
            return false;
        }

        return PasswordHasher.VerifyPassword(password, usuario.PasswordHash, usuario.PasswordSalt);
    }
}
