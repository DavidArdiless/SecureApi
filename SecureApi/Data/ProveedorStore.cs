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

    public bool Actualizar(int id, Proveedor datosActualizados)
    {
        if (!_proveedores.ContainsKey(id)) return false;

        datosActualizados.Id = id;
        _proveedores[id] = datosActualizados;
        return true;
    }

    public bool Eliminar(int id) => _proveedores.TryRemove(id, out _);
}
