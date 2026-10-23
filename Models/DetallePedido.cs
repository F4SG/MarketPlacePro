namespace MarketPlacePro.Models;

public class DetallePedido
{
    public long Id { get; set; }
    public string NombreProducto { get; set; } = string.Empty;
    public decimal PrecioUnitario { get; set; }
    public int Cantidad { get; set; }
    public decimal Subtotal { get; set; }

    public long PedidoId { get; set; }
    public Pedido Pedido { get; set; } = null!;

    public long ProductoId { get; set; }
    public Producto Producto { get; set; } = null!;
}
