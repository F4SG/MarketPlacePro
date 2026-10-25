using System.ComponentModel.DataAnnotations;

namespace MarketPlacePro.Models;

[AttributeUsage(AttributeTargets.Property)]
public sealed class ImporteMonetarioAttribute : ValidationAttribute
{
    public ImporteMonetarioAttribute() : base("Use como máximo dos decimales.") { }

    public override bool IsValid(object? value) => value is not decimal importe || decimal.Round(importe, 2) == importe;
}
