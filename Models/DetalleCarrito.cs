namespace MarketPlacePro.Models;

public class DetalleCarrito
{
    public long Id { get; set; }
    public int Cantidad { get; set; } = 1;
    public DateTime? FechaAgregado { get; set; }

    public long CarritoId { get; set; }
    public Carrito Carrito { get; set; } = null!;

    public long ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;
}
