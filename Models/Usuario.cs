using System.ComponentModel.DataAnnotations;

namespace MarketPlacePro.Models;

public class Usuario
{
    public long Id { get; set; }
    [Required(ErrorMessage = "Ingrese el nombre completo.")]
    [StringLength(255, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 255 caracteres.")]
    [RegularExpression(@"^ *[A-Za-zÀ-ÖØ-öø-ÿ]+(?:[ '’-][A-Za-zÀ-ÖØ-öø-ÿ]+)* *$", ErrorMessage = "Use letras, espacios, apóstrofes o guiones; no se permiten números.")]
    public string Nombre { get; set; } = string.Empty;
    [Required(ErrorMessage = "Ingrese el correo.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
    [StringLength(255, ErrorMessage = "El correo admite hasta 255 caracteres.")]
    public string Correo { get; set; } = string.Empty;
    [StringLength(255, ErrorMessage = "La contraseña admite hasta 255 caracteres.")]
    public string Contrasena { get; set; } = string.Empty;
    [CiudadBolivia]
    [StringLength(255)]
    public string? Ciudad { get; set; }
    [RegularExpression(@"^(?:\+591 ?)?[0-9]{8}$", ErrorMessage = "Ingrese 8 dígitos, opcionalmente precedidos por +591.")]
    [StringLength(50)]
    public string? Telefono { get; set; }
    public DateTime? FechaRegistro { get; set; }
    [Required(ErrorMessage = "Seleccione un rol.")]
    [StringLength(50)]
    [RegularExpression("^(comprador|vendedor|admin)$", ErrorMessage = "Seleccione un rol válido.")]
    public string Rol { get; set; } = "comprador";
    [StringLength(255, ErrorMessage = "La tienda admite hasta 255 caracteres.")]
    public string? NombreTienda { get; set; }
    [StringLength(1000, ErrorMessage = "La descripción admite hasta 1000 caracteres.")]
    public string? DescripcionTienda { get; set; }

    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
    public Carrito? Carrito { get; set; }
    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}
