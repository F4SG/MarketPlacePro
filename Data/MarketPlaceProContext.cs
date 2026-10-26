using Microsoft.EntityFrameworkCore;
using MarketPlacePro.Models;

namespace MarketPlacePro.Data;

public class MarketPlaceProContext : DbContext
{
    public MarketPlaceProContext(DbContextOptions<MarketPlaceProContext> options)
        : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Carrito> Carritos => Set<Carrito>();
    public DbSet<DetalleCarrito> DetallesCarrito => Set<DetalleCarrito>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<DetallePedido> DetallesPedido => Set<DetallePedido>();
    public DbSet<Pago> Pagos => Set<Pago>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("Usuarios");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nombre).HasMaxLength(255).IsRequired();
            e.Property(x => x.Correo).HasMaxLength(255).IsRequired();
            e.Property(x => x.Contrasena).HasMaxLength(255).IsRequired();
            e.Property(x => x.Ciudad).HasMaxLength(255);
            e.Property(x => x.Telefono).HasMaxLength(50);
            e.Property(x => x.Rol).HasMaxLength(50).IsRequired();
            e.Property(x => x.NombreTienda).HasMaxLength(255);
            e.Property(x => x.DescripcionTienda).HasMaxLength(1000);

            e.HasIndex(x => x.Correo).IsUnique();

            e.ToTable(t => t.HasCheckConstraint("CK_Usuarios_Rol", "[Rol] IN ('comprador', 'vendedor', 'admin')"));
        });

        modelBuilder.Entity<Categoria>(e =>
        {
            e.ToTable("Categorias");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nombre).HasMaxLength(255).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(1000);
            e.Property(x => x.Imagen).HasMaxLength(255);
        });

        modelBuilder.Entity<Producto>(e =>
        {
            e.ToTable("Productos");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nombre).HasMaxLength(255).IsRequired();
            e.Property(x => x.Descripcion).HasMaxLength(2000);
            e.Property(x => x.Precio).HasPrecision(10, 2);
            e.Property(x => x.Imagen).HasMaxLength(255);
            e.Property(x => x.Estado).HasMaxLength(20).IsRequired();

            e.HasOne(p => p.Vendedor)
             .WithMany(u => u.Productos)
             .HasForeignKey(p => p.UsuarioId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(p => p.Categoria)
             .WithMany(c => c.Productos)
             .HasForeignKey(p => p.CategoriaId)
             .OnDelete(DeleteBehavior.Restrict);

            e.ToTable(t => t.HasCheckConstraint("CK_Productos_Estado", "[Estado] IN ('activo', 'agotado', 'inactivo', 'pausado')"));
        });

        modelBuilder.Entity<Carrito>(e =>
        {
            e.ToTable("Carritos");
            e.HasKey(x => x.Id);
            e.Property(x => x.Estado).HasMaxLength(20).IsRequired();

            e.HasOne(c => c.Usuario)
             .WithOne(u => u.Carrito)
             .HasForeignKey<Carrito>(c => c.UsuarioId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(c => c.UsuarioId).IsUnique();
        });

        modelBuilder.Entity<DetalleCarrito>(e =>
        {
            e.ToTable("DetallesCarrito");
            e.HasKey(x => x.Id);

            e.HasOne(d => d.Carrito)
             .WithMany(c => c.Detalles)
             .HasForeignKey(d => d.CarritoId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(d => d.Producto)
             .WithMany(p => p.DetallesCarrito)
             .HasForeignKey(d => d.ProductoId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(d => new { d.CarritoId, d.ProductoId }).IsUnique();
        });

        modelBuilder.Entity<Pedido>(e =>
        {
            e.ToTable("Pedidos");
            e.HasKey(x => x.Id);
            e.Property(x => x.NumeroPedido).HasMaxLength(50).IsRequired();
            e.Property(x => x.Subtotal).HasPrecision(10, 2);
            e.Property(x => x.Total).HasPrecision(10, 2);
            e.Property(x => x.Estado).HasMaxLength(20).IsRequired();

            e.HasOne(p => p.Comprador)
             .WithMany(u => u.Pedidos)
             .HasForeignKey(p => p.UsuarioId)
             .IsRequired()
             .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(p => p.NumeroPedido).IsUnique();

            e.ToTable(t => t.HasCheckConstraint("CK_Pedidos_Estado", "[Estado] IN ('pendiente', 'completada', 'cancelada', 'en_proceso')"));
        });

        modelBuilder.Entity<DetallePedido>(e =>
        {
            e.ToTable("DetallesPedido");
            e.HasKey(x => x.Id);
            e.Property(x => x.NombreProducto).HasMaxLength(255).IsRequired();
            e.Property(x => x.PrecioUnitario).HasPrecision(10, 2);
            e.Property(x => x.Subtotal).HasPrecision(10, 2);

            e.HasOne(d => d.Pedido)
             .WithMany(p => p.Detalles)
             .HasForeignKey(d => d.PedidoId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(d => d.Producto)
             .WithMany(p => p.DetallesPedido)
             .HasForeignKey(d => d.ProductoId)
             .IsRequired()
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Pago>(e =>
        {
            e.ToTable("Pagos");
            e.HasKey(x => x.Id);
            e.Property(x => x.Metodo).HasMaxLength(100).IsRequired();
            e.Property(x => x.Monto).HasPrecision(10, 2);
            e.Property(x => x.StockDescontado).HasDefaultValue(false);
            e.Property(x => x.Estado).HasMaxLength(20).IsRequired();
            e.Property(x => x.Referencia).HasMaxLength(255);

            e.HasOne(p => p.Pedido)
             .WithOne(pd => pd.Pago)
             .HasForeignKey<Pago>(p => p.PedidoId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(p => p.PedidoId).IsUnique();

            e.ToTable(t => t.HasCheckConstraint("CK_Pagos_Estado", "[Estado] IN ('pendiente', 'completado', 'rechazado', 'reembolsado')"));
        });
    }
}
