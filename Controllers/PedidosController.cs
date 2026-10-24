
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MarketPlacePro.Data;
using MarketPlacePro.Models;

namespace MarketPlacePro.Controllers
{
    public class PedidosController : Controller
    {
        private readonly MarketPlaceProContext _context;

        private readonly string[] _estados =
        {
            "pendiente",
            "completada",
            "cancelada",
            "en_proceso"
        };

        public PedidosController(MarketPlaceProContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var pedidos = await _context.Pedidos
                .AsNoTracking()
                .Include(p => p.Comprador)
                .OrderByDescending(p => p.FechaPedido)
                .ToListAsync();

            return View(pedidos);
        }

        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pedido = await _context.Pedidos
                .AsNoTracking()
                .Include(p => p.Comprador)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pedido == null)
            {
                return NotFound();
            }

            return View(pedido);
        }

        public IActionResult Create()
        {
            CargarCompradores();
            CargarEstados();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("NumeroPedido,Subtotal,Total,Estado,UsuarioId")] Pedido pedido)
        {
            pedido.NumeroPedido = pedido.NumeroPedido?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(pedido.NumeroPedido))
            {
                ModelState.AddModelError(
                    nameof(pedido.NumeroPedido),
                    "El número de pedido es obligatorio.");
            }
            else if (await _context.Pedidos.AnyAsync(
                p => p.NumeroPedido == pedido.NumeroPedido))
            {
                ModelState.AddModelError(
                    nameof(pedido.NumeroPedido),
                    "Ya existe un pedido con ese número.");
            }

            if (!await _context.Usuarios.AnyAsync(u => u.Id == pedido.UsuarioId))
            {
                ModelState.AddModelError(
                    nameof(pedido.UsuarioId),
                    "El comprador seleccionado no existe.");
            }

            if (!_estados.Contains(pedido.Estado))
            {
                ModelState.AddModelError(
                    nameof(pedido.Estado),
                    "El estado seleccionado no es válido.");
            }

            if (ModelState.IsValid)
            {
                pedido.FechaPedido = DateTime.UtcNow;

                _context.Pedidos.Add(pedido);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            CargarCompradores(pedido.UsuarioId);
            CargarEstados(pedido.Estado);

            return View(pedido);
        }

        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pedido = await _context.Pedidos.FindAsync(id);

            if (pedido == null)
            {
                return NotFound();
            }

            CargarCompradores(pedido.UsuarioId);
            CargarEstados(pedido.Estado);

            return View(pedido);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            long id,
            [Bind("Id,NumeroPedido,Subtotal,Total,Estado,UsuarioId")] Pedido pedido)
        {
            if (id != pedido.Id)
            {
                return NotFound();
            }

            pedido.NumeroPedido = pedido.NumeroPedido?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(pedido.NumeroPedido))
            {
                ModelState.AddModelError(
                    nameof(pedido.NumeroPedido),
                    "El número de pedido es obligatorio.");
            }
            else if (await _context.Pedidos.AnyAsync(
                p => p.NumeroPedido == pedido.NumeroPedido && p.Id != id))
            {
                ModelState.AddModelError(
                    nameof(pedido.NumeroPedido),
                    "Ya existe otro pedido con ese número.");
            }

            if (!await _context.Usuarios.AnyAsync(u => u.Id == pedido.UsuarioId))
            {
                ModelState.AddModelError(
                    nameof(pedido.UsuarioId),
                    "El comprador seleccionado no existe.");
            }

            if (!_estados.Contains(pedido.Estado))
            {
                ModelState.AddModelError(
                    nameof(pedido.Estado),
                    "El estado seleccionado no es válido.");
            }

            var pedidoExistente = await _context.Pedidos.FindAsync(id);

            if (pedidoExistente == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                pedidoExistente.NumeroPedido = pedido.NumeroPedido;
                pedidoExistente.Subtotal = pedido.Subtotal;
                pedidoExistente.Total = pedido.Total;
                pedidoExistente.Estado = pedido.Estado;
                pedidoExistente.UsuarioId = pedido.UsuarioId;

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            CargarCompradores(pedido.UsuarioId);
            CargarEstados(pedido.Estado);

            return View(pedido);
        }

        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pedido = await _context.Pedidos
                .AsNoTracking()
                .Include(p => p.Comprador)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pedido == null)
            {
                return NotFound();
            }

            return View(pedido);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var pedido = await _context.Pedidos.FindAsync(id);

            if (pedido == null)
            {
                return NotFound();
            }

            _context.Pedidos.Remove(pedido);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private void CargarCompradores(long? usuarioSeleccionado = null)
        {
            ViewData["UsuarioId"] = new SelectList(
                _context.Usuarios
                    .AsNoTracking()
                    .OrderBy(u => u.Nombre),
                "Id",
                "Nombre",
                usuarioSeleccionado);
        }

        private void CargarEstados(string? estadoSeleccionado = null)
        {
            ViewData["Estados"] = new SelectList(
                _estados,
                estadoSeleccionado);
        }
    }
}
