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
    public class CarritosController : Controller
    {
        private readonly MarketPlaceProContext _context;

        public CarritosController(MarketPlaceProContext context)
        {
            _context = context;
        }

        // GET: Carritos
        public async Task<IActionResult> Index()
        {
            var marketPlaceProContext = _context.Carritos.Include(c => c.Usuario);
            return View(await marketPlaceProContext.ToListAsync());
        }

        // GET: Carritos/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var carrito = await _context.Carritos
                .AsNoTracking()
                .Include(c => c.Usuario)
                .Include(c => c.Detalles).ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (carrito == null)
            {
                return NotFound();
            }

            return View(carrito);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearPedido(long id)
        {
            Pedido? pedido = null;
            if (await Persistencia.GuardarAsync(this, _context,
                async () => { pedido = await CompraCarrito.CrearPedidoAsync(_context, id); }))
            {
                TempData["Exito"] = "Pedido creado. Revisa sus productos y registra el pago cuando corresponda.";
                return RedirectToAction("Details", "Pedidos", new { id = pedido!.Id });
            }
            _context.ChangeTracker.Clear();
            var carrito = await _context.Carritos.AsNoTracking().Include(c => c.Usuario)
                .Include(c => c.Detalles).ThenInclude(d => d.Producto)
                .SingleOrDefaultAsync(c => c.Id == id);
            if (carrito == null) return NotFound();
            return View("Details", carrito);
        }

        private void CargarUsuarios(long? usuarioSeleccionado = null)
        {
            ViewData["UsuarioId"] = new SelectList(
                _context.Usuarios.AsNoTracking().OrderBy(u => u.Nombre),
                "Id", "Nombre", usuarioSeleccionado);
        }

        // GET: Carritos/Create
        public IActionResult Create()
        {
            CargarUsuarios();
            return View();
        }

        // POST: Carritos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Estado,FechaCreacion,FechaActualizacion,UsuarioId")] Carrito carrito)
        {
            if (!await _context.Usuarios.AnyAsync(u => u.Id == carrito.UsuarioId))
            {
                ModelState.AddModelError(nameof(Carrito.UsuarioId), "El usuario seleccionado no existe.");
            }
            else if (await _context.Carritos.AnyAsync(c => c.UsuarioId == carrito.UsuarioId))
            {
                ModelState.AddModelError(nameof(Carrito.UsuarioId), "Ese usuario ya tiene un carrito.");
            }

            if (ModelState.IsValid)
            {
                carrito.FechaCreacion = DateTime.UtcNow;
                _context.Add(carrito);
                if (await Persistencia.GuardarAsync(this, _context)) return RedirectToAction(nameof(Details), new { id = carrito.Id });
            }

            CargarUsuarios(carrito.UsuarioId);
            return View(carrito);
        }

        // GET: Carritos/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var carrito = await _context.Carritos.FindAsync(id);
            if (carrito == null)
            {
                return NotFound();
            }

            CargarUsuarios(carrito.UsuarioId);
            return View(carrito);
        }

        // POST: Carritos/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Id,Estado,FechaCreacion,FechaActualizacion,UsuarioId")] Carrito carrito)
        {
            if (id != carrito.Id)
            {
                return NotFound();
            }

            if (!await _context.Usuarios.AnyAsync(u => u.Id == carrito.UsuarioId))
            {
                ModelState.AddModelError(nameof(Carrito.UsuarioId), "El usuario seleccionado no existe.");
            }

            if (await _context.Carritos.AnyAsync(c => c.UsuarioId == carrito.UsuarioId && c.Id != id))
                ModelState.AddModelError(nameof(Carrito.UsuarioId), "Ese usuario ya tiene otro carrito.");
            var existente = await _context.Carritos.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (existente == null) return NotFound();
            carrito.FechaCreacion = existente.FechaCreacion;
            carrito.FechaActualizacion = DateTime.UtcNow;

            if (ModelState.IsValid)
            {
                _context.Update(carrito);
                if (await Persistencia.GuardarAsync(this, _context)) return RedirectToAction(nameof(Details), new { id = carrito.Id });
            }

            CargarUsuarios(carrito.UsuarioId);
            return View(carrito);
        }

        // GET: Carritos/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var carrito = await _context.Carritos
                .AsNoTracking()
                .Include(c => c.Usuario)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (carrito == null)
            {
                return NotFound();
            }

            return View(carrito);
        }

        // POST: Carritos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var carrito = await _context.Carritos.FindAsync(id);
            if (carrito != null)
            {
                _context.Carritos.Remove(carrito);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

    }
}
