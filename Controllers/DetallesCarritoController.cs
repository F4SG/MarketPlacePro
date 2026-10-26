using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MarketPlacePro.Data;
using MarketPlacePro.Models;

namespace MarketPlacePro.Controllers
{
    public class DetallesCarritoController : Controller
    {
        private readonly MarketPlaceProContext _context;

        public DetallesCarritoController(MarketPlaceProContext context)
        {
            _context = context;
        }

        // GET: DetallesCarrito
        public async Task<IActionResult> Index()
        {
            var marketPlaceProContext = _context.DetallesCarrito
                .Include(d => d.Carrito).ThenInclude(c => c.Usuario)
                .Include(d => d.Producto);
            return View(await marketPlaceProContext.ToListAsync());
        }

        // GET: DetallesCarrito/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var detalleCarrito = await _context.DetallesCarrito
                .Include(d => d.Carrito).ThenInclude(c => c.Usuario)
                .Include(d => d.Producto)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (detalleCarrito == null)
            {
                return NotFound();
            }

            return View(detalleCarrito);
        }

        private async Task CargarListasAsync(long? carritoSeleccionado = null, long? productoSeleccionado = null)
        {
            var carritos = await _context.Carritos.AsNoTracking()
                .Include(c => c.Usuario)
                .Select(c => new { c.Id, Texto = $"Carrito de {c.Usuario.Nombre} (#{c.Id})" })
                .ToListAsync();

            ViewData["CarritoId"] = new SelectList(carritos, "Id", "Texto", carritoSeleccionado);

            ViewData["ProductoId"] = new SelectList(
                _context.Productos.AsNoTracking().Where(p => p.Estado == "activo").OrderBy(p => p.Nombre),
                "Id", "Nombre", productoSeleccionado);
        }

        // GET: DetallesCarrito/Create
        public async Task<IActionResult> Create(long? carritoId = null)
        {
            if (carritoId.HasValue && !await _context.Carritos.AnyAsync(c => c.Id == carritoId))
                return NotFound();
            await CargarListasAsync(carritoId);
            return View(new DetalleCarrito { CarritoId = carritoId ?? 0, Cantidad = 1 });
        }

        // POST: DetallesCarrito/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Cantidad,FechaAgregado,CarritoId,ProductoId")] DetalleCarrito detalleCarrito)
        {
            if (!await _context.Carritos.AnyAsync(c => c.Id == detalleCarrito.CarritoId))
            {
                ModelState.AddModelError(nameof(DetalleCarrito.CarritoId), "El carrito seleccionado no existe.");
            }
            if (!await _context.Productos.AnyAsync(p => p.Id == detalleCarrito.ProductoId))
            {
                ModelState.AddModelError(nameof(DetalleCarrito.ProductoId), "El producto seleccionado no existe.");
            }
            else if (await _context.DetallesCarrito.AnyAsync(d => d.CarritoId == detalleCarrito.CarritoId && d.ProductoId == detalleCarrito.ProductoId))
            {
                ModelState.AddModelError(nameof(DetalleCarrito.ProductoId), "Ese producto ya está en el carrito.");
            }

            if (ModelState.IsValid)
            {
                detalleCarrito.FechaAgregado = DateTime.UtcNow;
                _context.Add(detalleCarrito);
                if (await Persistencia.GuardarAsync(this, _context))
                    return RedirectToAction("Details", "Carritos", new { id = detalleCarrito.CarritoId });
            }

            await CargarListasAsync(detalleCarrito.CarritoId, detalleCarrito.ProductoId);
            return View(detalleCarrito);
        }

        // GET: DetallesCarrito/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var detalleCarrito = await _context.DetallesCarrito.FindAsync(id);
            if (detalleCarrito == null)
            {
                return NotFound();
            }

            await CargarListasAsync(detalleCarrito.CarritoId, detalleCarrito.ProductoId);
            return View(detalleCarrito);
        }

        // POST: DetallesCarrito/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Id,Cantidad,FechaAgregado,CarritoId,ProductoId")] DetalleCarrito detalleCarrito)
        {
            if (id != detalleCarrito.Id)
            {
                return NotFound();
            }

            if (!await _context.Carritos.AnyAsync(c => c.Id == detalleCarrito.CarritoId))
            {
                ModelState.AddModelError(nameof(DetalleCarrito.CarritoId), "El carrito seleccionado no existe.");
            }
            if (!await _context.Productos.AnyAsync(p => p.Id == detalleCarrito.ProductoId))
            {
                ModelState.AddModelError(nameof(DetalleCarrito.ProductoId), "El producto seleccionado no existe.");
            }

            if (await _context.DetallesCarrito.AnyAsync(d => d.CarritoId == detalleCarrito.CarritoId &&
                d.ProductoId == detalleCarrito.ProductoId && d.Id != id))
                ModelState.AddModelError(nameof(DetalleCarrito.ProductoId), "Ese producto ya está en el carrito.");
            var existente = await _context.DetallesCarrito.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
            if (existente == null) return NotFound();
            detalleCarrito.FechaAgregado = existente.FechaAgregado;

            if (ModelState.IsValid)
            {
                _context.Update(detalleCarrito);
                if (await Persistencia.GuardarAsync(this, _context))
                    return RedirectToAction("Details", "Carritos", new { id = detalleCarrito.CarritoId });
            }

            await CargarListasAsync(detalleCarrito.CarritoId, detalleCarrito.ProductoId);
            return View(detalleCarrito);
        }

        // GET: DetallesCarrito/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var detalleCarrito = await _context.DetallesCarrito
                .Include(d => d.Carrito).ThenInclude(c => c.Usuario)
                .Include(d => d.Producto)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (detalleCarrito == null)
            {
                return NotFound();
            }

            return View(detalleCarrito);
        }

        // POST: DetallesCarrito/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var detalleCarrito = await _context.DetallesCarrito.FindAsync(id);
            if (detalleCarrito != null)
            {
                _context.DetallesCarrito.Remove(detalleCarrito);
            }

            await _context.SaveChangesAsync();
            if (detalleCarrito != null)
                return RedirectToAction("Details", "Carritos", new { id = detalleCarrito.CarritoId });
            return RedirectToAction(nameof(Index));
        }

    }
}
