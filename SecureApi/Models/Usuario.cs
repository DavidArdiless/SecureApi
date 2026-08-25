namespace SecureApi.Models;

/// <summary>
/// Usuario del sistema. La contraseña nunca se guarda en texto plano:
/// se almacena el hash (PBKDF2) y la sal usados para generarlo.
/// </summary>
public class Usuario
{
    public int Id { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string Rol { get; set; } = "Admin";
}
