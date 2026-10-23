namespace MarketPlacePro.Models;

public class Producto
{
    public long Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Precio { get; set; }
    public int Stock { get; set; }
    public string? Imagen { get; set; }
    public string Estado { get; set; } = "activo";
    public DateTime? FechaPublicacion { get; set; }

    public long UsuarioId { get; set; }
    public Usuario Vendedor { get; set; } = null!;

    public long CategoriaId { get; set; }
    public Categoria Categoria { get; set; } = null!;

    public ICollection<DetalleCarrito> DetallesCarrito { get; set; } = new List<DetalleCarrito>();
    public ICollection<DetallePedido> DetallesPedido { get; set; } = new List<DetallePedido>();
}
