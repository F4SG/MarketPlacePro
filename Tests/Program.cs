using System.ComponentModel.DataAnnotations;
using System.Globalization;
using MarketPlacePro.Controllers;
using MarketPlacePro.Data;
using MarketPlacePro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
var passed = 0;
var failures = new List<string>();
async Task Check(string name, Func<Task> test)
{
    try { await test(); passed++; Console.WriteLine($"OK {name}"); }
    catch (Exception ex) { failures.Add(name); Console.WriteLine($"ERROR {name}: {ex}"); }
}
void Assert(bool value, string message) { if (!value) throw new Exception(message); }
void Validate(Controller controller, object model)
{
    var results = new List<ValidationResult>();
    Validator.TryValidateObject(model, new ValidationContext(model), results, true);
    foreach (var result in results)
        foreach (var member in result.MemberNames.DefaultIfEmpty(string.Empty))
            controller.ModelState.AddModelError(member, result.ErrorMessage!);
}
async Task AddDetail(MarketPlaceProContext db, long pedido = 1, long producto = 1, int cantidad = 2)
{
    var controller = new DetallePedidosController(db);
    var model = new DetallePedido { PedidoId = pedido, ProductoId = producto, Cantidad = cantidad };
    Validate(controller, model);
    Assert(await controller.Create(model) is RedirectToActionResult, "No se guardó el detalle válido.");
    db.ChangeTracker.Clear();
}

