using System.Data;
using MarketPlacePro.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MarketPlacePro.Data;

public static class MarketPlaceSeeder
{
    public static async Task InicializarAsync(MarketPlaceProContext context)
    {
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (await context.Usuarios.AnyAsync(u => u.Correo.EndsWith(".demo@marketplace.example.invalid"))) return;
        var ahora = DateTime.UtcNow;
        var nombres = new[] { "Andrea Vargas", "Luis Flores", "Carla Rojas", "María Pérez", "José Salas", "Ana Gutiérrez" };
        var ciudades = new[] { "La Paz", "Cochabamba", "Santa Cruz de la Sierra", "El Alto", "Sucre", "Tarija" };
        var usuarios = nombres.Select((nombre, i) => new Usuario
        {
            Nombre = nombre, Correo = (i == 0 ? "admin" : "persona" + i) + ".demo@marketplace.example.invalid",
            Rol = i == 0 ? "admin" : i <= 2 ? "vendedor" : "comprador", Ciudad = ciudades[i],
            Telefono = "7123456" + i, FechaRegistro = ahora.AddDays(-20 + i),
            NombreTienda = i == 1 ? "Casa Digital" : i == 2 ? "Todo Hogar" : null,
            DescripcionTienda = i is 1 or 2 ? "Tienda de demostración con catálogo y precios en bolivianos." : null
        }).ToArray();
        foreach (var usuario in usuarios)
            usuario.Contrasena = new PasswordHasher<Usuario>().HashPassword(usuario, "DemoVentas2026!");
        context.Usuarios.AddRange(usuarios);
        var categorias = new[] { "Tecnología", "Hogar", "Libros", "Deportes" }.Select((nombre, i) => new Categoria
        {
            Nombre = nombre, Descripcion = "Productos de " + nombre.ToLowerInvariant() + " para la demostración del marketplace.",
            Imagen = "/images/demo/categoria-" + i + ".svg"
        }).ToArray();
        context.Categorias.AddRange(categorias);
        await context.SaveChangesAsync();
        var productosNombres = new[] { "Mouse inalámbrico", "Audífonos", "Teclado compacto", "Lámpara de escritorio",
            "Taza de cerámica", "Organizador", "Novela ilustrada", "Cuaderno de notas", "Guía de cocina",
            "Balón de fútbol", "Botella deportiva", "Cuerda para saltar" };
        var precios = new decimal[] { 85, 150, 220, 120, 35, 65, 75, 25, 90, 110, 45, 30 };
        var existencias = new[] { 12, 8, 7, 15, 10, 6, 20, 4, 2, 1, 1, 5 };
        var productos = productosNombres.Select((nombre, i) => new Producto
        {
            Nombre = nombre, Descripcion = nombre + ". Producto de demostración con imagen y precio en bolivianos.",
            Precio = precios[i], Stock = existencias[i], Estado = i == 8 ? "pausado" : "activo",
            FechaPublicacion = ahora.AddDays(-15), CategoriaId = categorias[i / 3].Id,
            UsuarioId = usuarios[i < 3 || i >= 9 ? 1 : 2].Id, Imagen = "/images/demo/producto-" + i + ".svg"
        }).ToArray();
        context.Productos.AddRange(productos);
        await context.SaveChangesAsync();
        Pedido PedidoDemo(string numero, int comprador, params (int Producto, int Cantidad)[] lineas)
        {
            var pedido = new Pedido { NumeroPedido = numero, UsuarioId = usuarios[comprador].Id, Estado = "pendiente",
                FechaPedido = ahora.AddDays(-3), Detalles = lineas.Select(l => new DetallePedido
                {
                    ProductoId = productos[l.Producto].Id, NombreProducto = productos[l.Producto].Nombre,
                    PrecioUnitario = productos[l.Producto].Precio, Cantidad = l.Cantidad,
                    Subtotal = productos[l.Producto].Precio * l.Cantidad
                }).ToList() };
            TotalesPedido.AsignarTotales(pedido, pedido.Detalles.ToList());
            context.Pedidos.Add(pedido); return pedido;
        }
        var venta1 = PedidoDemo("DEMO-VENTA-001", 3, (0, 1), (3, 2));
        var venta2 = PedidoDemo("DEMO-VENTA-002", 4, (10, 1), (6, 1));
        PedidoDemo("DEMO-PENDIENTE-001", 3, (1, 1), (7, 2));
        var pendiente = PedidoDemo("DEMO-PENDIENTE-002", 5, (4, 2));
        var cancelado = PedidoDemo("DEMO-CANCELADO-001", 4, (11, 1));
        cancelado.Estado = "cancelada";
        await context.SaveChangesAsync();
        await Ventas.ConfirmarCobroAsync(context, venta1.Id, "efectivo", "DEMO-EFECTIVO-001");
        await Ventas.ConfirmarCobroAsync(context, venta2.Id, "transferencia", "DEMO-TRANSFERENCIA-002");
        venta1.Pago!.FechaPago = ahora.AddDays(-2); venta2.Pago!.FechaPago = ahora.AddDays(-1);
        context.Pagos.Add(new Pago { PedidoId = pendiente.Id, Metodo = "qr", Monto = pendiente.Total,
            Estado = "pendiente", Referencia = "DEMO-POR-VERIFICAR", FechaPago = ahora });
        context.Carritos.AddRange(
            new Carrito { UsuarioId = usuarios[3].Id, Estado = "activo", FechaCreacion = ahora.AddDays(-1), FechaActualizacion = ahora,
                Detalles = new List<DetalleCarrito> { new() { ProductoId = productos[2].Id, Cantidad = 1, FechaAgregado = ahora },
                    new() { ProductoId = productos[4].Id, Cantidad = 2, FechaAgregado = ahora } } },
            new Carrito { UsuarioId = usuarios[4].Id, Estado = "activo", FechaCreacion = ahora, FechaActualizacion = ahora },
            new Carrito { UsuarioId = usuarios[5].Id, Estado = "activo", FechaCreacion = ahora, FechaActualizacion = ahora,
                Detalles = new List<DetalleCarrito> { new() { ProductoId = productos[9].Id, Cantidad = 1, FechaAgregado = ahora } } });
        await context.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
