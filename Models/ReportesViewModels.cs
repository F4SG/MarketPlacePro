namespace MarketPlacePro.Models;

public class VentaPorProductoDto
{
    public string Producto { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public int CantidadVendida { get; set; }
    public decimal MontoTotal { get; set; }
}

public class ReporteVentasViewModel
{
    public DateTime Desde { get; set; }
    public DateTime Hasta { get; set; }
    public string Estado { get; set; } = "completada";
    public List<VentaPorProductoDto> Filas { get; set; } = new();
    public int TotalPedidos { get; set; }
    public decimal MontoTotal { get; set; }
    public string? ProductoMasVendido { get; set; }
}
