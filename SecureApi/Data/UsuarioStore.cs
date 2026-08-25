using SecureApi.Models;
using SecureApi.Security;

namespace SecureApi.Data;

/// <summary>
/// Store de usuarios en memoria, con un admin seedeado al arrancar.
/// La contraseña por default se lee de configuración (ver appsettings /
/// variables de entorno) y nunca se guarda en texto plano: se hashea
/// una sola vez al construir el store.
/// </summary>
public class UsuarioStore
{
    private readonly List<Usuario> _usuarios = new();

    public UsuarioStore(IConfiguration configuration)
    {
        string seedUser = configuration["Seed:AdminUsername"] ?? "admin";
        string seedPassword = configuration["Seed:AdminPassword"] ?? "CambiarEsta123!";

        var (hash, salt) = PasswordHasher.HashPassword(seedPassword);
        _usuarios.Add(new Usuario
        {
            Id = 1,
            NombreUsuario = seedUser,
            PasswordHash = hash,
            PasswordSalt = salt,
            Rol = "Admin",
        });
    }

    public Usuario? BuscarPorUsuario(string nombreUsuario) =>
        _usuarios.FirstOrDefault(u => string.Equals(u.NombreUsuario, nombreUsuario, StringComparison.OrdinalIgnoreCase));
}
