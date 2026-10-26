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
        private readonly AlmacenImagenes? _imagenes;
        private readonly MarketPlaceProContext _context;

        public ProductosController(MarketPlaceProContext context, AlmacenImagenes? imagenes = null)
        {
            _context = context;
            _imagenes = imagenes;
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
        [ValidateAntiForgeryToken, RequestSizeLimit(4 * 1024 * 1024)]
        public async Task<IActionResult> Create(
            [Bind("Nombre,Descripcion,Precio,Stock,Estado,UsuarioId,CategoriaId")] Producto producto, IFormFile? foto = null)
        {
            if (!await _context.Categorias.AnyAsync(c => c.Id == producto.CategoriaId))
            {
                ModelState.AddModelError(nameof(Producto.CategoriaId), "La categoría seleccionada no existe.");
            }
            if (!await _context.Usuarios.AnyAsync(u => u.Id == producto.UsuarioId && u.Rol == "vendedor"))
            {
                ModelState.AddModelError(nameof(Producto.UsuarioId), "El vendedor seleccionado no existe.");
            }

            if (producto.Estado is "activo" or "agotado") producto.Estado = producto.Stock == 0 ? "agotado" : "activo";
            if (ModelState.IsValid)
            {
                string? nueva = null;
                if (await Persistencia.GuardarAsync(this, _context, async () =>
                {
                    if (foto != null) nueva = await _imagenes!.GuardarAsync(foto);
                    producto.Imagen = nueva ?? "/images/demo/catalogo.svg";
                    producto.FechaPublicacion = DateTime.UtcNow;
                    _context.Add(producto);
                    await _context.SaveChangesAsync();
                })) return RedirectToAction(nameof(Index));
                _imagenes?.EliminarNueva(nueva);
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
        [ValidateAntiForgeryToken, RequestSizeLimit(4 * 1024 * 1024)]
        public async Task<IActionResult> Edit(long id,
            [Bind("Id,Nombre,Descripcion,Precio,Stock,Estado,UsuarioId,CategoriaId")] Producto producto, IFormFile? foto = null, int? stockAnterior = null)
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

            if (producto.Estado is "activo" or "agotado") producto.Estado = producto.Stock == 0 ? "agotado" : "activo";
            if (ModelState.IsValid)
            {
                string? nueva = null;
                if (await Persistencia.GuardarAsync(this, _context, async () =>
                {
                    await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                    var anterior = await _context.Productos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id)
                        ?? throw new System.ComponentModel.DataAnnotations.ValidationException("El registro ya no existe.");
                    if (!stockAnterior.HasValue || stockAnterior != anterior.Stock)
                        throw new System.ComponentModel.DataAnnotations.ValidationException("El stock cambió desde que abriste el formulario. Vuelve a abrir el producto antes de ajustar existencias.");
                    if (foto != null) nueva = await _imagenes!.GuardarAsync(foto);
                    producto.Imagen = nueva ?? anterior.Imagen;
                    producto.FechaPublicacion = anterior.FechaPublicacion;
                    _context.Update(producto);
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();
                })) return RedirectToAction(nameof(Index));
                _imagenes?.EliminarNueva(nueva);
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
            var producto = await _context.Productos.Include(p => p.Categoria).Include(p => p.Vendedor).FirstOrDefaultAsync(x => x.Id == id);
            if (producto == null) return NotFound();
            if (await _context.DetallesCarrito.AnyAsync(d => d.ProductoId == id) || await _context.DetallesPedido.AnyAsync(d => d.ProductoId == id))
            {
                ModelState.AddModelError(string.Empty, "No se puede eliminar el producto porque está asociado a carritos o pedidos.");
                return View("Delete", producto);
            }
            _context.Productos.Remove(producto);
            if (await Persistencia.GuardarAsync(this, _context)) return RedirectToAction(nameof(Index));
            return View("Delete", producto);
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

    }
}
