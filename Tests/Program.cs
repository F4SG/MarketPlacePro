using System.ComponentModel.DataAnnotations;
using System.Globalization;
using MarketPlacePro.Controllers;
using MarketPlacePro.Data;
using MarketPlacePro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

if (args.Contains("--web")) { await WebSmoke.RunAsync(); return; }

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
        db.Pagos.Add(new Pago { PedidoId = p.Id, Metodo = "efectivo", Estado = "completado", Monto = i == 2 ? 40 : 10, FechaPago = fechas[i] });
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
await Check("Un pedido solo se completa al confirmar el cobro", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db; await AddDetail(db);
    var c = new PedidosController(db);
    Assert(await c.Edit(1, new Pedido { Id = 1, NumeroPedido = "PED-1", UsuarioId = 1, Estado = "completada" })
        is ViewResult && !c.ModelState.IsValid, "Permitió completar manualmente sin cobrar.");
    db.ChangeTracker.Clear();
    await Ventas.ConfirmarCobroAsync(db, 1, "efectivo", null); db.ChangeTracker.Clear();
    Assert((await db.Pedidos.FindAsync(1L))!.Estado == "completada", "No se completó al cobrar.");
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
await Check("Confirmar carrito crea pedido completo y permite reutilizar el carrito", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    db.Carritos.Add(new Carrito { Id = 1, UsuarioId = 1, Estado = "activo" });
    db.DetallesCarrito.AddRange(new DetalleCarrito { CarritoId = 1, ProductoId = 1, Cantidad = 2 },
        new DetalleCarrito { CarritoId = 1, ProductoId = 2, Cantidad = 3 });
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var pedido = await CompraCarrito.CrearPedidoAsync(db, 1);
    db.ChangeTracker.Clear();
    var saved = await db.Pedidos.Include(p => p.Detalles).SingleAsync(p => p.Id == pedido.Id);
    Assert(saved.UsuarioId == 1 && saved.Estado == "pendiente" && saved.Total == 80 && saved.Subtotal == 80
        && saved.Detalles.Count == 2, "El pedido no corresponde al carrito.");
    Assert(!await db.DetallesCarrito.AnyAsync() && (await db.Carritos.FindAsync(1L))!.Estado == "activo",
        "El carrito no quedó disponible.");
    Assert((await db.Productos.FindAsync(1L))!.Stock == 5, "Se alteró el stock manual.");
    try { await CompraCarrito.CrearPedidoAsync(db, 1); throw new Exception("Se duplicó el pedido."); }
    catch (ValidationException) { }
    Assert(await db.Pedidos.CountAsync() == 3, "Se creó un segundo pedido vacío.");
    db.ChangeTracker.Clear();
    (await db.Productos.FindAsync(1L))!.Precio = 99;
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    Assert((await db.DetallesPedido.SingleAsync(d => d.PedidoId == pedido.Id && d.ProductoId == 1)).PrecioUnitario == 10,
        "No se conservó el precio de compra.");
});
await Check("Confirmación rechaza carrito inactivo o producto inactivo sin vaciarlo", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    db.Carritos.Add(new Carrito { Id = 1, UsuarioId = 1, Estado = "abandonado" });
    db.DetallesCarrito.Add(new DetalleCarrito { CarritoId = 1, ProductoId = 1, Cantidad = 1 });
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    for (int i = 0; i < 2; i++)
    {
        try { await CompraCarrito.CrearPedidoAsync(db, 1); throw new Exception("Se aceptó el carrito inválido."); }
        catch (ValidationException) { }
        db.ChangeTracker.Clear();
        Assert(await db.Pedidos.CountAsync() == 2 && await db.DetallesCarrito.CountAsync() == 1,
            "La confirmación dejó cambios parciales.");
        (await db.Carritos.FindAsync(1L))!.Estado = "activo";
        (await db.Productos.FindAsync(1L))!.Estado = "inactivo";
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    }
});
await Check("Importe excesivo no crea pedido ni vacía el carrito", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    db.Carritos.Add(new Carrito { Id = 1, UsuarioId = 1 });
    db.DetallesCarrito.Add(new DetalleCarrito { CarritoId = 1, ProductoId = 1, Cantidad = int.MaxValue });
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    try { await CompraCarrito.CrearPedidoAsync(db, 1); throw new Exception("Se aceptó el importe excesivo."); }
    catch (ValidationException) { }
    db.ChangeTracker.Clear();
    Assert(await db.Pedidos.CountAsync() == 2 && await db.DetallesCarrito.CountAsync() == 1, "Se perdieron productos.");
});
await Check("Cobro desde pedido precarga total y no ofrece editar una venta cerrada", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db; await AddDetail(db);
    var c = new PagosController(db);
    var result = (ViewResult)await c.Create(pedidoId: 1);
    var pago = (CobroViewModel)result.Model!;
    Assert(pago.PedidoId == 1 && pago.Total == 20, "Cobro sin importe calculado.");
    await Ventas.ConfirmarCobroAsync(db, 1, "efectivo", null); db.ChangeTracker.Clear();
    Assert(await c.Create(pedidoId: 1) is RedirectToActionResult { ActionName: "Details" }, "Permitió volver a cobrar desde el formulario.");
});
await Check("Error al guardar la compra revierte el vaciado del carrito", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    db.Carritos.Add(new Carrito { Id = 1, UsuarioId = 1 });
    db.DetallesCarrito.Add(new DetalleCarrito { CarritoId = 1, ProductoId = 1, Cantidad = 2 });
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FallarPedido BEFORE INSERT ON Pedidos BEGIN SELECT RAISE(ABORT, 'Fallo de prueba'); END;");
    try { await CompraCarrito.CrearPedidoAsync(db, 1); throw new Exception("Faltó el fallo de persistencia."); }
    catch (DbUpdateException) { }
    db.ChangeTracker.Clear();
    Assert(await db.Pedidos.CountAsync() == 2 && await db.DetallesPedido.CountAsync() == 0
        && await db.DetallesCarrito.CountAsync() == 1, "La compra dejó cambios parciales.");
});

