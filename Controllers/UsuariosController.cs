using System.ComponentModel.DataAnnotations;
using MarketPlacePro.Data;
using MarketPlacePro.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MarketPlacePro.Controllers;

public class UsuariosController : Controller
{
    private readonly MarketPlaceProContext _context;
    public UsuariosController(MarketPlaceProContext context) => _context = context;

    public async Task<IActionResult> Index() => View(await _context.Usuarios.AsNoTracking().OrderBy(u => u.Nombre).ToListAsync());

    public async Task<IActionResult> Details(long? id)
    {
        var usuario = await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        return usuario == null ? NotFound() : View(usuario);
    }

    public IActionResult Create() => View(new Usuario { FechaRegistro = DateTime.Now });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Nombre,Correo,Contrasena,Ciudad,Telefono,Rol,NombreTienda,DescripcionTienda")] Usuario usuario)
    {
        await ValidarAsync(usuario, true);
        if (ModelState.IsValid)
        {
            usuario.FechaRegistro = DateTime.UtcNow;
            usuario.Contrasena = new PasswordHasher<Usuario>().HashPassword(usuario, usuario.Contrasena);
            _context.Usuarios.Add(usuario);
            if (await GuardarAsync()) return RedirectToAction(nameof(Index));
        }
        LimpiarContrasena(usuario);
        return View(usuario);
    }

    public async Task<IActionResult> Edit(long? id)
    {
        var usuario = await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (usuario == null) return NotFound();
        usuario.Contrasena = string.Empty;
        return View(usuario);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, [Bind("Id,Nombre,Correo,Contrasena,Ciudad,Telefono,Rol,NombreTienda,DescripcionTienda")] Usuario usuario)
    {
        if (id != usuario.Id) return NotFound();
        var existente = await _context.Usuarios.FindAsync(id);
        if (existente == null) return NotFound();
        await ValidarAsync(usuario, false);
        if (existente.Rol == "vendedor" && usuario.Rol != "vendedor" && await _context.Productos.AnyAsync(p => p.UsuarioId == id))
            ModelState.AddModelError(nameof(Usuario.Rol), "Este usuario tiene productos asociados y debe conservar el rol de vendedor.");
        if (ModelState.IsValid)
        {
            existente.Nombre = usuario.Nombre;
            existente.Correo = usuario.Correo;
            existente.Ciudad = usuario.Ciudad;
            existente.Telefono = usuario.Telefono;
            existente.Rol = usuario.Rol;
            existente.NombreTienda = usuario.NombreTienda;
            existente.DescripcionTienda = usuario.DescripcionTienda;
            // Una contraseña vacía conserva la actual; nunca se envía al formulario.
            if (!string.IsNullOrEmpty(usuario.Contrasena))
                existente.Contrasena = new PasswordHasher<Usuario>().HashPassword(existente, usuario.Contrasena);
            if (await GuardarAsync()) return RedirectToAction(nameof(Index));
        }
        LimpiarContrasena(usuario);
        return View(usuario);
    }

    public async Task<IActionResult> Delete(long? id)
    {
        var usuario = await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        return usuario == null ? NotFound() : View(usuario);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(long id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null) return NotFound();
        if (await _context.Productos.AnyAsync(p => p.UsuarioId == id) ||
            await _context.Pedidos.AnyAsync(p => p.UsuarioId == id) ||
            await _context.Carritos.AnyAsync(c => c.UsuarioId == id))
        {
            ModelState.AddModelError(string.Empty, "No se puede eliminar este usuario porque tiene productos, pedidos o un carrito asociado.");
            return View("Delete", usuario);
        }
        _context.Usuarios.Remove(usuario);
        try
        {
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateConcurrencyException)
        {
            return NotFound();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            ModelState.AddModelError(string.Empty, "No se puede eliminar el usuario porque tiene registros relacionados.");
            return View("Delete", usuario);
        }
    }

    private async Task ValidarAsync(Usuario usuario, bool crear)
    {
        usuario.Nombre = usuario.Nombre?.Trim() ?? string.Empty;
        usuario.Correo = usuario.Correo?.Trim().ToLowerInvariant() ?? string.Empty;
        usuario.Ciudad = CiudadesBolivia.Normalizar(usuario.Ciudad);
        usuario.Telefono = string.IsNullOrWhiteSpace(usuario.Telefono) ? null : usuario.Telefono.Trim();
        usuario.NombreTienda = usuario.NombreTienda?.Trim();
        usuario.DescripcionTienda = usuario.DescripcionTienda?.Trim();
        var errores = new List<ValidationResult>();
        Validator.TryValidateObject(usuario, new ValidationContext(usuario), errores, true);
        foreach (var error in errores)
            foreach (var campo in error.MemberNames)
                if (!ModelState.TryGetValue(campo, out var entrada) || entrada.Errors.Count == 0)
                    ModelState.AddModelError(campo, error.ErrorMessage!);
        if (crear && string.IsNullOrWhiteSpace(usuario.Contrasena))
            ModelState.AddModelError(nameof(Usuario.Contrasena), "Ingrese una contraseña de al menos 8 caracteres.");
        else if (!string.IsNullOrEmpty(usuario.Contrasena) &&
            (string.IsNullOrWhiteSpace(usuario.Contrasena) || usuario.Contrasena.Length < 8))
            ModelState.AddModelError(nameof(Usuario.Contrasena), "La contraseña debe tener al menos 8 caracteres y no estar formada solo por espacios.");
        if (usuario.Rol == "vendedor" && string.IsNullOrWhiteSpace(usuario.NombreTienda))
            ModelState.AddModelError(nameof(Usuario.NombreTienda), "Ingrese el nombre de la tienda del vendedor.");
        if (await _context.Usuarios.AnyAsync(u => u.Correo.ToLower() == usuario.Correo && u.Id != usuario.Id))
            ModelState.AddModelError(nameof(Usuario.Correo), "Ya existe un usuario con ese correo.");
    }

    private async Task<bool> GuardarAsync()
    {
        try { await _context.SaveChangesAsync(); return true; }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty, "El usuario ya no está disponible. Vuelva al listado.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            ModelState.AddModelError(nameof(Usuario.Correo), "Ya existe un usuario con ese correo.");
        }
        return false;
    }

    private void LimpiarContrasena(Usuario usuario)
    {
        usuario.Contrasena = string.Empty;
        if (ModelState.TryGetValue(nameof(Usuario.Contrasena), out var entrada))
        {
            entrada.RawValue = null;
            entrada.AttemptedValue = null;
        }
    }
}
