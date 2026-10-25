using System.ComponentModel.DataAnnotations;

namespace MarketPlacePro.Models;

public class Categoria
{
    public long Id { get; set; }
    [Required(ErrorMessage = "Ingrese el nombre de la categoría.")]
    [StringLength(255, ErrorMessage = "El nombre admite hasta 255 caracteres.")]
    public string Nombre { get; set; } = string.Empty;
    [StringLength(1000, ErrorMessage = "La descripción admite hasta 1000 caracteres.")]
    public string? Descripcion { get; set; }
    [StringLength(255, ErrorMessage = "La imagen admite hasta 255 caracteres.")]
    public string? Imagen { get; set; }

    public ICollection<Producto> Productos { get; set; } = new List<Producto>();
}
