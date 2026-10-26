using System.ComponentModel.DataAnnotations;
namespace MarketPlacePro.Models;

public class CobroViewModel
{
    [Range(1, long.MaxValue, ErrorMessage = "Seleccione un pedido.")]
    public long PedidoId { get; set; }
    [Required(ErrorMessage = "Seleccione el método de pago.")]
    [RegularExpression("^(efectivo|transferencia|qr)$", ErrorMessage = "Seleccione un método válido.")]
    public string Metodo { get; set; } = "efectivo";
    [StringLength(255)]
    public string? Referencia { get; set; }
    public bool ConfirmarRecepcion { get; set; }
    public decimal Total { get; set; }
    public string NumeroPedido { get; set; } = "";
    public string Comprador { get; set; } = "";
}
