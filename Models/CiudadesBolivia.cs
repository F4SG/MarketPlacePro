using System.ComponentModel.DataAnnotations;

namespace MarketPlacePro.Models;

public static class CiudadesBolivia
{
    public static IReadOnlyList<string> Disponibles { get; } = Array.AsReadOnly(new[]
    {
        "Cobija", "Cochabamba", "El Alto", "La Paz", "Oruro", "Potosí",
        "Santa Cruz de la Sierra", "Sucre", "Tarija", "Trinidad"
    });

    public static string? Normalizar(string? ciudad) => string.IsNullOrWhiteSpace(ciudad) ? null
        : Disponibles.FirstOrDefault(c => string.Equals(c, ciudad.Trim(), StringComparison.OrdinalIgnoreCase)) ?? ciudad.Trim();
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class CiudadBoliviaAttribute : ValidationAttribute
{
    public CiudadBoliviaAttribute() : base("Seleccione una ciudad de la lista disponible.") { }

    public override bool IsValid(object? value) => value == null || value is string ciudad &&
        (string.IsNullOrWhiteSpace(ciudad) || CiudadesBolivia.Disponibles.Contains(ciudad.Trim(), StringComparer.OrdinalIgnoreCase));
}
