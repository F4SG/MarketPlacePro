using MarketPlacePro.Data;
using MarketPlacePro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace MarketPlacePro.Controllers;

public class ReportesController : Controller
{
    private readonly MarketPlaceProContext _context;
    private readonly int _umbral;

    public ReportesController(MarketPlaceProContext context, IConfiguration? configuration = null)
    {
        _context = context;
        _umbral = Math.Max(0, configuration?.GetValue<int?>("Inventario:UmbralBajoStock") ?? 5);
    }

    public async Task<IActionResult> VentasPorPeriodo(DateTime? desde, DateTime? hasta, string estado = "completada")
    {
        var hastaFecha = DateTime.SpecifyKind((hasta ?? DateTime.UtcNow.AddHours(-4)).Date, DateTimeKind.Unspecified);
        var desdeFecha = desde.HasValue ? DateTime.SpecifyKind(desde.Value.Date, DateTimeKind.Unspecified)
            : (hastaFecha >= DateTime.MinValue.AddDays(30) ? hastaFecha.AddDays(-30) : hastaFecha);
        var modelo = new ReporteVentasViewModel { Desde = desdeFecha, Hasta = hastaFecha, Estado = estado };
        if (desdeFecha > hastaFecha)
            ModelState.AddModelError(nameof(modelo.Desde), "La fecha inicial no puede ser posterior a la fecha final.");
        if (hastaFecha >= DateTime.MaxValue.Date)
            ModelState.AddModelError(nameof(modelo.Hasta), "Seleccione una fecha final anterior al 31/12/9999.");
        if (estado is not ("completada" or "pendiente" or "en_proceso" or "cancelada"))
            ModelState.AddModelError(nameof(modelo.Estado), "Seleccione un estado válido.");
        if (!ModelState.IsValid) return View(modelo);

        // Los pedidos se registran en UTC; los filtros representan días completos de Bolivia.
        var desdeUtc = new DateTimeOffset(desdeFecha, TimeSpan.FromHours(-4)).UtcDateTime;
        var hastaUtcExclusivo = new DateTimeOffset(hastaFecha.AddDays(1), TimeSpan.FromHours(-4)).UtcDateTime;
        var pedidos = _context.Pedidos.AsNoTracking().Where(p => p.Estado == estado);
        if (estado == "completada")
            pedidos = pedidos.Where(p => p.Pago != null && p.Pago.Estado == "completado" &&
                p.Pago.FechaPago >= desdeUtc && p.Pago.FechaPago < hastaUtcExclusivo);
        else
            pedidos = pedidos.Where(p => p.FechaPedido >= desdeUtc && p.FechaPedido < hastaUtcExclusivo);
        modelo.Filas = await _context.DetallesPedido.AsNoTracking()
            .Where(d => pedidos.Select(p => p.Id).Contains(d.PedidoId))
            .GroupBy(d => new { d.ProductoId, d.Producto.Nombre, d.Producto.CategoriaId,
                Categoria = d.Producto.Categoria.Nombre })
            .Select(g => new VentaPorProductoDto
            {
                ProductoId = g.Key.ProductoId,
                Producto = g.Key.Nombre,
                Categoria = g.Key.Categoria,
                CantidadVendida = g.Sum(x => (long)x.Cantidad),
                MontoTotal = g.Sum(x => x.Subtotal)
            })
            .OrderByDescending(f => f.MontoTotal).ThenBy(f => f.ProductoId).ToListAsync();
        modelo.TotalPedidos = await pedidos.CountAsync();
        modelo.MontoTotal = modelo.Filas.Sum(f => f.MontoTotal);
        modelo.ProductoMasVendido = modelo.Filas.OrderByDescending(f => f.CantidadVendida)
            .ThenBy(f => f.ProductoId).FirstOrDefault()?.Producto;
        return View(modelo);
    }

    public async Task<IActionResult> ProductosBajoStock(int? umbral = null, long? categoriaId = null)
    {
        umbral ??= _umbral;
        var modelo = new ReporteStockViewModel { Umbral = umbral.Value, CategoriaId = categoriaId };
        var categorias = await _context.Categorias.AsNoTracking().OrderBy(c => c.Nombre).ToListAsync();
        ViewData["Categorias"] = new SelectList(categorias, "Id", "Nombre", categoriaId);

        if (umbral < 0)
            ModelState.AddModelError(nameof(ReporteStockViewModel.Umbral), "El umbral debe ser cero o mayor.");
        if (categoriaId.HasValue && !categorias.Any(c => c.Id == categoriaId.Value))
            ModelState.AddModelError(nameof(ReporteStockViewModel.CategoriaId), "Seleccione una categoría existente.");
        if (!ModelState.IsValid) return View(modelo);

        var query = _context.Productos.AsNoTracking()
            .Where(p => (p.Estado == "activo" || p.Estado == "agotado") && p.Stock <= umbral);
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
