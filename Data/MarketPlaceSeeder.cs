using Microsoft.EntityFrameworkCore;
using MarketPlacePro.Models;

namespace MarketPlacePro.Data;

public static class MarketPlaceSeeder
{
    public static async Task InicializarAsync(MarketPlaceProContext context, int cantidadUsuarios, int cantidadProductos)
    {
        if (cantidadUsuarios <= 0) return;

        // 1. Usuarios (si faltan)
        var usuariosActuales = await context.Usuarios.CountAsync();
        if (usuariosActuales < cantidadUsuarios)
        {
            var faker = new UsuarioFaker();
            context.Usuarios.AddRange(faker.Generate(cantidadUsuarios - usuariosActuales));
            await context.SaveChangesAsync();
        }

        // 2. Categorias (si faltan, fijo 6)
        var categoriasActuales = await context.Categorias.CountAsync();
        if (categoriasActuales < 6)
        {
            var faker = new CategoriaFaker();
            context.Categorias.AddRange(faker.Generate(6 - categoriasActuales));
            await context.SaveChangesAsync();
        }

        // 3. Productos (necesita vendedores y categorias ya insertados)
        var productosActuales = await context.Productos.CountAsync();
        if (productosActuales < cantidadProductos)
        {
            var vendedorIds = await context.Usuarios.Where(u => u.Rol == "vendedor")
                .Select(u => u.Id).ToListAsync();
            var categoriaIds = await context.Categorias.Select(c => c.Id).ToListAsync();

            if (vendedorIds.Count > 0 && categoriaIds.Count > 0)
            {
                var faker = new ProductoFaker(vendedorIds, categoriaIds);
                context.Productos.AddRange(faker.Generate(cantidadProductos - productosActuales));
                await context.SaveChangesAsync();
            }
        }
    }
}
