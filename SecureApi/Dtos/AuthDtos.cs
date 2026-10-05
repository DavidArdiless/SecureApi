using System.ComponentModel.DataAnnotations;

namespace SecureApi.Dtos;

public class LoginRequest
{
    // Los largos máximos acotan lo que llega al hasher: no hay razón para
    // aceptar un usuario o una contraseña de megabytes.
    [Required(ErrorMessage = "El usuario es obligatorio.")]
    [StringLength(64, ErrorMessage = "El usuario no puede superar los 64 caracteres.")]
    public string NombreUsuario { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(256, ErrorMessage = "La contraseña no puede superar los 256 caracteres.")]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiraUtc { get; set; }
}
