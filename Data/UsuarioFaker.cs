using Bogus;
using MarketPlacePro.Models;

namespace MarketPlacePro.Data;

public sealed class UsuarioFaker : Faker<Usuario>
{
    public UsuarioFaker() : base("es")
    {
        RuleFor(u => u.Nombre, f => f.Name.FullName());
        RuleFor(u => u.Correo, (f, u) => f.Internet.Email(u.Nombre));
        RuleFor(u => u.Contrasena, f => f.Internet.Password());
        RuleFor(u => u.Ciudad, f => f.Address.City());
        RuleFor(u => u.Telefono, f => f.Phone.PhoneNumber());
        RuleFor(u => u.FechaRegistro, f => f.Date.Past(2, DateTime.UtcNow));
        RuleFor(u => u.Rol, f => f.PickRandom("comprador", "vendedor"));
        RuleFor(u => u.NombreTienda, (f, u) => u.Rol == "vendedor" ? f.Company.CompanyName() : null);
        RuleFor(u => u.DescripcionTienda, (f, u) => u.Rol == "vendedor" ? f.Lorem.Sentence() : null);
    }
}
