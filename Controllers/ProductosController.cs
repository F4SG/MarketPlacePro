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
    public class ProductosController : Controller
    {
        private readonly MarketPlaceProContext _context;

        public ProductosController(MarketPlaceProContext context)
        {
            _context = context;
        }

        // GET: Productos
        public async Task<IActionResult> Index()
        {
            var marketPlaceProContext = _context.Productos.Include(p => p.Categoria).Include(p => p.Vendedor);
            return View(await marketPlaceProContext.ToListAsync());
        }

        // GET: Productos/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var producto = await _context.Productos
                .Include(p => p.Categoria)
                .Include(p => p.Vendedor)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (producto == null)
            {
                return NotFound();
            }

            return View(producto);
        }

        // GET: Productos/Create
        public IActionResult Create()
        {
            CargarListas();
            return View();
        }

        // POST: Productos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Nombre,Descripcion,Precio,Stock,Imagen,Estado,FechaPublicacion,UsuarioId,CategoriaId")] Producto producto)
        {
            if (!await _context.Categorias.AnyAsync(c => c.Id == producto.CategoriaId))
            {
                ModelState.AddModelError(nameof(Producto.CategoriaId), "La categoría seleccionada no existe.");
            }
            if (!await _context.Usuarios.AnyAsync(u => u.Id == producto.UsuarioId && u.Rol == "vendedor"))
            {
                ModelState.AddModelError(nameof(Producto.UsuarioId), "El vendedor seleccionado no existe.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(producto);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            CargarListas(producto.CategoriaId, producto.UsuarioId);
            return View(producto);
        }

        // GET: Productos/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null) return NotFound();

            var producto = await _context.Productos.FindAsync(id.Value);
            if (producto == null) return NotFound();

            CargarListas(producto.CategoriaId, producto.UsuarioId);
            return View(producto);
        }

        // POST: Productos/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id,
            [Bind("Id,Nombre,Descripcion,Precio,Stock,Imagen,Estado,FechaPublicacion,UsuarioId,CategoriaId")] Producto producto)
        {
            if (id != producto.Id) return NotFound();

            if (!await _context.Categorias.AnyAsync(c => c.Id == producto.CategoriaId))
            {
                ModelState.AddModelError(nameof(Producto.CategoriaId), "La categoría seleccionada no existe.");
            }
            if (!await _context.Usuarios.AnyAsync(u => u.Id == producto.UsuarioId && u.Rol == "vendedor"))
            {
                ModelState.AddModelError(nameof(Producto.UsuarioId), "El vendedor seleccionado no existe.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(producto);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Productos.AnyAsync(p => p.Id == producto.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            CargarListas(producto.CategoriaId, producto.UsuarioId);
            return View(producto);
        }

        // GET: Productos/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var producto = await _context.Productos
                .Include(p => p.Categoria)
                .Include(p => p.Vendedor)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (producto == null)
            {
                return NotFound();
            }

            return View(producto);
        }

        // POST: Productos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto != null)
            {
                _context.Productos.Remove(producto);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private void CargarListas(long? categoriaSeleccionada = null, long? vendedorSeleccionado = null)
        {
            ViewData["CategoriaId"] = new SelectList(
                _context.Categorias.AsNoTracking().OrderBy(c => c.Nombre),
                "Id", "Nombre", categoriaSeleccionada);

            ViewData["UsuarioId"] = new SelectList(
                _context.Usuarios.AsNoTracking().Where(u => u.Rol == "vendedor").OrderBy(u => u.Nombre),
                "Id", "Nombre", vendedorSeleccionado);
        }

        private bool ProductoExists(long id)
        {
            return _context.Productos.Any(e => e.Id == id);
        }
    }
}