await Check("Nombres personales aceptan tildes y compuestos y rechazan números", () =>
{
    foreach (var nombre in new[] { "María José", "Óscar Núñez", "Ana-María Pérez", "O'Connor", "D’Angelo" })
    {
        var u = new Usuario { Nombre = nombre, Correo = "prueba@example.invalid" };
        Assert(Validator.TryValidateObject(u, new ValidationContext(u), [], true), "Nombre válido rechazado: " + nombre);
    }
    foreach (var nombre in new[] { "Ana123", "123", " ", "A", "Ana@Pérez" })
    {
        var u = new Usuario { Nombre = nombre, Correo = "prueba@example.invalid" };
        Assert(!Validator.TryValidateObject(u, new ValidationContext(u), [], true), "Nombre inválido aceptado: " + nombre);
    }
    return Task.CompletedTask;
});
await Check("Ciudades y teléfonos se validan también en el servidor", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    foreach (var campo in new[] { "Ciudad", "Telefono", "Nombre", "Contrasena", "NombreTienda" })
    {
        var u = new Usuario { Nombre = "María Pérez", Correo = campo + "@example.invalid", Contrasena = "ClaveDePrueba" };
        if (campo == "Ciudad") u.Ciudad = "Madrid";
        if (campo == "Telefono") u.Telefono = "hola123";
        if (campo == "Nombre") u.Nombre = "Ana123";
        if (campo == "Contrasena") u.Contrasena = "123";
        if (campo == "NombreTienda") u.Rol = "vendedor";
        var c = new UsuariosController(db);
        Assert(await c.Create(u) is ViewResult && c.ModelState.ContainsKey(campo), "Se aceptó " + campo + " inválido.");
    }
    Assert(await db.Usuarios.CountAsync() == 3, "Se guardó un usuario inválido.");
    foreach (var telefono in new[] { "71234567", "+59171234567", "+591 71234567" })
    {
        var u = new Usuario { Nombre = "María Pérez", Correo = "tel@example.invalid", Ciudad = "La Paz", Telefono = telefono };
        Assert(Validator.TryValidateObject(u, new ValidationContext(u), [], true), "Teléfono válido rechazado.");
    }
});
await Check("Usuario normaliza datos, protege la contraseña y conserva fecha al editar", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    var u = new Usuario { Nombre = " María Pérez ", Correo = " NUEVA@EXAMPLE.INVALID ", Contrasena = "ClaveDePrueba",
        Ciudad = " la paz ", Telefono = "+591 71234567", FechaRegistro = new DateTime(2000, 1, 1) };
    Assert(await new UsuariosController(db).Create(u) is RedirectToActionResult, "No guardó el usuario válido.");
    db.ChangeTracker.Clear();
    var saved = await db.Usuarios.SingleAsync(x => x.Correo == "nueva@example.invalid");
    Assert(saved.Nombre == "María Pérez" && saved.Ciudad == "La Paz" && saved.FechaRegistro!.Value.Year > 2000,
        "Datos sin normalizar o fecha manipulada.");
    var hash = saved.Contrasena; var fecha = saved.FechaRegistro;
    Assert(new Microsoft.AspNetCore.Identity.PasswordHasher<Usuario>().VerifyHashedPassword(saved, hash, "ClaveDePrueba")
        != Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed, "Contraseña sin hash válido.");
    var c = new UsuariosController(db);
    Assert(await c.Edit(saved.Id, new Usuario { Id = saved.Id, Nombre = "María Pérez", Correo = saved.Correo,
        Contrasena = "", Ciudad = "El Alto", FechaRegistro = new DateTime(1900, 1, 1) }) is RedirectToActionResult,
        "No pudo editar sin cambiar contraseña.");
    db.ChangeTracker.Clear(); saved = await db.Usuarios.FindAsync(saved.Id);
    Assert(saved!.Contrasena == hash && saved.FechaRegistro == fecha && saved.Ciudad == "El Alto", "Se alteró la fecha o el hash.");
});
await Check("Correo duplicado se rechaza aunque cambien las mayúsculas", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    var c = new UsuariosController(db);
    Assert(await c.Create(new Usuario { Nombre = "Otro Comprador", Correo = "COMPRADOR@EXAMPLE.INVALID", Contrasena = "ClaveDePrueba" })
        is ViewResult && !c.ModelState.IsValid, "Se aceptó el correo duplicado.");
    Assert(await db.Usuarios.CountAsync() == 3, "Se persistió el duplicado.");
});
await Check("No agrega productos inactivos a carrito ni a pedido", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    db.Carritos.Add(new Carrito { Id = 1, UsuarioId = 1 });
    (await db.Productos.FindAsync(1L))!.Estado = "inactivo";
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var carrito = new DetallesCarritoController(db);
    Assert(await carrito.Create(new DetalleCarrito { CarritoId = 1, ProductoId = 1, Cantidad = 1 })
        is ViewResult && !carrito.ModelState.IsValid, "Agregó un producto inactivo al carrito.");
    var pedido = new DetallePedidosController(db);
    Assert(await pedido.Create(new DetallePedido { PedidoId = 1, ProductoId = 1, Cantidad = 1 })
        is ViewResult && !pedido.ModelState.IsValid, "Agregó un producto inactivo al pedido.");
    Assert(!await db.DetallesCarrito.AnyAsync() && !await db.DetallesPedido.AnyAsync(), "Persistió el producto inactivo.");
});
await Check("No permite agregar productos a carritos abandonados", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    db.Carritos.Add(new Carrito { Id = 1, UsuarioId = 1, Estado = "abandonado" });
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var c = new DetallesCarritoController(db);
    Assert(await c.Create(new DetalleCarrito { CarritoId = 1, ProductoId = 1, Cantidad = 1 })
        is ViewResult && !c.ModelState.IsValid, "Aceptó el carrito abandonado.");
    Assert(!await db.DetallesCarrito.AnyAsync(), "Guardó el detalle inválido.");
});
await Check("Cobro descuenta stock una vez y devuelve existencias una sola vez", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db; await AddDetail(db, cantidad: 5);
    await Ventas.ConfirmarCobroAsync(db, 1, "qr", "REF-001"); db.ChangeTracker.Clear();
    Assert((await db.Productos.FindAsync(1L))!.Stock == 0 && (await db.Productos.FindAsync(1L))!.Estado == "agotado", "Stock o estado incorrectos.");
    var pago = await db.Pagos.SingleAsync();
    Assert(pago.Monto == 50 && pago.Estado == "completado" && (await db.Pedidos.FindAsync(1L))!.Estado == "completada", "Venta incoherente.");
    db.ChangeTracker.Clear(); await Ventas.ConfirmarCobroAsync(db, 1, "qr", "REF-001"); db.ChangeTracker.Clear();
    Assert((await db.Productos.FindAsync(1L))!.Stock == 0 && await db.Pagos.CountAsync() == 1, "Descuento o pago duplicado.");
    await Ventas.DevolverAsync(db, 1); db.ChangeTracker.Clear();
    await Ventas.DevolverAsync(db, 1); db.ChangeTracker.Clear();
    Assert((await db.Productos.FindAsync(1L))!.Stock == 5 && (await db.Productos.FindAsync(1L))!.Estado == "activo"
        && (await db.Pagos.SingleAsync()).Estado == "reembolsado" && (await db.Pedidos.FindAsync(1L))!.Estado == "cancelada", "Devolución duplicada o incompleta.");
});
await Check("Stock insuficiente revierte todos los productos y no cobra", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    await AddDetail(db, producto: 1, cantidad: 2); await AddDetail(db, producto: 2, cantidad: 6);
    try { await Ventas.ConfirmarCobroAsync(db, 1, "efectivo", null); throw new Exception("Cobró sin existencias."); }
    catch (ValidationException) { }
    db.ChangeTracker.Clear();
    Assert((await db.Productos.FindAsync(1L))!.Stock == 5 && (await db.Productos.FindAsync(2L))!.Stock == 5
        && !await db.Pagos.AnyAsync() && (await db.Pedidos.FindAsync(1L))!.Estado == "pendiente", "Cambios parciales en el cobro.");
});
await Check("Error de persistencia al cobrar revierte el descuento de stock", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db; await AddDetail(db);
    await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FallarPago BEFORE INSERT ON Pagos BEGIN SELECT RAISE(ABORT, 'Fallo de prueba'); END;");
    try { await Ventas.ConfirmarCobroAsync(db, 1, "efectivo", null); throw new Exception("Faltó el fallo."); }
    catch (DbUpdateException) { }
    db.ChangeTracker.Clear();
    Assert((await db.Productos.FindAsync(1L))!.Stock == 5 && !await db.Pagos.AnyAsync()
        && (await db.Pedidos.FindAsync(1L))!.Estado == "pendiente", "Se descontó sin cobrar.");
});
await Check("Los cobros requieren referencia y no permiten métodos arbitrarios", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db; await AddDetail(db);
    foreach (var metodo in new[] { "qr", "transferencia", "inventado" })
    {
        try { await Ventas.ConfirmarCobroAsync(db, 1, metodo, null); throw new Exception("Aceptó el método sin validar."); }
        catch (ValidationException) { }
    }
    Assert(!await db.Pagos.AnyAsync(), "Guardó un cobro inválido.");
    var c = new PagosController(db);
    Assert(await c.Create(new CobroViewModel { PedidoId = 1, Metodo = "efectivo", ConfirmarRecepcion = false }) is ViewResult
        && !c.ModelState.IsValid, "No exigió confirmar la recepción del dinero.");
});
await Check("Cancelación sin cobro no toca stock y bloquea cobros posteriores", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db; await AddDetail(db);
    await Ventas.CancelarAsync(db, 1); db.ChangeTracker.Clear();
    try { await Ventas.ConfirmarCobroAsync(db, 1, "efectivo", null); throw new Exception("Cobró pedido cancelado."); }
    catch (ValidationException) { }
    db.ChangeTracker.Clear(); Assert((await db.Productos.FindAsync(1L))!.Stock == 5 && !await db.Pagos.AnyAsync(), "Stock alterado al cancelar.");
});
await Check("Pedido vacío no puede cobrarse", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    try { await Ventas.ConfirmarCobroAsync(db, 1, "efectivo", null); throw new Exception("Cobró pedido vacío."); }
    catch (ValidationException) { }
});
await Check("Ventas cerradas no permiten editar, agregar, mover ni borrar detalles o pedido", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db; await AddDetail(db);
    await Ventas.ConfirmarCobroAsync(db, 1, "efectivo", null); db.ChangeTracker.Clear();
    var id = await db.DetallesPedido.Select(d => d.Id).SingleAsync();
    var detalle = new DetallePedidosController(db);
    Assert(await detalle.Create(new DetallePedido { PedidoId = 1, ProductoId = 2, Cantidad = 1 }) is ViewResult && !detalle.ModelState.IsValid, "Agregó a venta cerrada.");
    db.ChangeTracker.Clear(); detalle = new DetallePedidosController(db);
    Assert(await detalle.Edit(id, new DetallePedido { Id = id, PedidoId = 2, ProductoId = 1, Cantidad = 1 }) is ViewResult && !detalle.ModelState.IsValid, "Trasladó un detalle cobrado.");
    db.ChangeTracker.Clear(); detalle = new DetallePedidosController(db);
    Assert(await detalle.DeleteConfirmed(id) is ViewResult && !detalle.ModelState.IsValid, "Eliminó detalle cobrado.");
    db.ChangeTracker.Clear(); var pedido = new PedidosController(db);
    Assert(await pedido.Edit(1, new Pedido { Id = 1, NumeroPedido = "OTRO", UsuarioId = 3 }) is ViewResult && !pedido.ModelState.IsValid, "Editó venta cerrada.");
    db.ChangeTracker.Clear(); pedido = new PedidosController(db);
    Assert(await pedido.DeleteConfirmed(1) is ViewResult && !pedido.ModelState.IsValid, "Eliminó venta cerrada.");
    db.ChangeTracker.Clear(); Assert((await db.Pedidos.FindAsync(1L))!.NumeroPedido == "PED-1" && (await db.Productos.FindAsync(1L))!.Stock == 3, "Se alteró el historial o las existencias.");
});
await Check("Un pago pendiente se confirma actualizando el mismo registro", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db; await AddDetail(db);
    var pago = new Pago { PedidoId = 1, Metodo = "qr", Monto = 20, Estado = "pendiente", Referencia = "ANTERIOR" };
    db.Pagos.Add(pago); await db.SaveChangesAsync(); var id = pago.Id; db.ChangeTracker.Clear();
    await Ventas.ConfirmarCobroAsync(db, 1, "transferencia", "REF-NUEVA"); db.ChangeTracker.Clear();
    pago = await db.Pagos.SingleAsync();
    Assert(pago.Id == id && pago.Monto == 20 && pago.Estado == "completado" && pago.Referencia == "REF-NUEVA", "Duplicó o no confirmó el pago.");
});
await Check("Umbral de stock incluye agotados y excluye pausados", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    (await db.Productos.FindAsync(1L))!.Stock = 0; (await db.Productos.FindAsync(1L))!.Estado = "agotado";
    (await db.Productos.FindAsync(2L))!.Estado = "pausado";
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    var m = (ReporteStockViewModel)((ViewResult)await new ReportesController(db).ProductosBajoStock()).Model!;
    Assert(m.Filas.Count == 1 && m.Filas[0].Stock == 0 && m.Umbral == 5, "Agotados o umbral incorrectos.");
});
await Check("Población de demostración es completa, coherente e idempotente", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db;
    await MarketPlaceSeeder.InicializarAsync(db); db.ChangeTracker.Clear();
    var counts = new[] { await db.Usuarios.CountAsync(), await db.Productos.CountAsync(), await db.Pedidos.CountAsync(), await db.Pagos.CountAsync(), await db.Carritos.CountAsync() };
    await MarketPlaceSeeder.InicializarAsync(db); db.ChangeTracker.Clear();
    var despues = new[] { await db.Usuarios.CountAsync(), await db.Productos.CountAsync(), await db.Pedidos.CountAsync(), await db.Pagos.CountAsync(), await db.Carritos.CountAsync() };
    Assert(counts.SequenceEqual(despues), "Duplicó la demostración.");
    Assert(await db.Usuarios.CountAsync(u => u.Correo.EndsWith(".demo@marketplace.example.invalid")) == 6, "Faltan usuarios de muestra.");
    var pedidos = await db.Pedidos.Include(p => p.Detalles).Include(p => p.Pago).Where(p => p.NumeroPedido.StartsWith("DEMO-")).ToListAsync();
    Assert(pedidos.Count == 5 && pedidos.All(p => p.Detalles.Count > 0 && p.Total == p.Detalles.Sum(d => d.PrecioUnitario * d.Cantidad)), "Pedidos de muestra incoherentes.");
    Assert(pedidos.Count(p => p.Estado == "completada" && p.Pago!.Estado == "completado") == 2, "Faltan ventas cobradas.");
    Assert(await db.Productos.AnyAsync(p => p.Estado == "agotado" && p.Stock == 0), "Falta producto agotado.");
    Assert(!await db.Productos.AnyAsync(p => p.Stock < 0 || p.Estado == "agotado" && p.Stock != 0), "Inventario incoherente.");
});
await Check("Fotos rechazan contenido inválido y admiten PNG con ruta segura", async () =>
{
    var carpeta = Path.Combine(Path.GetTempPath(), "marketplace-fotos-" + Guid.NewGuid().ToString("N"));
    try
    {
        var almacen = new AlmacenImagenes(new EntornoPrueba(carpeta));
        var contenido = System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>");
        var falso = new FormFile(new MemoryStream(contenido), 0, contenido.Length, "foto", "falso.png");
        try { await almacen.GuardarAsync(falso); throw new Exception("Aceptó una foto falsa."); } catch (ValidationException) { }
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j0ioAAAAASUVORK5CYII=");
        var foto = new FormFile(new MemoryStream(png), 0, png.Length, "foto", "../../foto.png");
        var ruta = await almacen.GuardarAsync(foto);
        Assert(ruta.StartsWith("/uploads/") && !ruta.Contains("..") && File.Exists(Path.Combine(carpeta, ruta.TrimStart('/'))), "Ruta de foto insegura.");
        almacen.EliminarNueva(ruta); Assert(!File.Exists(Path.Combine(carpeta, ruta.TrimStart('/'))), "No revirtió la foto nueva.");
    }
    finally { if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true); }
});

