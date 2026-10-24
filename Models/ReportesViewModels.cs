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

public class ProductoBajoStockDto
{
    public string Producto { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Vendedor { get; set; } = string.Empty;
    public int Stock { get; set; }
    public decimal Precio { get; set; }
}

public class ReporteStockViewModel
{
    public int Umbral { get; set; } = 5;
    public long? CategoriaId { get; set; }
    public List<ProductoBajoStockDto> Filas { get; set; } = new();
    public int CantidadBajoUmbral { get; set; }
    public decimal ValorEnRiesgo { get; set; }
}
