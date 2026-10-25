using System.ComponentModel.DataAnnotations;

namespace MarketPlacePro.Models;

public class NuevoPedidoViewModel
{
    [Required(ErrorMessage = "Ingrese el número de pedido.")]
    [StringLength(50, ErrorMessage = "El número de pedido admite hasta 50 caracteres.")]
    public string NumeroPedido { get; set; } = string.Empty;
    [Required(ErrorMessage = "Seleccione un estado.")]
    [RegularExpression("^(pendiente|completada|cancelada|en_proceso)$", ErrorMessage = "Seleccione un estado válido.")]
    public string Estado { get; set; } = "pendiente";
    public long UsuarioId { get; set; }
    public long ProductoId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public int Cantidad { get; set; } = 1;
}
