using SecureApi.Data;
using SecureApi.Models;
using Xunit;

namespace SecureApi.Tests;

public class ProveedorStoreTests
{
    [Fact]
    public void ObtenerTodos_AlCrearElStore_TraeLosDosProveedoresSeed()
    {
        var store = new ProveedorStore();

        var todos = store.ObtenerTodos();

        Assert.Equal(2, todos.Count);
    }

    [Fact]
    public void Agregar_AsignaUnIdNuevoYSecuencial()
    {
        var store = new ProveedorStore();

        var nuevo = store.Agregar(new Proveedor
        {
            RazonSocial = "Test SRL",
            Cuit = "30-11111111-1",
            Email = "test@test.com",
            Rubro = "Test",
        });

        Assert.Equal(3, nuevo.Id); // los 2 del seed ya ocuparon 1 y 2
        Assert.Equal(3, store.ObtenerTodos().Count);
    }

    [Fact]
    public void ObtenerPorId_ConIdInexistente_DevuelveNull()
    {
        var store = new ProveedorStore();

        Assert.Null(store.ObtenerPorId(999));
    }

    [Fact]
    public void Actualizar_ConIdExistente_PisaLosCamposEditables()
    {
        var store = new ProveedorStore();

        var actualizado = store.Actualizar(1, new Proveedor
        {
            RazonSocial = "Nombre Actualizado SRL",
            Cuit = "30-99999999-9",
            Email = "nuevo@test.com",
            Rubro = "Otro rubro",
        });

        Assert.NotNull(actualizado);
        Assert.Equal(1, actualizado!.Id);
        Assert.Equal("Nombre Actualizado SRL", store.ObtenerPorId(1)!.RazonSocial);
    }

    [Fact]
    public void Actualizar_PreservaFechaAltaYEstado()
    {
        var store = new ProveedorStore();
        var original = store.ObtenerPorId(1)!;
        DateTime fechaAltaOriginal = original.FechaAlta;

        // Un proveedor dado de baja no tiene que revivir por un PUT que solo
        // corrige el teléfono.
        original.Activo = false;

        var actualizado = store.Actualizar(1, new Proveedor
        {
            RazonSocial = original.RazonSocial,
            Cuit = original.Cuit,
            Email = original.Email,
            Telefono = "381-4000000",
            Rubro = original.Rubro,
        });

        Assert.NotNull(actualizado);
        Assert.Equal(fechaAltaOriginal, actualizado!.FechaAlta);
        Assert.False(actualizado.Activo);
        Assert.Equal("381-4000000", actualizado.Telefono);
    }

    [Fact]
    public void Actualizar_ConIdInexistente_DevuelveNull()
    {
        var store = new ProveedorStore();

        var actualizado = store.Actualizar(999, new Proveedor { RazonSocial = "X", Cuit = "X", Email = "x@x.com", Rubro = "X" });

        Assert.Null(actualizado);
    }

    [Fact]
    public void Eliminar_ConIdExistente_LoSacaDelStore()
    {
        var store = new ProveedorStore();

        bool eliminado = store.Eliminar(1);

        Assert.True(eliminado);
        Assert.Null(store.ObtenerPorId(1));
        Assert.Single(store.ObtenerTodos());
    }
}
