
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MarketPlacePro.Data;
using MarketPlacePro.Models;

namespace MarketPlacePro.Controllers
{
    public class PagosController : Controller
    {
        private readonly MarketPlaceProContext _context;

        private static readonly string[] EstadosPermitidos =
        {
            "pendiente",
            "completado",
            "rechazado",
            "reembolsado"
        };

        public PagosController(MarketPlaceProContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var pagos = await _context.Pagos
                .AsNoTracking()
                .Include(p => p.Pedido)
                .OrderByDescending(p => p.FechaPago)
                .ToListAsync();

            return View(pagos);
        }

        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
                return NotFound();

            var pago = await _context.Pagos
                .AsNoTracking()
                .Include(p => p.Pedido)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pago == null)
                return NotFound();

            return View(pago);
        }

        public async Task<IActionResult> Create()
        {
            await CargarPedidos();
            return View(new Pago { FechaPago = DateTime.Now, Estado = "pendiente" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Metodo,Monto,Estado,Referencia,FechaPago,PedidoId")] Pago pago)
        {
            ValidarPago(pago);

            var pedido = await _context.Pedidos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == pago.PedidoId);

            if (pedido == null)
            {
                ModelState.AddModelError(nameof(pago.PedidoId), "El pedido seleccionado no existe.");
            }
            else if (await _context.Pagos.AnyAsync(p => p.PedidoId == pago.PedidoId))
            {
                ModelState.AddModelError(nameof(pago.PedidoId), "Este pedido ya tiene un pago registrado.");
            }

            if (ModelState.IsValid)
            {
                pago.Metodo = pago.Metodo.Trim();
                pago.Estado = pago.Estado.Trim().ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(pago.Referencia))
                    pago.Referencia = null;
                else
                    pago.Referencia = pago.Referencia.Trim();

                if (pago.FechaPago == default)
                    pago.FechaPago = DateTime.Now;

                _context.Pagos.Add(pago);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            await CargarPedidos(pago.PedidoId);
            return View(pago);
        }

        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
                return NotFound();

            var pago = await _context.Pagos.FindAsync(id);

            if (pago == null)
                return NotFound();

            await CargarPedidos(pago.PedidoId, pago.Id);
            return View(pago);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Id,Metodo,Monto,Estado,Referencia,FechaPago,PedidoId")] Pago pago)
        {
            if (id != pago.Id)
                return NotFound();

            var pagoExistente = await _context.Pagos.FindAsync(id);

            if (pagoExistente == null)
                return NotFound();

            ValidarPago(pago);

            var pedido = await _context.Pedidos
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == pago.PedidoId);

            if (pedido == null)
            {
                ModelState.AddModelError(nameof(pago.PedidoId), "El pedido seleccionado no existe.");
            }
            else if (await _context.Pagos.AnyAsync(p => p.PedidoId == pago.PedidoId && p.Id != pago.Id))
            {
                ModelState.AddModelError(nameof(pago.PedidoId), "Este pedido ya tiene otro pago registrado.");
            }

            if (ModelState.IsValid)
            {
                pagoExistente.Metodo = pago.Metodo.Trim();
                pagoExistente.Monto = pago.Monto;
                pagoExistente.Estado = pago.Estado.Trim().ToLowerInvariant();
                pagoExistente.Referencia = string.IsNullOrWhiteSpace(pago.Referencia)
                    ? null
                    : pago.Referencia.Trim();
                pagoExistente.FechaPago = pago.FechaPago == default
                    ? pagoExistente.FechaPago
                    : pago.FechaPago;
                pagoExistente.PedidoId = pago.PedidoId;

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            await CargarPedidos(pago.PedidoId, pago.Id);
            return View(pago);
        }

        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
                return NotFound();

            var pago = await _context.Pagos
                .AsNoTracking()
                .Include(p => p.Pedido)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pago == null)
                return NotFound();

            return View(pago);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var pago = await _context.Pagos.FindAsync(id);

            if (pago != null)
            {
                _context.Pagos.Remove(pago);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private void ValidarPago(Pago pago)
        {
            if (string.IsNullOrWhiteSpace(pago.Metodo))
                ModelState.AddModelError(nameof(pago.Metodo), "El método de pago es obligatorio.");

            if (pago.Monto <= 0)
                ModelState.AddModelError(nameof(pago.Monto), "El monto debe ser mayor que cero.");

            if (string.IsNullOrWhiteSpace(pago.Estado) ||
                !EstadosPermitidos.Contains(pago.Estado.Trim().ToLowerInvariant()))
            {
                ModelState.AddModelError(nameof(pago.Estado), "El estado seleccionado no es válido.");
            }
        }

        private async Task CargarPedidos(long? pedidoId = null, long? pagoId = null)
        {
            var pedidos = await _context.Pedidos
                .AsNoTracking()
                .Where(p => !_context.Pagos.Any(pg =>
                    pg.PedidoId == p.Id &&
                    (!pagoId.HasValue || pg.Id != pagoId.Value)))
                .OrderBy(p => p.NumeroPedido)
                .ToListAsync();

            ViewData["PedidoId"] = new SelectList(pedidos, "Id", "NumeroPedido", pedidoId);
        }
    }
}
