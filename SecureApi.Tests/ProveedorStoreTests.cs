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
    public void Actualizar_ConIdExistente_PisaLosDatosYDevuelveTrue()
    {
        var store = new ProveedorStore();

        bool actualizado = store.Actualizar(1, new Proveedor
        {
            RazonSocial = "Nombre Actualizado SRL",
            Cuit = "30-99999999-9",
            Email = "nuevo@test.com",
            Rubro = "Otro rubro",
        });

        Assert.True(actualizado);
        Assert.Equal("Nombre Actualizado SRL", store.ObtenerPorId(1)!.RazonSocial);
    }

    [Fact]
    public void Actualizar_ConIdInexistente_DevuelveFalse()
    {
        var store = new ProveedorStore();

        bool actualizado = store.Actualizar(999, new Proveedor { RazonSocial = "X", Cuit = "X", Email = "x@x.com", Rubro = "X" });

        Assert.False(actualizado);
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
