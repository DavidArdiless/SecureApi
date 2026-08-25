using System.ComponentModel.DataAnnotations;

namespace SecureApi.Dtos;

/// <summary>
/// Lo que el cliente manda para crear o actualizar un proveedor.
/// Las validaciones acá evitan que basura llegue a la capa de datos.
/// </summary>
public class ProveedorRequest
{
    [Required(ErrorMessage = "La razón social es obligatoria.")]
    [StringLength(150, MinimumLength = 2)]
    public string RazonSocial { get; set; } = string.Empty;

    [Required(ErrorMessage = "El CUIT es obligatorio.")]
    [RegularExpression(@"^\d{2}-\d{8}-\d{1}$", ErrorMessage = "El CUIT debe tener el formato XX-XXXXXXXX-X.")]
    public string Cuit { get; set; } = string.Empty;

    [Required(ErrorMessage = "El email es obligatorio.")]
    [EmailAddress(ErrorMessage = "El email no tiene un formato válido.")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "El teléfono no tiene un formato válido.")]
    public string? Telefono { get; set; }

    [Required(ErrorMessage = "El rubro es obligatorio.")]
    [StringLength(80, MinimumLength = 2)]
    public string Rubro { get; set; } = string.Empty;
}

/// <summary>
/// Lo que la API devuelve. Separado del modelo de dominio a propósito:
/// así el contrato público no se rompe si el modelo interno cambia.
/// </summary>
public class ProveedorResponse
{
    public int Id { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public string Cuit { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string Rubro { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime FechaAlta { get; set; }
}
