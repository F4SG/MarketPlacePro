# MarketPlacePro

Aplicación web para gestionar usuarios, productos, carritos, pedidos y pagos de un marketplace. Desarrollada con ASP.NET Core MVC, Entity Framework Core y SQL Server.

## Integrantes

| Nombre | GitHub |
|---|---|
| Franco Andres Salas Gutierrez | [F4SG](https://github.com/F4SG) |
| Leonardo Ariel Albornos Millares | [LeonardoAlbornos-dev](https://github.com/LeonardoAlbornos-dev) |
| Roderick Cesar Bayes Urbano | [rodetias123-coder](https://github.com/rodetias123-coder) |
| Mateo Sebastian Morales Guzman | [MMXIV-debug](https://github.com/MMXIV-debug) |
| Liz Aneli Vargas Ortiz | [vargasortizlizaneli](https://github.com/vargasortizlizaneli) |

## Requisitos

- .NET 10 SDK.
- SQL Server, SQL Server Express o LocalDB.
- `dotnet-ef` versión 10 para aplicar las migraciones.

Si la herramienta de Entity Framework no está instalada:

```powershell
dotnet tool install --global dotnet-ef --version "10.*"
```

## Instalación y ejecución

### 1 Clonar el proyecto

```powershell
git clone https://github.com/F4SG/MarketPlacePro.git
cd MarketPlacePro
dotnet restore MarketPlacePro.csproj
```

### 2 Configurar la conexión

Editar `ConnectionStrings:MarketPlacePro` en `appsettings.json` con la instancia de SQL Server que se utilizará:

```json
"ConnectionStrings": {
  "MarketPlacePro": "Server=localhost;Database=MarketPlacePro;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

La configuración usa autenticación de Windows. Para SQL Server Express, usar `Server=.\\SQLEXPRESS`; para LocalDB, `Server=(localdb)\\mssqllocaldb` dentro del JSON.

### 3 Crear la base de datos

Con SQL Server iniciado, ejecutar:

```powershell
dotnet ef database update --project MarketPlacePro.csproj
```

Se creará la base `MarketPlacePro` mediante la migración `InicialMarketPlacePro`.

### 4 Iniciar la aplicación

```powershell
dotnet run --project MarketPlacePro.csproj --launch-profile http
```

Abrir [http://localhost:5196](http://localhost:5196). Para detener la aplicación, presionar `Ctrl+C`.

También está disponible el perfil `https` en el puerto 7204. Para usarlo:

```powershell
dotnet dev-certs https --trust
dotnet run --project MarketPlacePro.csproj --launch-profile https
```

## Datos de prueba

Al iniciar, el generador con Bogus completa hasta 30 usuarios, 6 categorías y 60 productos. Las cantidades se configuran en `DatosIniciales`, dentro de `appsettings.json`. Los registros existentes se conservan.

Para desactivar la generación automática:

```json
"DatosIniciales": {
  "CantidadUsuarios": 0,
  "CantidadProductos": 0
}
```

Los pedidos y pagos se registran desde la aplicación. El reporte de ventas mostrará resultados cuando existan pedidos con detalles en el período y estado seleccionados.

## Módulos

- **Usuarios:** registro, consulta, edición y eliminación, con validación de correo único y roles.
- **Categorías y productos:** administración del catálogo, precios, existencias y vendedores.
- **Carritos y sus detalles:** gestión de productos y cantidades, sin repetir un producto dentro del mismo carrito.
- **Pedidos y sus detalles:** registro de compras y cálculo de importes.
- **Pagos:** registro del método, monto, estado y referencia del pago de un pedido.

Cada módulo tiene acceso desde el menú y desde la página de inicio. Las listas y formularios muestran nombres de usuarios, categorías y productos en sus relaciones.

### Reglas de pedidos e importes

Desde Carritos, abrir el carrito del usuario y usar Agregar producto. Al confirmar, se crea un pedido pendiente con todos sus productos y los precios vigentes. El pedido, sus detalles y el vaciado del carrito se guardan en una sola transacción. El carrito queda activo y puede reutilizarse; confirmar de nuevo un carrito vacío no crea otro pedido. Los carritos vacíos o no activos y los productos no activos se rechazan.

También se puede registrar un pedido directamente seleccionando el comprador, el estado, el primer producto y su cantidad. El pedido y ese detalle se guardan juntos. Desde la pantalla del pedido se pueden agregar, editar o quitar productos, conservando al menos uno.

La pantalla del pedido muestra sus productos, total y pago. Registrar pago abre el formulario con el pedido y su importe seleccionados. Registrar o editar un pago vuelve al pedido. El estado del pedido se cambia de forma manual desde Editar pedido; registrar un pago no lo marca automáticamente como completado.

Todo pedido conserva al menos un detalle, incluso estando pendiente. No se permite eliminar ni trasladar su último detalle; sí se puede eliminar el pedido completo.

El subtotal de un detalle es su precio unitario por la cantidad. El subtotal y el total del pedido se calculan sumando sus detalles. Al editar una cantidad se conserva el precio registrado; al cambiar el producto se toma el precio del nuevo producto.

Un usuario puede no tener carrito y un producto puede no tener ventas. Cada carrito pertenece a un usuario, con un máximo de un carrito por usuario. Cada pedido admite como máximo un pago.

El stock se administra desde Productos y los pagos se registran manualmente. No se incluyen descuentos, impuestos ni pasarela de cobro.

## Reportes

### Ventas por período

Permite consultar los productos vendidos y los montos de los pedidos de un período. Sirve para dar seguimiento a las ventas e identificar los productos con mayor cantidad vendida.

- **Filtros:** fecha inicial, fecha final y estado del pedido. Por defecto muestra los pedidos completados.
- **Información:** código y nombre del producto, categoría, cantidad vendida y monto total.
- **Resumen:** cantidad de pedidos, monto total y producto con más unidades vendidas.

Las fechas representan días completos en horario de Bolivia, UTC-4. La consulta obtiene los datos de pedidos y detalles almacenados en la base y distingue los productos por su identificador, aunque tengan el mismo nombre. Si se elige otro estado, los resultados corresponden a pedidos de ese estado.

### Productos con bajo stock

Permite identificar productos que requieren revisión o reposición de existencias.

- **Filtros:** umbral de stock, inicialmente 5, y categoría opcional.
- **Información:** producto, categoría, vendedor, stock y precio.
- **Resumen:** cantidad de productos encontrados y valor del inventario disponible, calculado como precio por stock.

La consulta muestra productos activos cuyo stock sea menor o igual al umbral, ordenados desde la menor cantidad disponible. Los productos agotados, pausados o inactivos no se incluyen.
