namespace MarketPlacePro.Models;

public class Pago
{
    public long Id { get; set; }
    public string Metodo { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string Estado { get; set; } = "pendiente";
    public string? Referencia { get; set; }
    public DateTime FechaPago { get; set; }

    public long PedidoId { get; set; }
    public Pedido Pedido { get; set; } = null!;
}
