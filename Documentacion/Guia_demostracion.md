# Demostración de una venta en MarketPlacePro

## Preparación

Configurar una base SQL Server de prueba y ejecutar desde el proyecto:

```powershell
dotnet ef database update
dotnet run --no-launch-profile -- --inicializar
dotnet run --launch-profile http
```

Abrir http://localhost:5196. En una base vacía se cargan seis usuarios, cuatro categorías, doce productos, tres carritos, cinco pedidos y tres pagos. La carga no se repite al reiniciar. Para repetir desde cero, usar otra base de prueba; no es necesario borrar datos existentes.

## Recorrido principal

1. Abrir Productos. Mostrar imágenes, precios y disponibilidad. El teclado tiene 7 unidades y la taza 10 antes de iniciar el recorrido.
2. Abrir Carritos y el carrito de María Pérez. Contiene un teclado de Bs 220.00 y dos tazas de Bs 35.00: total Bs 290.00.
3. Confirmar el carrito. Se crea un pedido pendiente, se conserva el precio de sus detalles y se vacía el carrito. Aún no hay cobro ni descuento de stock.
4. Desde el pedido, pulsar Confirmar cobro y finalizar venta. Elegir efectivo y marcar que se recibió el dinero. Para QR o transferencia, introducir una referencia ficticia de demostración.
5. Confirmar. La pantalla muestra venta finalizada, pago completado y pedido completado. El teclado queda con 6 unidades y la taza con 8.
6. Abrir Ventas por período. La nueva venta aparece por la fecha del cobro. Las dos ventas iniciales suman Bs 445.00; después de esta operación suman Bs 735.00 si no se han hecho otros cambios.
7. Abrir Productos con bajo stock. Mostrar el producto agotado, el balón con una unidad y los demás productos con stock menor o igual a 5. La guía de cocina está pausada y no aparece.

## Casos adicionales

- Abrir el carrito de Ana Gutiérrez y cobrar su balón para demostrar cómo pasa de una unidad a agotado.
- Abrir DEMO-PENDIENTE-002: su pago QR sigue pendiente porque todavía no se verificó el dinero. Confirmarlo actualiza el mismo pago, sin crear un segundo registro.
- Intentar una cantidad superior al stock en un carrito. El formulario muestra el error.
- Adjuntar una foto PNG, JPG o WebP de hasta 3 MB a un producto o categoría. Mostrar la vista previa y el resultado guardado.
- Cancelar un pedido sin cobrar. Su stock no cambia.
- En una venta cobrada, registrar devolución completa después de confirmar la devolución ficticia del dinero y los productos. El pago queda reembolsado y se repone stock una sola vez.

## Explicación para la defensa

El carrito organiza una intención de compra. El pedido guarda productos y precios históricos. El pago confirma que se recibió dinero. La transacción del cobro une el cambio de estados con el descuento de inventario y revierte todo si falla alguna parte.

Cada usuario tiene como máximo un carrito; cada pedido tiene al menos un detalle y como máximo un pago. El atributo stock_descontado de Pago permite saber si corresponde reponer inventario y protege los registros del flujo anterior. El umbral de bajo stock es una configuración general del reporte.

La aplicación registra cobros manualmente; no se conecta a una pasarela bancaria ni gestiona envíos. Los roles clasifican usuarios y no implementan autenticación. Las fotografías se guardan como archivos y la base conserva su ruta.
