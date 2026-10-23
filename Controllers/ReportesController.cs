using MarketPlacePro.Data;
using MarketPlacePro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MarketPlacePro.Controllers;

public class ReportesController : Controller
{
    private readonly MarketPlaceProContext _context;

    public ReportesController(MarketPlaceProContext context)
    {
        _context = context;
    }

    // Reporte 1 - Franco
    public async Task<IActionResult> VentasPorPeriodo(DateTime? desde, DateTime? hasta, string estado = "completada")
    {
        var hastaFecha = hasta ?? DateTime.Today;
        var desdeFecha = desde ?? hastaFecha.AddDays(-30);

        var query = _context.DetallesPedido.AsNoTracking()
            .Include(d => d.Producto).ThenInclude(p => p.Categoria)
            .Where(d => d.Pedido.FechaPedido >= desdeFecha
                     && d.Pedido.FechaPedido <= hastaFecha
                     && d.Pedido.Estado == estado);

        var filas = await query
            .GroupBy(d => new { d.Producto.Nombre, Categoria = d.Producto.Categoria.Nombre })
            .Select(g => new VentaPorProductoDto
            {
                Producto = g.Key.Nombre,
                Categoria = g.Key.Categoria,
                CantidadVendida = g.Sum(x => x.Cantidad),
                MontoTotal = g.Sum(x => x.Subtotal)
            })
            .OrderByDescending(f => f.MontoTotal)
            .ToListAsync();

        var modelo = new ReporteVentasViewModel
        {
            Desde = desdeFecha,
            Hasta = hastaFecha,
            Estado = estado,
            Filas = filas,
            TotalPedidos = await _context.Pedidos.CountAsync(p =>
                p.FechaPedido >= desdeFecha && p.FechaPedido <= hastaFecha && p.Estado == estado),
            MontoTotal = filas.Sum(f => f.MontoTotal),
            ProductoMasVendido = filas.OrderByDescending(f => f.CantidadVendida).FirstOrDefault()?.Producto
        };

        return View(modelo);
    }
}
