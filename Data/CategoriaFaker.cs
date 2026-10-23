using Bogus;
using MarketPlacePro.Models;

namespace MarketPlacePro.Data;

public sealed class CategoriaFaker : Faker<Categoria>
{
    private static readonly string[] Nombres =
        { "Electrónica", "Ropa", "Hogar", "Deportes", "Libros", "Juguetes" };

    private int _indice;

    public CategoriaFaker() : base("es")
    {
        RuleFor(c => c.Nombre, f => Nombres[_indice++ % Nombres.Length]);
        RuleFor(c => c.Descripcion, f => f.Lorem.Sentence());
    }
}
