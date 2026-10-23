namespace MarketPlacePro.Models;

public class Usuario
{
    public long Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Contrasena { get; set; } = string.Empty;
    public string? Ciudad { get; set; }
    public string? Telefono { get; set; }
    public DateTime? FechaRegistro { get; set; }
    public string Rol { get; set; } = "comprador";
    public string? NombreTienda { get; set; }
    public string? DescripcionTienda { get; set; }

    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
    public Carrito? Carrito { get; set; }
    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}
