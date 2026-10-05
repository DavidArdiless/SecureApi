using System.Collections.Concurrent;
using SecureApi.Models;

namespace SecureApi.Data;

/// <summary>
/// Repositorio en memoria, pensado para que este proyecto corra sin
/// configurar una base de datos. La interfaz está separada del uso
/// (ver Program.cs) para poder reemplazar esto por EF Core + SQL Server
/// más adelante sin tocar los endpoints — el mismo patrón que ya se usa
/// en el sistema real de proveedores del Ministerio.
/// </summary>
public class ProveedorStore
{
    private readonly ConcurrentDictionary<int, Proveedor> _proveedores = new();
    private int _nextId = 0;

    public ProveedorStore()
    {
        Agregar(new Proveedor
        {
            RazonSocial = "Insumos Educativos SRL",
            Cuit = "30-71234567-4",
            Email = "contacto@insumoseducativos.com.ar",
            Telefono = "381-4123456",
            Rubro = "Papelería y material didáctico",
        });

        Agregar(new Proveedor
        {
            RazonSocial = "Construcciones del Norte SA",
            Cuit = "30-70987654-1",
            Email = "administracion@construccionesdelnorte.com.ar",
            Telefono = "381-4987654",
            Rubro = "Infraestructura escolar",
        });
    }

    public IReadOnlyCollection<Proveedor> ObtenerTodos() =>
        _proveedores.Values.OrderBy(p => p.Id).ToList();

    public Proveedor? ObtenerPorId(int id) =>
        _proveedores.TryGetValue(id, out var proveedor) ? proveedor : null;

    public Proveedor Agregar(Proveedor proveedor)
    {
        proveedor.Id = Interlocked.Increment(ref _nextId);
        proveedor.FechaAlta = DateTime.UtcNow;
        _proveedores[proveedor.Id] = proveedor;
        return proveedor;
    }

    /// <summary>
    /// Actualiza los campos editables y devuelve el proveedor tal como quedó
    /// guardado, o null si el id no existe.
    ///
    /// Preserva a propósito Id, FechaAlta y Activo: no viajan en el request de
    /// actualización, así que pisarlos con los valores por defecto de un
    /// Proveedor nuevo haría que un PUT para corregir un teléfono también
    /// reseteara la fecha de alta y reactivara a un proveedor dado de baja.
    /// </summary>
    public Proveedor? Actualizar(int id, Proveedor datosActualizados)
    {
        if (!_proveedores.TryGetValue(id, out var existente)) return null;

        datosActualizados.Id = id;
        datosActualizados.FechaAlta = existente.FechaAlta;
        datosActualizados.Activo = existente.Activo;

        _proveedores[id] = datosActualizados;
        return datosActualizados;
    }

    public bool Eliminar(int id) => _proveedores.TryRemove(id, out _);
}
