using MarketPlacePro.Data;
using MarketPlacePro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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

    public async Task<IActionResult> ProductosBajoStock(int umbral = 5, long? categoriaId = null)
    {
        var modelo = new ReporteStockViewModel { Umbral = umbral, CategoriaId = categoriaId };
        var categorias = await _context.Categorias.AsNoTracking().OrderBy(c => c.Nombre).ToListAsync();
        ViewData["Categorias"] = new SelectList(categorias, "Id", "Nombre", categoriaId);

        if (umbral < 0)
            ModelState.AddModelError(nameof(ReporteStockViewModel.Umbral), "El umbral debe ser cero o mayor.");
        if (categoriaId.HasValue && !categorias.Any(c => c.Id == categoriaId.Value))
            ModelState.AddModelError(nameof(ReporteStockViewModel.CategoriaId), "Seleccione una categoría existente.");
        if (!ModelState.IsValid) return View(modelo);

        var query = _context.Productos.AsNoTracking()
            .Where(p => p.Estado == "activo" && p.Stock <= umbral);
        if (categoriaId.HasValue)
            query = query.Where(p => p.CategoriaId == categoriaId.Value);

        modelo.Filas = await query.OrderBy(p => p.Stock).ThenBy(p => p.Nombre).ThenBy(p => p.Id)
            .Select(p => new ProductoBajoStockDto
            {
                Producto = p.Nombre,
                Categoria = p.Categoria.Nombre,
                Vendedor = p.Vendedor.NombreTienda == null || p.Vendedor.NombreTienda == ""
                    ? p.Vendedor.Nombre : p.Vendedor.NombreTienda,
                Stock = p.Stock,
                Precio = p.Precio
            }).ToListAsync();
        modelo.CantidadBajoUmbral = modelo.Filas.Count;
        modelo.ValorEnRiesgo = modelo.Filas.Sum(f => f.Precio * f.Stock);
        return View(modelo);
    }
}
