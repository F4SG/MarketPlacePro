# Verificación de MarketPlacePro

Desde la carpeta del proyecto:

```powershell
dotnet run --project Tests/MarketPlacePro.Regression.csproj
dotnet run --project Tests/MarketPlacePro.Regression.csproj -- --web
```

La primera ejecución comprueba reglas de datos, precios históricos, totales transaccionales, participación mínima de pedidos, integridad referencial, validaciones personales, stock, confirmación y devolución sin duplicados, pagos del flujo anterior y población de demostración idempotente. Incluye dos conexiones SQLite que compiten por la última unidad y comprueba que solo una venta la consume.

La segunda usa TestServer con los controladores y vistas de la aplicación, una base SQLite en memoria y datos ficticios. Comprueba nueve pantallas, carrito a pedido, confirmación de cobro por el total calculado, repetición, devolución, foto multipart y antifalsificación. Guarda las respuestas HTML en una carpeta temporal hermana del repositorio para inspección; elimina la foto de prueba al terminar.

No se conecta a la base de SQL Server del usuario. El esquema se compara con las migraciones usando el proveedor SQL Server sin conexión. La concurrencia de SQL Server y la instalación real se deben verificar en la instancia de destino.
