using System.ComponentModel.DataAnnotations;
namespace MarketPlacePro.Data;

public sealed class AlmacenImagenes
{
    public const int MaximoBytes = 3 * 1024 * 1024;
    private readonly string raiz;
    public AlmacenImagenes(IWebHostEnvironment entorno) => raiz = Path.Combine(entorno.WebRootPath, "uploads");

    public async Task<string> GuardarAsync(IFormFile archivo)
    {
        if (archivo.Length <= 0 || archivo.Length > MaximoBytes)
            throw new ValidationException("La imagen debe ocupar entre 1 byte y 3 MB.");
        await using var entrada = archivo.OpenReadStream();
        using var datos = new MemoryStream();
        var buffer = new byte[81920];
        int leidos;
        while ((leidos = await entrada.ReadAsync(buffer)) > 0)
        {
            if (datos.Length + leidos > MaximoBytes) throw new ValidationException("La imagen supera los 3 MB.");
            datos.Write(buffer, 0, leidos);
        }
        var bytes = datos.ToArray();
        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        var png = bytes.Length >= 24 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})
            && bytes.AsSpan(12,4).SequenceEqual("IHDR"u8);
        var jpg = bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255;
        var webp = bytes.Length >= 16 && bytes.AsSpan(0,4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8,4).SequenceEqual("WEBP"u8);
        if (!((png && extension == ".png") || (jpg && extension is ".jpg" or ".jpeg") || (webp && extension == ".webp")))
            throw new ValidationException("Adjunta una imagen PNG, JPG o WebP con contenido y extensión coincidentes.");
        Directory.CreateDirectory(raiz);
        var nombre = Guid.NewGuid().ToString("N") + (jpg ? ".jpg" : extension);
        await File.WriteAllBytesAsync(Path.Combine(raiz, nombre), bytes);
        return "/uploads/" + nombre;
    }

    public void EliminarNueva(string? ruta)
    {
        if (ruta?.StartsWith("/uploads/", StringComparison.Ordinal) == true &&
            Guid.TryParseExact(Path.GetFileNameWithoutExtension(ruta), "N", out _))
        {
            var nombre = Path.GetFileName(ruta);
            var destino = Path.GetFullPath(Path.Combine(raiz, nombre));
            if (Path.GetDirectoryName(destino) == Path.GetFullPath(raiz) && File.Exists(destino)) File.Delete(destino);
        }
    }
}
