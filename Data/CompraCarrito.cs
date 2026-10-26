using System.ComponentModel.DataAnnotations;
using System.Data;
using MarketPlacePro.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketPlacePro.Data;

public static class CompraCarrito
{
    public static async Task<Pedido> CrearPedidoAsync(MarketPlaceProContext context, long carritoId)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var carrito = await context.Carritos.Include(c => c.Detalles).ThenInclude(d => d.Producto)
            .SingleOrDefaultAsync(c => c.Id == carritoId)
            ?? throw new ValidationException("El carrito ya no existe.");
        if (carrito.Estado != "activo")
            throw new ValidationException("Solo se puede crear un pedido desde un carrito activo.");
        if (carrito.Detalles.Count == 0)
            throw new ValidationException("Agrega al menos un producto antes de crear el pedido.");
        if (carrito.Detalles.Any(d => d.Cantidad <= 0 || d.Producto.Estado != "activo" || d.Producto.Precio <= 0 || d.Cantidad > d.Producto.Stock))
            throw new ValidationException("Revisa las cantidades, las existencias y los productos que ya no están activos.");

        var pedido = new Pedido
        {
            NumeroPedido = "PED-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            UsuarioId = carrito.UsuarioId,
            FechaPedido = DateTime.UtcNow,
            Estado = "pendiente",
            Detalles = carrito.Detalles.Select(d => new DetallePedido
            {
                ProductoId = d.ProductoId,
                NombreProducto = d.Producto.Nombre,
                PrecioUnitario = d.Producto.Precio,
                Cantidad = d.Cantidad,
                Subtotal = d.Producto.Precio * d.Cantidad
            }).ToList()
        };
        TotalesPedido.AsignarTotales(pedido, pedido.Detalles.ToList());
        context.Pedidos.Add(pedido);
        context.DetallesCarrito.RemoveRange(carrito.Detalles);
        carrito.FechaActualizacion = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return pedido;
    }
}