await Check("El esquema SQL Server sigue coincidiendo con la migración", () =>
{
    using var db = new MarketPlaceProContext(new DbContextOptionsBuilder<MarketPlaceProContext>()
        .UseSqlServer("Server=localhost;Database=NoSeConecta;Integrated Security=True;TrustServerCertificate=True").Options);
    Assert(!db.Database.HasPendingModelChanges(), "El cambio exige una migración no prevista.");
    return Task.CompletedTask;
});
await Check("Producto rechaza nombre vacío, precio cero, stock negativo y estado inválido", () =>
{
    var errors = new List<ValidationResult>();
    var model = new Producto { Nombre = "  ", Precio = 0, Stock = -1, Estado = "otro" };
    Validator.TryValidateObject(model, new ValidationContext(model), errors, true);
    foreach (var name in new[] { "Nombre", "Precio", "Stock", "Estado" })
        Assert(errors.Any(e => e.MemberNames.Contains(name)), $"Falta validación: {name}");
    return Task.CompletedTask;
});
await Check("Cantidades y límites de importes", () =>
{
    foreach (var model in new object[] { new DetalleCarrito { Cantidad = 0 }, new DetallePedido { Cantidad = -1 },
        new Pago { Metodo = "Efectivo", Monto = 100000000m }, new Producto { Nombre = "A", Precio = 100000000m } })
        Assert(!Validator.TryValidateObject(model, new ValidationContext(model), [], true), "Se aceptó un valor fuera de límites.");
    return Task.CompletedTask;
});
await Check("No acepta precios ni pagos con más de dos decimales", () =>
{
    foreach (var model in new object[] { new Producto { Nombre = "A", Precio = 10.001m },
        new Pago { Metodo = "Efectivo", Monto = 10.001m } })
        Assert(!Validator.TryValidateObject(model, new ValidationContext(model), [], true), "Se aceptó precisión monetaria inválida.");
    return Task.CompletedTask;
});
await Check("Categoría vacía no se persiste", async () =>
{
    await using var f = await Fixture.Create();
    var c = new CategoriasController(f.Db); var model = new Categoria { Nombre = " " };
    Validate(c, model);
    Assert(await c.Create(model) is ViewResult && !c.ModelState.IsValid, "Falta error de categoría.");
    Assert(await f.Db.Categorias.CountAsync() == 1, "Se guardó una categoría vacía.");
});
await Check("Reporte valida rango, estado y fecha extrema sin consultar la base", async () =>
{
    using var db = new MarketPlaceProContext(new DbContextOptionsBuilder<MarketPlaceProContext>()
        .UseSqlServer("Server=localhost;Database=NoSeConecta;Integrated Security=True;TrustServerCertificate=True").Options);
    foreach (var (desde, hasta, estado) in new[] {
        (new DateTime(2026,10,26),new DateTime(2026,10,25),"completada"),
        (new DateTime(2026,10,25),new DateTime(2026,10,25),"invalido"),
        (new DateTime(2026,10,25),DateTime.MaxValue,"completada") })
    {
        var c = new ReportesController(db);
        Assert(await c.VentasPorPeriodo(desde, hasta, estado) is ViewResult && !c.ModelState.IsValid,
            "No se rechazó el filtro inválido.");
    }
    var malformed = new ReportesController(db);
    malformed.ModelState.AddModelError("Desde", "Fecha inválida");
    await malformed.VentasPorPeriodo(null, null);
    Assert(!malformed.ModelState.IsValid, "Se perdió el error de conversión.");
});
await Check("Ventas incluye ambos extremos del día boliviano y distingue productos homónimos", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    var inicio = new DateTime(2026, 10, 25, 4, 0, 0, DateTimeKind.Utc);
    var fechas = new[] { inicio.AddTicks(-1), inicio, inicio.AddDays(1).AddTicks(-1), inicio.AddDays(1) };
    for (int i = 0; i < fechas.Length; i++)
    {
        var p = new Pedido { NumeroPedido = $"VENTA-{i}", UsuarioId = 1, Estado = "completada", FechaPedido = fechas[i] };
        db.Pedidos.Add(p); await db.SaveChangesAsync();
        db.DetallesPedido.Add(new DetallePedido { PedidoId = p.Id, ProductoId = i == 2 ? 2 : 1,
            NombreProducto = "Mismo nombre", PrecioUnitario = i == 2 ? 20 : 10, Cantidad = i == 2 ? 2 : 1,
            Subtotal = i == 2 ? 40 : 10 });
    }
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var c = new ReportesController(db);
    var result = (ViewResult)await c.VentasPorPeriodo(DateTime.SpecifyKind(inicio.Date, DateTimeKind.Utc), inicio.Date);
    var m = (ReporteVentasViewModel)result.Model!;
    Assert(m.TotalPedidos == 2 && m.Filas.Count == 2 && m.MontoTotal == 50, "Límites o agrupación incorrectos.");
    Assert(m.Filas.Select(x => x.ProductoId).Distinct().Count() == 2, "Se mezclaron productos homónimos.");
    var empty = (ReporteVentasViewModel)((ViewResult)await new ReportesController(db)
        .VentasPorPeriodo(new DateTime(2026,10,20),new DateTime(2026,10,20))).Model!;
    Assert(empty.TotalPedidos == 0 && empty.Filas.Count == 0 && empty.MontoTotal == 0, "Resumen vacío incorrecto.");
});
await Check("Agregar detalle actualiza subtotal y total", async () =>
{
    await using var f = await Fixture.Create(); await AddDetail(f.Db);
    var p = await f.Db.Pedidos.FindAsync(1L);
    Assert(p!.Subtotal == 20 && p.Total == 20, "Totales desactualizados.");
});
await Check("Editar cantidad conserva el precio histórico", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db; await AddDetail(db);
    var producto = await db.Productos.FindAsync(1L); producto!.Precio = 30;
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var id = await db.DetallesPedido.Select(d => d.Id).SingleAsync();
    var c = new DetallePedidosController(db);
    Assert(await c.Edit(id, new DetallePedido { Id = id, PedidoId = 1, ProductoId = 1, Cantidad = 3 })
        is RedirectToActionResult, "Falló la edición.");
    db.ChangeTracker.Clear(); var d = await db.DetallesPedido.SingleAsync(); var p = await db.Pedidos.FindAsync(1L);
    Assert(d.PrecioUnitario == 10 && d.Subtotal == 30 && p!.Total == 30, "Se cambió el precio histórico.");
});
await Check("Cambiar producto toma el precio del nuevo producto", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db; await AddDetail(db);
    var id = await db.DetallesPedido.Select(d => d.Id).SingleAsync();
    Assert(await new DetallePedidosController(db).Edit(id,
        new DetallePedido { Id = id, PedidoId = 1, ProductoId = 2, Cantidad = 4 }) is RedirectToActionResult, "Falló el cambio.");
    db.ChangeTracker.Clear();
    Assert((await db.Pedidos.FindAsync(1L))!.Total == 80, "Precio del producto nuevo incorrecto.");
});
await Check("Mover un detalle recalcula los dos pedidos", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db; await AddDetail(db); await AddDetail(db, producto: 2, cantidad: 1);
    var id = await db.DetallesPedido.Where(d => d.ProductoId == 1).Select(d => d.Id).SingleAsync();
    Assert(await new DetallePedidosController(db).Edit(id,
        new DetallePedido { Id = id, PedidoId = 2, ProductoId = 1, Cantidad = 2 }) is RedirectToActionResult, "Falló el movimiento.");
    db.ChangeTracker.Clear();
    Assert((await db.Pedidos.FindAsync(1L))!.Total == 20 && (await db.Pedidos.FindAsync(2L))!.Total == 20,
        "No se actualizaron ambos pedidos.");
});
await Check("No permite eliminar el último detalle del pedido", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db; await AddDetail(db);
    var id = await db.DetallesPedido.Select(d => d.Id).SingleAsync();
    var c = new DetallePedidosController(db);
    Assert(await c.DeleteConfirmed(id) is ViewResult && !c.ModelState.IsValid, "Se permitió vaciar el pedido.");
    db.ChangeTracker.Clear();
    Assert((await db.Pedidos.FindAsync(1L))!.Total == 20 && await db.DetallesPedido.CountAsync() == 1, "Se alteró el pedido.");
});
await Check("No permite trasladar el último detalle a otro pedido", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db; await AddDetail(db);
    var id = await db.DetallesPedido.Select(d => d.Id).SingleAsync();
    var c = new DetallePedidosController(db);
    Assert(await c.Edit(id, new DetallePedido { Id = id, PedidoId = 2, ProductoId = 1, Cantidad = 2 })
        is ViewResult && !c.ModelState.IsValid, "Se permitió vaciar el pedido de origen.");
    db.ChangeTracker.Clear();
    Assert((await db.DetallesPedido.SingleAsync()).PedidoId == 1 && (await db.Pedidos.FindAsync(1L))!.Total == 20,
        "Hubo persistencia parcial al trasladar.");
});
await Check("Eliminar un detalle conserva los demás y recalcula", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db; await AddDetail(db); await AddDetail(db, producto: 2, cantidad: 1);
    var id = await db.DetallesPedido.Where(d => d.ProductoId == 1).Select(d => d.Id).SingleAsync();
    Assert(await new DetallePedidosController(db).DeleteConfirmed(id) is RedirectToActionResult, "Se bloqueó una eliminación válida.");
    db.ChangeTracker.Clear();
    Assert((await db.Pedidos.FindAsync(1L))!.Total == 20 && await db.DetallesPedido.CountAsync() == 1, "Totales incorrectos.");
});
await Check("Rechaza total que excede decimal(10,2) sin guardar el nuevo detalle", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    db.DetallesPedido.Add(new DetallePedido { PedidoId = 1, ProductoId = 1, NombreProducto = "A",
        PrecioUnitario = TotalesPedido.MaximoImporte, Cantidad = 1, Subtotal = TotalesPedido.MaximoImporte });
    var p = await db.Pedidos.FindAsync(1L); p!.Total = p.Subtotal = TotalesPedido.MaximoImporte;
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var c = new DetallePedidosController(db);
    Assert(await c.Create(new DetallePedido { PedidoId = 1, ProductoId = 2, Cantidad = 1 }) is ViewResult && !c.ModelState.IsValid,
        "No se rechazó el total excesivo.");
    db.ChangeTracker.Clear();
    Assert(await db.DetallesPedido.CountAsync() == 1 && (await db.Pedidos.FindAsync(1L))!.Total == TotalesPedido.MaximoImporte,
        "Hubo persistencia parcial.");
});
await Check("La transacción revierte si falla la FK del detalle", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db; await AddDetail(db);
    db.DetallesPedido.Add(new DetallePedido { PedidoId = 1, ProductoId = 999, NombreProducto = "No existe",
        PrecioUnitario = 15, Cantidad = 1, Subtotal = 15 });
    try { await TotalesPedido.GuardarAsync(db, 1); throw new Exception("Se esperaba error de FK."); }
    catch (DbUpdateException) { }
    db.ChangeTracker.Clear();
    Assert((await db.Pedidos.FindAsync(1L))!.Total == 20 && await db.DetallesPedido.CountAsync() == 1,
        "La transacción dejó cambios parciales.");
});
await Check("Crear pedido guarda el primer detalle y calcula sus importes", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    var c = new PedidosController(db);
    var m = new NuevoPedidoViewModel { NumeroPedido = "NUEVO", UsuarioId = 1, ProductoId = 1, Cantidad = 3 };
    Validate(c, m);
    Assert(await c.Create(m) is RedirectToActionResult, "No se creó el pedido con su detalle.");
    db.ChangeTracker.Clear(); var p = await db.Pedidos.Include(x => x.Detalles).SingleAsync(x => x.NumeroPedido == "NUEVO");
    Assert(p.Total == 30 && p.Subtotal == 30 && p.Detalles.Count == 1, "Pedido o detalle incompletos.");
});
await Check("No crea pedidos sin producto ni con cantidad cero", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    foreach (var m in new[] {
        new NuevoPedidoViewModel { NumeroPedido = "SIN-PRODUCTO", UsuarioId = 1, ProductoId = 0 },
        new NuevoPedidoViewModel { NumeroPedido = "SIN-CANTIDAD", UsuarioId = 1, ProductoId = 1, Cantidad = 0 } })
    {
        var c = new PedidosController(db); Validate(c, m);
        Assert(await c.Create(m) is ViewResult && !c.ModelState.IsValid, "Se aceptó un pedido vacío.");
    }
    Assert(await db.Pedidos.CountAsync() == 2, "Se persistió un pedido incompleto.");
});
await Check("Un pedido vacío anterior no puede marcarse completado", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    var c = new PedidosController(db);
    Assert(await c.Edit(1, new Pedido { Id = 1, NumeroPedido = "PED-1", UsuarioId = 1, Estado = "completada" })
        is ViewResult && !c.ModelState.IsValid, "Se completó un pedido vacío.");
    db.ChangeTracker.Clear(); Assert((await db.Pedidos.FindAsync(1L))!.Estado == "pendiente", "Se cambió el estado.");
});
await Check("Un pedido válido sí puede completarse", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db; await AddDetail(db);
    var c = new PedidosController(db);
    Assert(await c.Edit(1, new Pedido { Id = 1, NumeroPedido = "PED-1", UsuarioId = 1, Estado = "completada" })
        is RedirectToActionResult, "No pudo completarse el pedido válido.");
    db.ChangeTracker.Clear(); Assert((await db.Pedidos.FindAsync(1L))!.Estado == "completada", "No se guardó el estado.");
});
await Check("Editar pedido no permite alterar manualmente los importes", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db; await AddDetail(db);
    var c = new PedidosController(db);
    Assert(await c.Edit(1, new Pedido { Id = 1, NumeroPedido = "PED-1", UsuarioId = 1, Subtotal = 999, Total = 999 })
        is RedirectToActionResult, "Falló la edición del pedido.");
    db.ChangeTracker.Clear(); var p = await db.Pedidos.FindAsync(1L);
    Assert(p!.Total == 20 && p.Subtotal == 20, "Se aceptaron importes manuales.");
});
await Check("Editar carrito rechaza usuario duplicado", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    db.Carritos.AddRange(new Carrito { Id = 1, UsuarioId = 1 }, new Carrito { Id = 2, UsuarioId = 3 });
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var c = new CarritosController(db);
    Assert(await c.Edit(2, new Carrito { Id = 2, UsuarioId = 1 }) is ViewResult && !c.ModelState.IsValid, "Falta validación de unicidad.");
    db.ChangeTracker.Clear(); Assert((await db.Carritos.FindAsync(2L))!.UsuarioId == 3, "Se guardó el duplicado.");
});
await Check("Editar detalle de carrito rechaza producto duplicado", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    db.Carritos.Add(new Carrito { Id = 1, UsuarioId = 1 });
    db.DetallesCarrito.AddRange(new DetalleCarrito { Id = 1, CarritoId = 1, ProductoId = 1 },
        new DetalleCarrito { Id = 2, CarritoId = 1, ProductoId = 2 });
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var c = new DetallesCarritoController(db);
    Assert(await c.Edit(2, new DetalleCarrito { Id = 2, CarritoId = 1, ProductoId = 1 }) is ViewResult && !c.ModelState.IsValid,
        "Falta validación de producto duplicado.");
    db.ChangeTracker.Clear(); Assert((await db.DetallesCarrito.FindAsync(2L))!.ProductoId == 2, "Se guardó el duplicado.");
});
await Check("Categoría con productos muestra mensaje y no se elimina", async () =>
{
    await using var f = await Fixture.Create(); var c = new CategoriasController(f.Db);
    Assert(await c.DeleteConfirmed(1) is ViewResult && !c.ModelState.IsValid, "Falta mensaje de relación.");
    Assert(await f.Db.Categorias.AnyAsync(x => x.Id == 1), "Se eliminó la categoría.");
});
await Check("Producto usado en un pedido muestra mensaje y no se elimina", async () =>
{
    await using var f = await Fixture.Create(); await AddDetail(f.Db); var c = new ProductosController(f.Db);
    Assert(await c.DeleteConfirmed(1) is ViewResult && !c.ModelState.IsValid, "Falta mensaje de relación.");
    Assert(await f.Db.Productos.AnyAsync(x => x.Id == 1), "Se eliminó el producto.");
});
await Check("Producto usado en carrito muestra mensaje y no se elimina", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    db.Carritos.Add(new Carrito { Id = 1, UsuarioId = 1 });
    db.DetallesCarrito.Add(new DetalleCarrito { CarritoId = 1, ProductoId = 1 });
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var c = new ProductosController(db);
    Assert(await c.DeleteConfirmed(1) is ViewResult && !c.ModelState.IsValid, "Falta mensaje de relación.");
    Assert(await db.Productos.AnyAsync(x => x.Id == 1), "Se eliminó el producto.");
});
await Check("Categoría sin productos sí se puede eliminar", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    db.Categorias.Add(new Categoria { Id = 2, Nombre = "Vacía" }); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    Assert(await new CategoriasController(db).DeleteConfirmed(2) is RedirectToActionResult, "Se bloqueó una eliminación válida.");
    Assert(!await db.Categorias.AnyAsync(x => x.Id == 2), "La categoría no se eliminó.");
});
Console.WriteLine($"Resultado: {passed} correctas, {failures.Count} fallidas.");
if (failures.Count > 0) Environment.ExitCode = 1;

