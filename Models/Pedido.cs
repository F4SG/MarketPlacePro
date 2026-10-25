using System.ComponentModel.DataAnnotations;

namespace MarketPlacePro.Models;

public class Pedido
{
    public long Id { get; set; }
    [Required(ErrorMessage = "Ingrese el número de pedido.")]
    [StringLength(50, ErrorMessage = "El número de pedido admite hasta 50 caracteres.")]
    public string NumeroPedido { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    [Required(ErrorMessage = "Seleccione un estado.")]
    [StringLength(20)]
    [RegularExpression("^(pendiente|completada|cancelada|en_proceso)$", ErrorMessage = "Seleccione un estado válido.")]
    public string Estado { get; set; } = "pendiente";
    public DateTime? FechaPedido { get; set; }

    public long UsuarioId { get; set; }
    public Usuario Comprador { get; set; } = null!;

    public ICollection<DetallePedido> Detalles { get; set; } = new List<DetallePedido>();
    public Pago? Pago { get; set; }
}
