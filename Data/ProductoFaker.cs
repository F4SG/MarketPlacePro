using Bogus;
using MarketPlacePro.Models;

namespace MarketPlacePro.Data;

public sealed class ProductoFaker : Faker<Producto>
{
    public ProductoFaker(IReadOnlyList<long> vendedorIds, IReadOnlyList<long> categoriaIds) : base("es")
    {
        RuleFor(p => p.Nombre, f => f.Commerce.ProductName());
        RuleFor(p => p.Descripcion, f => f.Commerce.ProductDescription());
        RuleFor(p => p.Precio, f => decimal.Round(f.Random.Decimal(10, 2000), 2));
        RuleFor(p => p.Stock, f => f.Random.Int(0, 100));
        RuleFor(p => p.Estado, f => f.PickRandom(new[] { "activo", "activo", "activo", "agotado", "pausado" }));
        RuleFor(p => p.FechaPublicacion, f => f.Date.Past(1, DateTime.UtcNow));
        RuleFor(p => p.UsuarioId, f => f.PickRandom<long>(vendedorIds));
        RuleFor(p => p.CategoriaId, f => f.PickRandom<long>(categoriaIds));
    }
}
