
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
                .Include(p => p.Detalles)
                .Include(p => p.Pago)
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
            CargarProductos();
            return View(new NuevoPedidoViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("NumeroPedido,Estado,UsuarioId,ProductoId,Cantidad")] NuevoPedidoViewModel formulario)
        {
            formulario.NumeroPedido = formulario.NumeroPedido?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(formulario.NumeroPedido))
                ModelState.AddModelError(nameof(formulario.NumeroPedido), "El número de pedido es obligatorio.");
            else if (await _context.Pedidos.AnyAsync(p => p.NumeroPedido == formulario.NumeroPedido))
                ModelState.AddModelError(nameof(formulario.NumeroPedido), "Ya existe un pedido con ese número.");
            if (!await _context.Usuarios.AnyAsync(u => u.Id == formulario.UsuarioId))
                ModelState.AddModelError(nameof(formulario.UsuarioId), "El comprador seleccionado no existe.");
            if (!_estados.Contains(formulario.Estado))
                ModelState.AddModelError(nameof(formulario.Estado), "El estado seleccionado no es válido.");
            if (formulario.Cantidad <= 0)
                ModelState.AddModelError(nameof(formulario.Cantidad), "La cantidad debe ser mayor que cero.");
            var producto = await _context.Productos.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == formulario.ProductoId && p.Estado == "activo");
            if (producto == null)
                ModelState.AddModelError(nameof(formulario.ProductoId), "Seleccione un producto activo para el primer detalle.");

            if (ModelState.IsValid && producto != null)
            {
                var pedido = new Pedido
                {
                    NumeroPedido = formulario.NumeroPedido,
                    Estado = formulario.Estado,
                    UsuarioId = formulario.UsuarioId,
                    FechaPedido = DateTime.UtcNow,
                    Detalles = new List<DetallePedido>
                    {
                        new DetallePedido
                        {
                            ProductoId = producto.Id,
                            NombreProducto = producto.Nombre,
                            PrecioUnitario = producto.Precio,
                            Cantidad = formulario.Cantidad,
                            Subtotal = producto.Precio * formulario.Cantidad
                        }
                    }
                };
                if (await Persistencia.GuardarAsync(this, _context,
                    () => TotalesPedido.CrearAsync(_context, pedido)))
                    return RedirectToAction(nameof(Details), new { id = pedido.Id });
            }
            CargarCompradores(formulario.UsuarioId);
            CargarEstados(formulario.Estado);
            CargarProductos(formulario.ProductoId);
            return View(formulario);
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
            [Bind("Id,NumeroPedido,Estado,UsuarioId")] Pedido pedido)
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
                pedidoExistente.Estado = pedido.Estado;
                pedidoExistente.UsuarioId = pedido.UsuarioId;

                if (await Persistencia.GuardarAsync(this, _context,
                    () => TotalesPedido.GuardarAsync(_context, id)))
                    return RedirectToAction(nameof(Details), new { id });
            }

            pedido.Subtotal = pedidoExistente.Subtotal;
            pedido.Total = pedidoExistente.Total;
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

        private void CargarProductos(long? productoSeleccionado = null)
        {
            var productos = _context.Productos.AsNoTracking().Where(p => p.Estado == "activo")
                .OrderBy(p => p.Nombre).ThenBy(p => p.Id)
                .Select(p => new { p.Id, Texto = p.Nombre + " (#" + p.Id + ")" }).ToList();
            ViewData["Productos"] = new SelectList(productos, "Id", "Texto", productoSeleccionado);
        }

        private void CargarEstados(string? estadoSeleccionado = null)
        {
            ViewData["Estados"] = new SelectList(
                _estados,
                estadoSeleccionado);
        }
    }
}
