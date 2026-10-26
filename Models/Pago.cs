using System.ComponentModel.DataAnnotations;

namespace MarketPlacePro.Models;

public class Pago
{
    public long Id { get; set; }
    [Required(ErrorMessage = "Ingrese el método de pago.")]
    [StringLength(100, ErrorMessage = "El método admite hasta 100 caracteres.")]
    public string Metodo { get; set; } = string.Empty;
    [Range(typeof(decimal), "0.01", "99999999.99", ErrorMessage = "El monto debe estar entre 0.01 y 99999999.99.")]
    [ImporteMonetario]
    public decimal Monto { get; set; }
    [Required(ErrorMessage = "Seleccione un estado.")]
    [RegularExpression("^(pendiente|completado|rechazado|reembolsado)$", ErrorMessage = "Seleccione un estado válido.")]
    [StringLength(20)]
    public string Estado { get; set; } = "pendiente";
    [StringLength(255, ErrorMessage = "La referencia admite hasta 255 caracteres.")]
    public string? Referencia { get; set; }
    public DateTime FechaPago { get; set; }

    public long PedidoId { get; set; }
    public Pedido Pedido { get; set; } = null!;
}
