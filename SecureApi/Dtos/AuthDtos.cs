using System.ComponentModel.DataAnnotations;

namespace SecureApi.Dtos;

public class LoginRequest
{
    [Required(ErrorMessage = "El usuario es obligatorio.")]
    public string NombreUsuario { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiraUtc { get; set; }
}
