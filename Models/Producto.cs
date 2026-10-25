using System.ComponentModel.DataAnnotations;

namespace MarketPlacePro.Models;

public class Producto
{
    public long Id { get; set; }
    [Required(ErrorMessage = "Ingrese el nombre del producto.")]
    [StringLength(255, ErrorMessage = "El nombre admite hasta 255 caracteres.")]
    public string Nombre { get; set; } = string.Empty;
    [StringLength(2000, ErrorMessage = "La descripción admite hasta 2000 caracteres.")]
    public string? Descripcion { get; set; }
    [Range(typeof(decimal), "0.01", "99999999.99", ErrorMessage = "El precio debe estar entre 0.01 y 99999999.99.")]
    [ImporteMonetario]
    public decimal Precio { get; set; }
    [Range(0, int.MaxValue, ErrorMessage = "El stock no puede ser negativo.")]
    public int Stock { get; set; }
    [StringLength(255, ErrorMessage = "La imagen admite hasta 255 caracteres.")]
    public string? Imagen { get; set; }
    [Required(ErrorMessage = "Seleccione un estado.")]
    [StringLength(20)]
    [RegularExpression("^(activo|agotado|inactivo|pausado)$", ErrorMessage = "Seleccione un estado válido.")]
    public string Estado { get; set; } = "activo";
    public DateTime? FechaPublicacion { get; set; }

    public long UsuarioId { get; set; }
    public Usuario Vendedor { get; set; } = null!;

    public long CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    public ICollection<DetalleCarrito> DetallesCarrito { get; set; } = new List<DetalleCarrito>();
    public ICollection<DetallePedido> DetallesPedido { get; set; } = new List<DetallePedido>();
}
