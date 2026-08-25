namespace SecureApi.Models;

/// <summary>
/// Entidad de dominio: un proveedor registrado ante el organismo.
/// Inspirado en el sistema de gestión de proveedores real construido
/// en el Ministerio de Educación de Tucumán, recreado acá como
/// proyecto de portfolio.
/// </summary>
public class Proveedor
{
    public int Id { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public string Cuit { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string Rubro { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;
}
