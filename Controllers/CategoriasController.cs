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
    public class CategoriasController : Controller
    {
        private readonly AlmacenImagenes? _imagenes;
        private readonly MarketPlaceProContext _context;

        public CategoriasController(MarketPlaceProContext context, AlmacenImagenes? imagenes = null)
        {
            _context = context;
            _imagenes = imagenes;
        }

        // GET: Categorias
        public async Task<IActionResult> Index()
        {
            return View(await _context.Categorias.ToListAsync());
        }

        // GET: Categorias/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var categoria = await _context.Categorias
                .FirstOrDefaultAsync(m => m.Id == id);
            if (categoria == null)
            {
                return NotFound();
            }

            return View(categoria);
        }

        // GET: Categorias/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Categorias/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken, RequestSizeLimit(4 * 1024 * 1024)]
        public async Task<IActionResult> Create([Bind("Nombre,Descripcion")] Categoria categoria, IFormFile? foto = null)
        {
            if (ModelState.IsValid)
            {
                string? nueva = null;
                if (await Persistencia.GuardarAsync(this, _context, async () =>
                {
                    if (foto != null) nueva = await _imagenes!.GuardarAsync(foto);
                    categoria.Imagen = nueva ?? "/images/demo/catalogo.svg";
                    _context.Add(categoria);
                    await _context.SaveChangesAsync();
                })) return RedirectToAction(nameof(Index));
                _imagenes?.EliminarNueva(nueva);
            }
            return View(categoria);
        }

        // GET: Categorias/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var categoria = await _context.Categorias.FindAsync(id);
            if (categoria == null)
            {
                return NotFound();
            }
            return View(categoria);
        }

        // POST: Categorias/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken, RequestSizeLimit(4 * 1024 * 1024)]
        public async Task<IActionResult> Edit(long id, [Bind("Id,Nombre,Descripcion")] Categoria categoria, IFormFile? foto = null)
        {
            if (id != categoria.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                string? nueva = null;
                if (await Persistencia.GuardarAsync(this, _context, async () =>
                {
                    var anterior = await _context.Categorias.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id)
                        ?? throw new System.ComponentModel.DataAnnotations.ValidationException("El registro ya no existe.");
                    if (foto != null) nueva = await _imagenes!.GuardarAsync(foto);
                    categoria.Imagen = nueva ?? anterior.Imagen;
                    _context.Update(categoria);
                    await _context.SaveChangesAsync();
                })) return RedirectToAction(nameof(Index));
                _imagenes?.EliminarNueva(nueva);
            }
            return View(categoria);
        }

        // GET: Categorias/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var categoria = await _context.Categorias
                .FirstOrDefaultAsync(m => m.Id == id);
            if (categoria == null)
            {
                return NotFound();
            }

            return View(categoria);
        }

        // POST: Categorias/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var categoria = await _context.Categorias.FirstOrDefaultAsync(x => x.Id == id);
            if (categoria == null) return NotFound();
            if (await _context.Productos.AnyAsync(p => p.CategoriaId == id))
            {
                ModelState.AddModelError(string.Empty, "No se puede eliminar la categoría porque tiene productos asociados.");
                return View("Delete", categoria);
            }
            _context.Categorias.Remove(categoria);
            if (await Persistencia.GuardarAsync(this, _context)) return RedirectToAction(nameof(Index));
            return View("Delete", categoria);
        }

    }
}