sealed class Fixture : IAsyncDisposable
{
    private readonly SqliteConnection connection;
    public MarketPlaceProContext Db { get; }
    private Fixture(SqliteConnection c, MarketPlaceProContext db) { connection = c; Db = db; }
    public static async Task<Fixture> Create()
    {
        var c = new SqliteConnection("Data Source=:memory:"); await c.OpenAsync();
        var db = new MarketPlaceProContext(new DbContextOptionsBuilder<MarketPlaceProContext>().UseSqlite(c).Options);
        await db.Database.EnsureCreatedAsync();
        db.Usuarios.AddRange(new Usuario { Id = 1, Nombre = "Comprador", Correo = "comprador@example.invalid", Contrasena = "dato-de-prueba" },
            new Usuario { Id = 2, Nombre = "Vendedor", Correo = "vendedor@example.invalid", Contrasena = "dato-de-prueba", Rol = "vendedor" },
            new Usuario { Id = 3, Nombre = "Otro", Correo = "otro@example.invalid", Contrasena = "dato-de-prueba" });
        db.Categorias.Add(new Categoria { Id = 1, Nombre = "Categoría" });
        db.Productos.AddRange(new Producto { Id = 1, Nombre = "Mismo nombre", CategoriaId = 1, UsuarioId = 2, Precio = 10, Stock = 5 },
            new Producto { Id = 2, Nombre = "Mismo nombre", CategoriaId = 1, UsuarioId = 2, Precio = 20, Stock = 5 });
        db.Pedidos.AddRange(new Pedido { Id = 1, NumeroPedido = "PED-1", UsuarioId = 1 },
            new Pedido { Id = 2, NumeroPedido = "PED-2", UsuarioId = 1 });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return new Fixture(c, db);
    }
    public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
}
