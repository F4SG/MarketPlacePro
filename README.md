# MarketPlacePro — README de instalación

## Descripción

MarketPlacePro es un sistema de marketplace desarrollado en **ASP.NET Core MVC (.NET 10)** y **Entity Framework Core** con SQL Server. Incluye gestión de usuarios (compradores y vendedores), catálogo de productos por categorías, carrito de compras, pedidos, pagos y reportes.

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server (LocalDB o instancia local)
- `dotnet-ef` (Entity Framework CLI)

Para instalar la herramienta EF si no la tienes:

```bash
dotnet tool install --global dotnet-ef
```

## Configuración inicial

**1. Clonar el repositorio**

```bash
git clone https://github.com/F4SG/MarketPlacePro.git
cd MarketPlacePro
```

**2. Configurar la cadena de conexión**

Edita `appsettings.json` y ajusta el valor de `ConnectionStrings:MarketPlacePro` según tu instancia de SQL Server:

```json
"ConnectionStrings": {
  "MarketPlacePro": "Server=localhost;Database=MarketPlacePro;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

Si usas **LocalDB**, cambia `Server=localhost` por `Server=(localdb)\\mssqllocaldb`.

**3. Aplicar migraciones y crear la base de datos**

```bash
dotnet ef database update
```

Esto crea la base de datos y aplica la migración inicial `InicialMarketPlacePro`.

## Datos iniciales (seeder)

Al arrancar la aplicación, si `DatosIniciales:CantidadUsuarios` es mayor que `0` en `appsettings.json`, el sistema siembra datos de prueba automáticamente usando [Bogus](https://github.com/bchavez/Bogus):

| Entidad | Cantidad por defecto |
| --- | --- |
| Usuarios | 30 |
| Categorías | 6 |
| Productos | 60 |

Para deshabilitar el seeder, establece `CantidadUsuarios` en `0`:

```json
"DatosIniciales": {
  "CantidadUsuarios": 0,
  "CantidadProductos": 0
}
```

El seeder es acumulativo: solo inserta los registros que falten para alcanzar la cantidad configurada.

## Ejecutar el proyecto

```bash
dotnet run
```

La aplicación queda disponible en:

- HTTP: `http://localhost:5196`
- HTTPS: `https://localhost:7204`

Desde Visual Studio, puedes usar el perfil `https` definido en `Properties/launchSettings.json` para lanzar con el navegador abierto automáticamente.
