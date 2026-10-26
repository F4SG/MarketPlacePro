using Bogus;
using Microsoft.AspNetCore.Identity;
using MarketPlacePro.Models;

namespace MarketPlacePro.Data;

public sealed class UsuarioFaker : Faker<Usuario>
{
    public UsuarioFaker() : base("es")
    {
        RuleFor(u => u.Nombre, f => f.Name.FirstName() + " " + f.Name.LastName());
        RuleFor(u => u.Correo, (f, u) => f.Internet.Email(u.Nombre));
        RuleFor(u => u.Contrasena, (f, u) => new PasswordHasher<Usuario>().HashPassword(u, f.Internet.Password(12)));
        RuleFor(u => u.Ciudad, f => f.PickRandom(CiudadesBolivia.Disponibles.ToArray()));
        RuleFor(u => u.Telefono, f => f.Random.Int(0, 1) == 0 ? "6" + f.Random.ReplaceNumbers("#######") : "7" + f.Random.ReplaceNumbers("#######"));
        RuleFor(u => u.FechaRegistro, f => f.Date.Past(2, DateTime.UtcNow));
        RuleFor(u => u.Rol, f => f.PickRandom("comprador", "vendedor"));
        RuleFor(u => u.NombreTienda, (f, u) => u.Rol == "vendedor" ? f.Company.CompanyName() : null);
        RuleFor(u => u.DescripcionTienda, (f, u) => u.Rol == "vendedor" ? f.Lorem.Sentence() : null);
    }
}
