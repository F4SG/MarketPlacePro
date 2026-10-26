using MarketPlacePro.Data;
using MarketPlacePro.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MarketPlacePro.Controllers;

public class PagosController : Controller
{
    private readonly MarketPlaceProContext _context;
    public PagosController(MarketPlaceProContext context) => _context = context;

    public async Task<IActionResult> Index() => View(await _context.Pagos.AsNoTracking().Include(p => p.Pedido)
        .OrderByDescending(p => p.FechaPago).ToListAsync());

    public async Task<IActionResult> Details(long? id)
    {
        var pago = await _context.Pagos.AsNoTracking().Include(p => p.Pedido).SingleOrDefaultAsync(p => p.Id == id);
        return pago == null ? NotFound() : View(pago);
    }

    public async Task<IActionResult> Create(long? pedidoId = null)
    {
        if (!pedidoId.HasValue) return RedirectToAction("Index", "Pedidos");
        var pedido = await _context.Pedidos.AsNoTracking().Include(p => p.Comprador).Include(p => p.Pago)
            .SingleOrDefaultAsync(p => p.Id == pedidoId);
        if (pedido == null) return NotFound();
        if (!Ventas.EstaAbierto(pedido)) return RedirectToAction("Details", "Pedidos", new { id = pedido.Id });
        return View(new CobroViewModel { PedidoId = pedido.Id, NumeroPedido = pedido.NumeroPedido,
            Total = pedido.Total, Comprador = pedido.Comprador.Nombre });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("PedidoId,Metodo,Referencia,ConfirmarRecepcion")] CobroViewModel formulario)
    {
        if (!formulario.ConfirmarRecepcion)
            ModelState.AddModelError(nameof(formulario.ConfirmarRecepcion), "Confirma que recibiste o verificaste el dinero.");
        if (formulario.Metodo != "efectivo" && string.IsNullOrWhiteSpace(formulario.Referencia))
            ModelState.AddModelError(nameof(formulario.Referencia), "Ingrese la referencia de la transferencia o del pago QR.");
        if (ModelState.IsValid && await Persistencia.GuardarAsync(this, _context,
            () => Ventas.ConfirmarCobroAsync(_context, formulario.PedidoId, formulario.Metodo, formulario.Referencia)))
        {
            TempData["Exito"] = "Venta finalizada: pago confirmado, pedido completado y stock actualizado.";
            return RedirectToAction("Details", "Pedidos", new { id = formulario.PedidoId });
        }
        var pedido = await _context.Pedidos.AsNoTracking().Include(p => p.Comprador).SingleOrDefaultAsync(p => p.Id == formulario.PedidoId);
        if (pedido == null) return NotFound();
        formulario.Total = pedido.Total; formulario.NumeroPedido = pedido.NumeroPedido; formulario.Comprador = pedido.Comprador.Nombre;
        return View(formulario);
    }

    public async Task<IActionResult> Edit(long? id)
    {
        var pago = await _context.Pagos.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id);
        if (pago == null) return NotFound();
        return pago.Estado is "completado" or "reembolsado"
            ? RedirectToAction(nameof(Details), new { id = pago.Id })
            : RedirectToAction(nameof(Create), new { pedidoId = pago.PedidoId });
    }
}
