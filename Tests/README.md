# Pruebas de regresión

Ejecutar desde la carpeta del proyecto:

```powershell
dotnet run --project Tests/MarketPlacePro.Regression.csproj
```

Las pruebas usan bases SQLite temporales en memoria. No se conectan a la base de SQL Server ni modifican datos del usuario. La comprobación del esquema usa el proveedor SQL Server y la migración existente sin abrir una conexión.

Se comprueban los límites de fechas del reporte en horario de Bolivia, productos con el mismo nombre, validaciones, duplicados al editar, eliminaciones relacionadas, conservación del precio histórico y actualización transaccional de los totales al crear, editar, mover y eliminar detalles. Incluye rechazo de importes excesivos y rollback ante un error de integridad.

También se comprueba que un pedido se cree junto con su primer detalle, que no se pueda vaciar al eliminar o mover el último detalle y que un pedido vacío anterior no pueda completarse. La participación mínima se aplica a todos los estados, conforme al DER actualizado.

Estas pruebas no reemplazan una prueba de funcionamiento con SQL Server ni una prueba de concurrencia de múltiples usuarios.