await Check("Devolución de pago antiguo no repone stock que nunca se descontó", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db; await AddDetail(db);
    (await db.Pedidos.FindAsync(1L))!.Estado = "completada";
    db.Pagos.Add(new Pago { PedidoId = 1, Estado = "completado", Metodo = "efectivo", Monto = 20, StockDescontado = false });
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    await Ventas.DevolverAsync(db, 1); db.ChangeTracker.Clear();
    Assert((await db.Productos.FindAsync(1L))!.Stock == 5 && (await db.Pagos.SingleAsync()).Estado == "reembolsado", "Infló stock de un pago antiguo.");
});
await Check("Pedido antiguo completado sin pago se reabre sin modificar stock", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db; await AddDetail(db);
    (await db.Pedidos.FindAsync(1L))!.Estado = "completada";
    await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    await Ventas.ReabrirPendienteAsync(db, 1); db.ChangeTracker.Clear();
    Assert((await db.Pedidos.FindAsync(1L))!.Estado == "pendiente" && (await db.Productos.FindAsync(1L))!.Stock == 5, "Reapertura incoherente.");
    await Ventas.ConfirmarCobroAsync(db, 1, "efectivo", null); db.ChangeTracker.Clear();
    try { await Ventas.ReabrirPendienteAsync(db, 1); throw new Exception("Reabrió una venta cobrada."); } catch (ValidationException) { }
});
await Check("Formulario de inventario desactualizado no pisa el stock de una venta", async () =>
{
    await using var f = await Fixture.Create(); var db = f.Db; await AddDetail(db);
    await Ventas.ConfirmarCobroAsync(db, 1, "efectivo", null); db.ChangeTracker.Clear();
    var c = new ProductosController(db);
    Assert(await c.Edit(1, new Producto { Id = 1, Nombre = "Mismo nombre", Precio = 10, Stock = 9, Estado = "activo", UsuarioId = 2, CategoriaId = 1 }, stockAnterior: 5)
        is ViewResult && !c.ModelState.IsValid, "Sobrescribió existencias con un formulario antiguo.");
    db.ChangeTracker.Clear(); Assert((await db.Productos.FindAsync(1L))!.Stock == 3, "Se perdieron los cambios de la venta.");
});

