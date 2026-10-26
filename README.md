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
- SQL Server, SQL Server Express o LocalDB iniciado.
- Herramienta `dotnet-ef` 10 para ejecutar migraciones desde la terminal.

```powershell
dotnet tool install --global dotnet-ef --version "10.*"
```

## Preparar y ejecutar

Clonar el repositorio o extraer el ZIP y abrir la carpeta que contiene `MarketPlacePro.csproj`:

```powershell
git clone https://github.com/F4SG/MarketPlacePro.git
cd MarketPlacePro
dotnet restore MarketPlacePro.csproj
```

Configurar `ConnectionStrings:MarketPlacePro` en `appsettings.json`. La conexión incluida usa autenticación de Windows:

```json
"ConnectionStrings": {
  "MarketPlacePro": "Server=localhost;Database=MarketPlacePro;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

Para SQL Server Express usar `Server=.\\SQLEXPRESS` dentro del JSON. Para LocalDB usar `Server=(localdb)\\mssqllocaldb`. También se puede suministrar la conexión mediante la variable de entorno `ConnectionStrings__MarketPlacePro`, sin guardar credenciales en el repositorio.

Crear o actualizar el esquema y cargar los datos de demostración:

```powershell
dotnet ef database update --project MarketPlacePro.csproj
dotnet run --project MarketPlacePro.csproj --no-launch-profile -- --inicializar
```

El primer comando aplica las migraciones; el segundo carga la demostración habilitada y termina sin abrir el servidor. Al iniciar normalmente, la aplicación también aplica las migraciones pendientes e inicializa la demostración si corresponde.

```powershell
dotnet run --project MarketPlacePro.csproj --launch-profile http
```

Abrir [http://localhost:5196](http://localhost:5196). Detener con `Ctrl+C`. El perfil `https` está disponible en el puerto 7204.

## Datos de demostración

En una base vacía se agregan 6 usuarios, 4 categorías, 12 productos, 3 carritos, 5 pedidos, 8 detalles de pedido, 3 detalles de carrito y 3 pagos. Las imágenes son archivos locales incluidos en el proyecto. Todos los datos son ficticios.

La carga se realiza en una sola transacción. Si ya existe algún usuario del conjunto de demostración, no se vuelve a insertar ni se sobrescriben los cambios hechos durante el uso. En una base anterior, sus registros se conservan y se agrega el conjunto de muestra si aún no existe. Para repetir la demostración desde cero, usar otra base de datos de prueba en la conexión.

Los campos opcionales se completan cuando corresponde: los vendedores tienen tienda, los compradores pueden no tenerla y los pedidos sin cobrar pueden no tener pago. Las contraseñas de muestra se almacenan como hash. Los roles clasifican a los usuarios; este proyecto no incluye inicio de sesión ni autorización por rol.

Para deshabilitar la carga, configurar:

```json
"DatosDemostracion": { "Habilitados": false }
```

Casos preparados:

- Carrito de María Pérez: teclado compacto y dos tazas, total Bs 290.00, listo para demostrar una venta.
- Dos ventas completadas con sus pagos confirmados, total Bs 445.00, visibles en el reporte inicial.
- Dos pedidos pendientes, uno con un registro de pago QR aún por verificar.
- Un pedido cancelado sin cobro.
- Productos con stock normal, bajo y agotado; un producto pausado para comprobar los filtros.

## Flujo de venta

1. Registrar comprador, vendedor y categoría cuando sean necesarios.
2. Publicar un producto con precio, stock e imagen. Abrir el carrito del comprador y agregar productos.
3. Confirmar el carrito: se crea un pedido pendiente con sus precios históricos y se vacía el carrito para reutilizarlo. Este paso no reserva ni descuenta stock.
4. Abrir el pedido y pulsar **Confirmar cobro y finalizar venta**. Seleccionar efectivo, transferencia o QR. Transferencia y QR requieren referencia.
5. Confirmar que se recibió o verificó el dinero. El servidor obtiene el total del pedido, vuelve a comprobar existencias y, en una sola transacción, completa el pago, completa el pedido y descuenta stock.
6. Revisar la confirmación de venta y los reportes.

El sistema registra cobros de forma manual: no procesa dinero ni verifica cuentas bancarias. Un pedido completado representa una venta cobrada dentro del alcance del proyecto; no representa un envío entregado.

Cada pedido admite como máximo un pago. Se cobra su total, sin abonos parciales. Un pago pendiente o rechazado se confirma actualizando el mismo registro. Las fechas de cobro se asignan en UTC y se muestran en horario de Bolivia. No se escriben estados ni montos libremente.

El pedido se puede crear también directamente con su primer producto. Siempre conserva al menos un detalle. Los subtotales se calculan como precio registrado por cantidad; el total es la suma de detalles, sin impuestos ni descuentos. Cambiar solo la cantidad conserva el precio histórico; cambiar el producto toma su precio actual.

## Stock y operaciones posteriores

- Las cantidades son enteras positivas y el stock no puede ser negativo.
- La disponibilidad a la venta se muestra como agotada cuando el stock es cero. Pausado e inactivo son decisiones independientes del inventario.
- El cobro usa actualizaciones condicionales dentro de una transacción. Si falta stock o falla el guardado, no se confirma el pago ni se aplican descuentos parciales.
- Repetir un cobro confirmado no genera otro pago ni vuelve a descontar unidades.
- Los pedidos cobrados, devueltos o cancelados no permiten editar sus productos, comprador o importes. Los pagos cobrados y reembolsados se conservan como historial.
- Cancelar un pedido pendiente no modifica stock, porque no estaba reservado.
- La devolución es completa: se confirma la devolución del dinero y de todos los productos, el pago pasa a reembolsado, el pedido a cancelado y se reponen una sola vez las unidades que tenían descuento automático.
- Los ajustes manuales de inventario comprueban que el stock no haya cambiado desde que se abrió el formulario.

El umbral de bajo stock es general y configurable en `Inventario:UmbralBajoStock`, inicialmente 5. No es un stock mínimo diferente para cada producto y no se utiliza como límite de venta.

## Imágenes y validaciones

Productos y categorías aceptan archivos PNG, JPG y WebP de hasta 3 MB. Se comprueban tamaño, extensión y firma del formato. La vista previa permite revisar la foto antes de guardar. Los archivos se guardan con nombres aleatorios en `wwwroot/uploads`; la base almacena su ruta. Al editar sin adjunto se conserva la imagen anterior. Las fotos adjuntadas durante el uso no se incluyen en Git: para trasladar una instalación con fotos propias hay que copiar esa carpeta además de la base.

Los nombres personales admiten letras latinas, tildes, espacios, guiones y apóstrofes, sin números. Los nombres de productos y tiendas pueden contener números. El selector de ciudad incluye las nueve capitales departamentales y El Alto; no pretende incluir todos los municipios de Bolivia. El teléfono admite ocho dígitos y prefijo +591 opcional. Se validan correos únicos, importes con dos decimales, relaciones existentes y datos de tienda para vendedores. Las validaciones se aplican también en el servidor.

## Reportes

**Ventas por período:** filtra fechas y estado. De forma predeterminada muestra únicamente pedidos completados con pago confirmado y utiliza la fecha del cobro. Resume productos, unidades, montos, cantidad de pedidos y producto con más unidades. Los límites incluyen días completos de Bolivia (UTC-4). Los pedidos reembolsados dejan de formar parte de las ventas completadas. Al consultar otros estados se usa la fecha del pedido y se muestran importes de pedidos, no dinero cobrado.

**Productos con bajo stock:** filtra umbral y categoría. Incluye productos a la venta o agotados con stock menor o igual al umbral; excluye pausados e inactivos. Muestra vendedor, precio, unidades y valor del inventario disponible, ordenado desde las menores existencias.

Ambos reportes consultan la base de datos y cuentan con filtros y resúmenes propios.

## DER y migraciones

Se mantienen las ocho entidades y las nueve relaciones. Respecto del DER original:

- Usuario puede tener cero o un carrito.
- Producto puede aparecer en cero o muchos detalles de pedido.
- Pedido conserva uno o más detalles en todos sus estados.
- Se agrega `stock_descontado` a Pago, de tipo booleano: indica que el sistema descontó las existencias y todavía no las repuso. El modelo final tiene 47 atributos conceptuales.

La migración `ControlDescuentoStock` agrega `Pagos.StockDescontado`, obligatorio y con valor inicial falso. Los cobros nuevos lo activan; las devoluciones lo desactivan. Los pagos del flujo anterior conservan falso: no se descuentan existencias retroactivamente ni se repone stock que el sistema no descontó.

Un pedido antiguo marcado como completado sin cobro confirmado puede reabrirse explícitamente desde su pantalla para revisar y confirmar el dinero. Los pedidos antiguos vacíos deben completarse con un detalle o eliminarse como pedidos completos. No se inventan productos ni se reparan registros masivamente.

En `Documentacion` están el DER original, el actualizado, la comparación, una guía de demostración y el script idempotente del esquema. El script SQL se ejecuta sobre una base de datos ya creada; la carga de muestra se hace desde la aplicación con `--inicializar`.
