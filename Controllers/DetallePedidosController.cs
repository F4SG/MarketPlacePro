
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MarketPlacePro.Data;
using MarketPlacePro.Models;

namespace MarketPlacePro.Controllers
{
    public class DetallePedidosController : Controller
    {
        private readonly MarketPlaceProContext _context;

        public DetallePedidosController(MarketPlaceProContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var detalles = await _context.DetallesPedido
                .AsNoTracking()
                .Include(d => d.Pedido)
                .Include(d => d.Producto)
                .OrderBy(d => d.Id)
                .ToListAsync();

            return View(detalles);
        }

        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
                return NotFound();

            var detalle = await _context.DetallesPedido
                .AsNoTracking()
                .Include(d => d.Pedido)
                .Include(d => d.Producto)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (detalle == null)
                return NotFound();

            return View(detalle);
        }

        public async Task<IActionResult> Create(long? pedidoId = null)
        {
            if (pedidoId.HasValue && !await _context.Pedidos.AnyAsync(p => p.Id == pedidoId))
                return NotFound();
            await CargarListas(pedidoId);
            return View(new DetallePedido { PedidoId = pedidoId ?? 0, Cantidad = 1 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Cantidad,PedidoId,ProductoId")] DetallePedido detallePedido)
        {
            if (detallePedido.Cantidad <= 0)
                ModelState.AddModelError(nameof(detallePedido.Cantidad), "La cantidad debe ser mayor que cero.");

            var pedido = await _context.Pedidos
                .FirstOrDefaultAsync(p => p.Id == detallePedido.PedidoId);

            if (pedido == null)
                ModelState.AddModelError(nameof(detallePedido.PedidoId), "El pedido seleccionado no existe.");

            var producto = await _context.Productos
                .FirstOrDefaultAsync(p => p.Id == detallePedido.ProductoId);

            if (producto == null || producto.Estado != "activo")
                ModelState.AddModelError(nameof(detallePedido.ProductoId), "Seleccione un producto activo.");

            if (producto != null && detallePedido.Cantidad > 0)
            {
                detallePedido.NombreProducto = producto.Nombre;
                detallePedido.PrecioUnitario = producto.Precio;
                detallePedido.Subtotal = producto.Precio * detallePedido.Cantidad;
            }

            if (ModelState.IsValid)
            {
                _context.DetallesPedido.Add(detallePedido);
                if (await Persistencia.GuardarAsync(this, _context,
                    () => TotalesPedido.GuardarAsync(_context, detallePedido.PedidoId)))
                    return RedirectToAction("Details", "Pedidos", new { id = detallePedido.PedidoId });
            }

            await CargarListas(detallePedido.PedidoId, detallePedido.ProductoId);
            return View(detallePedido);
        }

        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
                return NotFound();

            var detalle = await _context.DetallesPedido.FindAsync(id);

            if (detalle == null)
                return NotFound();

            await CargarListas(detalle.PedidoId, detalle.ProductoId);
            return View(detalle);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Id,Cantidad,PedidoId,ProductoId")] DetallePedido detallePedido)
        {
            if (id != detallePedido.Id)
                return NotFound();

            var detalleExistente = await _context.DetallesPedido.FindAsync(id);

            if (detalleExistente == null)
                return NotFound();

            if (detallePedido.Cantidad <= 0)
                ModelState.AddModelError(nameof(detallePedido.Cantidad), "La cantidad debe ser mayor que cero.");

            var pedido = await _context.Pedidos
                .FirstOrDefaultAsync(p => p.Id == detallePedido.PedidoId);

            if (pedido == null)
                ModelState.AddModelError(nameof(detallePedido.PedidoId), "El pedido seleccionado no existe.");

            var producto = await _context.Productos
                .FirstOrDefaultAsync(p => p.Id == detallePedido.ProductoId);

            if (producto == null || (producto.Estado != "activo" && producto.Id != detalleExistente.ProductoId))
                ModelState.AddModelError(nameof(detallePedido.ProductoId), "Seleccione un producto activo.");

            if (ModelState.IsValid && producto != null)
            {
                var pedidoAnteriorId = detalleExistente.PedidoId;
                var cambiaProducto = detalleExistente.ProductoId != producto.Id;
                detalleExistente.PedidoId = detallePedido.PedidoId;
                detalleExistente.ProductoId = producto.Id;
                if (cambiaProducto)
                {
                    detalleExistente.NombreProducto = producto.Nombre;
                    detalleExistente.PrecioUnitario = producto.Precio;
                }
                detalleExistente.Cantidad = detallePedido.Cantidad;
                detalleExistente.Subtotal = detalleExistente.PrecioUnitario * detallePedido.Cantidad;

                if (await Persistencia.GuardarAsync(this, _context,
                    () => TotalesPedido.GuardarAsync(_context, pedidoAnteriorId, detallePedido.PedidoId)))
                    return RedirectToAction("Details", "Pedidos", new { id = detallePedido.PedidoId });
            }

            await CargarListas(detallePedido.PedidoId, detallePedido.ProductoId);
            return View(detallePedido);
        }

        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
                return NotFound();

            var detalle = await _context.DetallesPedido
                .AsNoTracking()
                .Include(d => d.Pedido)
                .Include(d => d.Producto)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (detalle == null)
                return NotFound();

            return View(detalle);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var detalle = await _context.DetallesPedido.Include(d => d.Pedido).Include(d => d.Producto)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (detalle != null)
            {
                _context.DetallesPedido.Remove(detalle);
                if (!await Persistencia.GuardarAsync(this, _context,
                    () => TotalesPedido.GuardarAsync(_context, detalle.PedidoId)))
                    return View("Delete", detalle);
            }

            if (detalle != null)
                return RedirectToAction("Details", "Pedidos", new { id = detalle.PedidoId });
            return RedirectToAction(nameof(Index));
        }

        private async Task CargarListas(long? pedidoId = null, long? productoId = null)
        {
            var pedidos = await _context.Pedidos
                .AsNoTracking()
                .OrderBy(p => p.NumeroPedido)
                .ToListAsync();

            var productos = await _context.Productos
                .AsNoTracking()
                .Where(p => p.Estado == "activo" || p.Id == productoId)
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            ViewData["PedidoId"] = new SelectList(pedidos, "Id", "NumeroPedido", pedidoId);
            ViewData["ProductoId"] = new SelectList(productos, "Id", "Nombre", productoId);
        }
    }
}
