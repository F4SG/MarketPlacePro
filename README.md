# MarketPlacePro

Sistema de Marketplace desarrollado en ASP.NET Core MVC (.NET 10) y Entity Framework Core.

## Requisitos
- .NET 10 SDK
- SQL Server (LocalDB o localhost)

## Configuración inicial
1. Clonar el repositorio:
   ```bash
   git clone https://github.com/F4SG/MarketPlacePro.git
   ```
2. Configurar la cadena de conexión en `appsettings.json` según tu instancia local de SQL Server.
3. Aplicar migraciones:
   ```bash
   dotnet ef database update
   ```
