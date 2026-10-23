namespace MarketPlacePro.Models;

public class Pedido
{
    public long Id { get; set; }
    public string NumeroPedido { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public string Estado { get; set; } = "pendiente";
    public DateTime? FechaPedido { get; set; }

    public long UsuarioId { get; set; }
    public Usuario Comprador { get; set; } = null!;

    public ICollection<DetallePedido> Detalles { get; set; } = new List<DetallePedido>();
    public Pago? Pago { get; set; }
}
