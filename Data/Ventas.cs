using System.ComponentModel.DataAnnotations;
using System.Data;
using MarketPlacePro.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketPlacePro.Data;

public static class Ventas
{
    public static bool EstaAbierto(Pedido pedido) => (pedido.Estado is "pendiente" or "en_proceso") &&
        pedido.Pago?.Estado is not ("completado" or "reembolsado") && pedido.Pago?.StockDescontado != true;

    public static async Task VerificarEditableAsync(MarketPlaceProContext context, long id)
    {
        var pedido = await context.Pedidos.AsNoTracking().Include(p => p.Pago).SingleOrDefaultAsync(p => p.Id == id)
            ?? throw new ValidationException("El pedido ya no existe.");
        if (!EstaAbierto(pedido))
            throw new ValidationException("El pedido está cerrado. Sus productos, importes y comprador no se pueden modificar.");
    }

    public static async Task<Pedido> ConfirmarCobroAsync(MarketPlaceProContext context, long id, string metodo, string? referencia)
    {
        if (metodo is not ("efectivo" or "transferencia" or "qr"))
            throw new ValidationException("Seleccione efectivo, transferencia o QR.");
        referencia = referencia?.Trim();
        if (referencia?.Length > 255 || (metodo != "efectivo" && string.IsNullOrWhiteSpace(referencia)))
            throw new ValidationException("Transferencia y QR requieren una referencia de hasta 255 caracteres.");
        await using var tx = context.Database.CurrentTransaction == null
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        var pedido = await context.Pedidos.Include(p => p.Detalles).Include(p => p.Pago).SingleOrDefaultAsync(p => p.Id == id)
            ?? throw new ValidationException("El pedido ya no existe.");
        if (pedido.Estado == "completada" && pedido.Pago?.Estado == "completado") return pedido;
        if (!EstaAbierto(pedido)) throw new ValidationException("El pedido no está disponible para cobrar.");
        TotalesPedido.AsignarTotales(pedido, pedido.Detalles.ToList());
        foreach (var grupo in pedido.Detalles.GroupBy(d => d.ProductoId).OrderBy(g => g.Key))
        {
            var cantidad = grupo.Sum(d => (long)d.Cantidad);
            if (cantidad > int.MaxValue) throw new ValidationException("La cantidad supera las existencias admitidas.");
            var unidades = (int)cantidad;
            var cambios = await context.Productos.Where(p => p.Id == grupo.Key && p.Estado == "activo" && p.Stock >= unidades)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - unidades)
                    .SetProperty(p => p.Estado, p => p.Stock == unidades ? "agotado" : p.Estado));
            if (cambios != 1)
                throw new ValidationException("Un producto ya no está activo o no tiene stock suficiente. No se registró el cobro; revisa el pedido.");
        }
        pedido.Pago ??= new Pago();
        pedido.Pago.Metodo = metodo;
        pedido.Pago.Monto = pedido.Total;
        pedido.Pago.Referencia = string.IsNullOrWhiteSpace(referencia) ? null : referencia;
        pedido.Pago.FechaPago = DateTime.UtcNow;
        pedido.Pago.Estado = "completado";
        pedido.Pago.StockDescontado = true;
        pedido.Estado = "completada";
        await context.SaveChangesAsync();
        if (tx != null) await tx.CommitAsync();
        return pedido;
    }

    public static async Task CancelarAsync(MarketPlaceProContext context, long id)
    {
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var pedido = await context.Pedidos.Include(p => p.Pago).SingleOrDefaultAsync(p => p.Id == id)
            ?? throw new ValidationException("El pedido ya no existe.");
        if (pedido.Estado == "cancelada") return;
        if (!EstaAbierto(pedido)) throw new ValidationException("Una venta cobrada requiere devolución completa, no cancelación.");
        pedido.Estado = "cancelada";
        if (pedido.Pago != null) pedido.Pago.Estado = "rechazado";
        await context.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public static async Task ReabrirPendienteAsync(MarketPlaceProContext context, long id)
    {
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var pedido = await context.Pedidos.Include(p => p.Pago).SingleOrDefaultAsync(p => p.Id == id)
            ?? throw new ValidationException("El pedido ya no existe.");
        if (pedido.Estado != "completada" || pedido.Pago?.Estado is "completado" or "reembolsado" || pedido.Pago?.StockDescontado == true)
            throw new ValidationException("Solo se puede revisar un pedido antiguo marcado como completado sin cobro confirmado.");
        pedido.Estado = "pendiente";
        await context.SaveChangesAsync(); await tx.CommitAsync();
    }

    public static async Task DevolverAsync(MarketPlaceProContext context, long id)
    {
        await using var tx = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var pedido = await context.Pedidos.Include(p => p.Detalles).Include(p => p.Pago).SingleOrDefaultAsync(p => p.Id == id)
            ?? throw new ValidationException("El pedido ya no existe.");
        if (pedido.Estado == "cancelada" && pedido.Pago?.Estado == "reembolsado") return;
        if (pedido.Estado != "completada" || pedido.Pago?.Estado != "completado")
            throw new ValidationException("Solo se puede devolver una venta cobrada.");
        foreach (var grupo in pedido.Detalles.Where(_ => pedido.Pago.StockDescontado).GroupBy(d => d.ProductoId).OrderBy(g => g.Key))
        {
            var cantidad = grupo.Sum(d => (long)d.Cantidad);
            if (cantidad > int.MaxValue) throw new ValidationException("La reposición supera el límite admitido.");
            var unidades = (int)cantidad;
            var cambios = await context.Productos.Where(p => p.Id == grupo.Key && p.Stock <= int.MaxValue - unidades)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + unidades)
                    .SetProperty(p => p.Estado, p => p.Estado == "agotado" ? "activo" : p.Estado));
            if (cambios != 1) throw new ValidationException("No se puede reponer el stock del producto. No se guardó la devolución.");
        }
        pedido.Estado = "cancelada";
        pedido.Pago.Estado = "reembolsado";
        pedido.Pago.StockDescontado = false;
        await context.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
