using System.ComponentModel.DataAnnotations;

namespace MarketPlacePro.Models;

public class Carrito
{
    public long Id { get; set; }
    [Required(ErrorMessage = "Seleccione un estado.")]
    [StringLength(20)]
    [RegularExpression("^(activo|abandonado|convertido)$", ErrorMessage = "Seleccione un estado válido.")]
    public string Estado { get; set; } = "activo";
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }

    public long UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;

    public ICollection<DetalleCarrito> Detalles { get; set; } = new List<DetalleCarrito>();
}
