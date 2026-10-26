using System.ComponentModel.DataAnnotations;
using System.Data;
using MarketPlacePro.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketPlacePro.Data;

public static class TotalesPedido
{
    public const decimal MaximoImporte = 99999999.99m;

    public static async Task GuardarAsync(MarketPlaceProContext context, params long[] pedidoIds)
    {
        // Se guardan los detalles y ambos totales en una sola transacción.
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        context.ChangeTracker.DetectChanges();
        foreach (var pedidoId in pedidoIds.Distinct().Order())
        {
            await Ventas.VerificarEditableAsync(context, pedidoId);
            await context.DetallesPedido.Where(d => d.PedidoId == pedidoId).LoadAsync();
            var pedido = await context.Pedidos.FindAsync(pedidoId)
                ?? throw new ValidationException("El pedido ya no existe. Vuelva al listado.");
            var detalles = context.ChangeTracker.Entries<DetallePedido>()
                .Where(e => e.State != EntityState.Deleted && e.Entity.PedidoId == pedidoId)
                .Select(e => e.Entity).ToList();
            AsignarTotales(pedido, detalles);
            var pago = await context.Pagos.SingleOrDefaultAsync(p => p.PedidoId == pedidoId);
            if (pago != null && pago.Estado is "pendiente" or "rechazado") pago.Monto = pedido.Total;
        }
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public static async Task CrearAsync(MarketPlaceProContext context, Pedido pedido)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (pedido.Estado != "pendiente")
            throw new ValidationException("Los pedidos nuevos se crean pendientes; se completan al confirmar el cobro.");
        AsignarTotales(pedido, pedido.Detalles.ToList());
        context.Pedidos.Add(pedido);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    internal static void AsignarTotales(Pedido pedido, List<DetallePedido> detalles)
    {
        if (detalles.Count == 0)
            throw new ValidationException("Todo pedido debe conservar al menos un detalle. No puede eliminar ni trasladar el último producto; puede eliminar el pedido completo.");
        if (detalles.Any(d => d.Cantidad <= 0 || d.PrecioUnitario <= 0 || d.Subtotal < 0))
            throw new ValidationException("Revise las cantidades y los precios de los detalles del pedido.");
        foreach (var detalle in detalles)
        {
            if (decimal.Round(detalle.PrecioUnitario, 2) != detalle.PrecioUnitario)
                throw new ValidationException("Los precios deben tener como máximo dos decimales.");
            detalle.Subtotal = detalle.PrecioUnitario * detalle.Cantidad;
        }
        var subtotal = detalles.Sum(d => d.Subtotal);
        if (subtotal > MaximoImporte)
            throw new ValidationException("El importe del pedido supera el máximo de 99999999.99.");
        pedido.Subtotal = subtotal;
        // No hay impuestos, envío ni descuentos definidos en este modelo.
        pedido.Total = subtotal;
    }

}