await Check("Dos compras compiten por la última unidad sin sobreventa", async () =>
{
    var archivo = Path.Combine(Path.GetTempPath(), "marketplace-carrera-" + Guid.NewGuid().ToString("N") + ".db");
    var options = new DbContextOptionsBuilder<MarketPlaceProContext>().UseSqlite("Data Source=" + archivo + ";Pooling=False;Default Timeout=10").Options;
    try
    {
        await using (var f = await Fixture.Create())
        await using (var destino = new SqliteConnection("Data Source=" + archivo + ";Pooling=False"))
        {
            await destino.OpenAsync(); ((SqliteConnection)f.Db.Database.GetDbConnection()).BackupDatabase(destino);
        }
        await using (var db = new MarketPlaceProContext(options))
        {
            (await db.Productos.FindAsync(1L))!.Stock = 1;
            foreach (var id in new long[] { 1, 2 })
            {
                var p = await db.Pedidos.FindAsync(id); p!.Total = p.Subtotal = 10;
                db.DetallesPedido.Add(new DetallePedido { PedidoId = id, ProductoId = 1, NombreProducto = "Última unidad", PrecioUnitario = 10, Cantidad = 1, Subtotal = 10 });
            }
            await db.SaveChangesAsync();
        }
        async Task<bool> Cobrar(long id)
        {
            await using var db = new MarketPlaceProContext(options);
            try { await Ventas.ConfirmarCobroAsync(db, id, "efectivo", null); return true; }
            catch (ValidationException) { return false; }
            catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6) { return false; }
        }
        var resultados = await Task.WhenAll(Task.Run(() => Cobrar(1)), Task.Run(() => Cobrar(2)));
        await using var verificar = new MarketPlaceProContext(options);
        Assert(resultados.Count(x => x) == 1 && (await verificar.Productos.FindAsync(1L))!.Stock == 0
            && await verificar.Pagos.CountAsync() == 1, "Las dos compras consumieron la misma unidad.");
    }
    finally { if (File.Exists(archivo)) File.Delete(archivo); }
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

sealed class EntornoPrueba : IWebHostEnvironment
{
    public EntornoPrueba(string ruta) { WebRootPath = ruta; ContentRootPath = ruta; }
    public string ApplicationName { get; set; } = "MarketPlacePro";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; }
    public string EnvironmentName { get; set; } = "Development";
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
