IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023184933_InicialMarketPlacePro'
)
BEGIN
    CREATE TABLE [Categorias] (
        [Id] bigint NOT NULL IDENTITY,
        [Nombre] nvarchar(255) NOT NULL,
        [Descripcion] nvarchar(1000) NULL,
        [Imagen] nvarchar(255) NULL,
        CONSTRAINT [PK_Categorias] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023184933_InicialMarketPlacePro'
)
BEGIN
    CREATE TABLE [Usuarios] (
        [Id] bigint NOT NULL IDENTITY,
        [Nombre] nvarchar(255) NOT NULL,
        [Correo] nvarchar(255) NOT NULL,
        [Contrasena] nvarchar(255) NOT NULL,
        [Ciudad] nvarchar(255) NULL,
        [Telefono] nvarchar(50) NULL,
        [FechaRegistro] datetime2 NULL,
        [Rol] nvarchar(50) NOT NULL,
        [NombreTienda] nvarchar(255) NULL,
        [DescripcionTienda] nvarchar(1000) NULL,
        CONSTRAINT [PK_Usuarios] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Usuarios_Rol] CHECK ([Rol] IN ('comprador', 'vendedor', 'admin'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023184933_InicialMarketPlacePro'
)
BEGIN
    CREATE TABLE [Carritos] (
        [Id] bigint NOT NULL IDENTITY,
        [Estado] nvarchar(20) NOT NULL,
        [FechaCreacion] datetime2 NOT NULL,
        [FechaActualizacion] datetime2 NULL,
        [UsuarioId] bigint NOT NULL,
        CONSTRAINT [PK_Carritos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Carritos_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023184933_InicialMarketPlacePro'
)
BEGIN
    CREATE TABLE [Pedidos] (
        [Id] bigint NOT NULL IDENTITY,
        [NumeroPedido] nvarchar(50) NOT NULL,
        [Subtotal] decimal(10,2) NOT NULL,
        [Total] decimal(10,2) NOT NULL,
        [Estado] nvarchar(20) NOT NULL,
        [FechaPedido] datetime2 NULL,
        [UsuarioId] bigint NOT NULL,
        CONSTRAINT [PK_Pedidos] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Pedidos_Estado] CHECK ([Estado] IN ('pendiente', 'completada', 'cancelada', 'en_proceso')),
        CONSTRAINT [FK_Pedidos_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023184933_InicialMarketPlacePro'
)
BEGIN
    CREATE TABLE [Productos] (
        [Id] bigint NOT NULL IDENTITY,
        [Nombre] nvarchar(255) NOT NULL,
        [Descripcion] nvarchar(2000) NULL,
        [Precio] decimal(10,2) NOT NULL,
        [Stock] int NOT NULL,
        [Imagen] nvarchar(255) NULL,
        [Estado] nvarchar(20) NOT NULL,
        [FechaPublicacion] datetime2 NULL,
        [UsuarioId] bigint NOT NULL,
        [CategoriaId] bigint NOT NULL,
        CONSTRAINT [PK_Productos] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Productos_Estado] CHECK ([Estado] IN ('activo', 'agotado', 'inactivo', 'pausado')),
        CONSTRAINT [FK_Productos_Categorias_CategoriaId] FOREIGN KEY ([CategoriaId]) REFERENCES [Categorias] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Productos_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023184933_InicialMarketPlacePro'
)
BEGIN
    CREATE TABLE [Pagos] (
        [Id] bigint NOT NULL IDENTITY,
        [Metodo] nvarchar(100) NOT NULL,
        [Monto] decimal(10,2) NOT NULL,
        [Estado] nvarchar(20) NOT NULL,
        [Referencia] nvarchar(255) NULL,
        [FechaPago] datetime2 NOT NULL,
        [PedidoId] bigint NOT NULL,
        CONSTRAINT [PK_Pagos] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Pagos_Estado] CHECK ([Estado] IN ('pendiente', 'completado', 'rechazado', 'reembolsado')),
        CONSTRAINT [FK_Pagos_Pedidos_PedidoId] FOREIGN KEY ([PedidoId]) REFERENCES [Pedidos] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023184933_InicialMarketPlacePro'
)
BEGIN
    CREATE TABLE [DetallesCarrito] (
        [Id] bigint NOT NULL IDENTITY,
        [Cantidad] int NOT NULL,
        [FechaAgregado] datetime2 NULL,
        [CarritoId] bigint NOT NULL,
        [ProductoId] bigint NOT NULL,
        CONSTRAINT [PK_DetallesCarrito] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DetallesCarrito_Carritos_CarritoId] FOREIGN KEY ([CarritoId]) REFERENCES [Carritos] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DetallesCarrito_Productos_ProductoId] FOREIGN KEY ([ProductoId]) REFERENCES [Productos] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023184933_InicialMarketPlacePro'
)
BEGIN
    CREATE TABLE [DetallesPedido] (
        [Id] bigint NOT NULL IDENTITY,
        [NombreProducto] nvarchar(255) NOT NULL,
        [PrecioUnitario] decimal(10,2) NOT NULL,
        [Cantidad] int NOT NULL,
        [Subtotal] decimal(10,2) NOT NULL,
        [PedidoId] bigint NOT NULL,
        [ProductoId] bigint NOT NULL,
        CONSTRAINT [PK_DetallesPedido] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DetallesPedido_Pedidos_PedidoId] FOREIGN KEY ([PedidoId]) REFERENCES [Pedidos] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DetallesPedido_Productos_ProductoId] FOREIGN KEY ([ProductoId]) REFERENCES [Productos] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023184933_InicialMarketPlacePro'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Carritos_UsuarioId] ON [Carritos] ([UsuarioId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023184933_InicialMarketPlacePro'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DetallesCarrito_CarritoId_ProductoId] ON [DetallesCarrito] ([CarritoId], [ProductoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023184933_InicialMarketPlacePro'
)
BEGIN
    CREATE INDEX [IX_DetallesCarrito_ProductoId] ON [DetallesCarrito] ([ProductoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023184933_InicialMarketPlacePro'
)
BEGIN
    CREATE INDEX [IX_DetallesPedido_PedidoId] ON [DetallesPedido] ([PedidoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023184933_InicialMarketPlacePro'
)
BEGIN
    CREATE INDEX [IX_DetallesPedido_ProductoId] ON [DetallesPedido] ([ProductoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023184933_InicialMarketPlacePro'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Pagos_PedidoId] ON [Pagos] ([PedidoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023184933_InicialMarketPlacePro'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Pedidos_NumeroPedido] ON [Pedidos] ([NumeroPedido]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023184933_InicialMarketPlacePro'
)
BEGIN
    CREATE INDEX [IX_Pedidos_UsuarioId] ON [Pedidos] ([UsuarioId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023184933_InicialMarketPlacePro'
)
BEGIN
    CREATE INDEX [IX_Productos_CategoriaId] ON [Productos] ([CategoriaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023184933_InicialMarketPlacePro'
)
BEGIN
    CREATE INDEX [IX_Productos_UsuarioId] ON [Productos] ([UsuarioId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023184933_InicialMarketPlacePro'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Usuarios_Correo] ON [Usuarios] ([Correo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261023184933_InicialMarketPlacePro'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261023184933_InicialMarketPlacePro', N'10.0.14');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026013347_ControlDescuentoStock'
)
BEGIN
    ALTER TABLE [Pagos] ADD [StockDescontado] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261026013347_ControlDescuentoStock'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261026013347_ControlDescuentoStock', N'10.0.14');
END;

COMMIT;
GO
