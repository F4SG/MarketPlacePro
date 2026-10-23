namespace MarketPlacePro.Models;

public class Categoria
{
    public long Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string? Imagen { get; set; }

    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
