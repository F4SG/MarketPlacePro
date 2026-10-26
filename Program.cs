using MarketPlacePro.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configurar cultura invariante para consistencia de campos decimales (precios y montos)
var cultura = new System.Globalization.CultureInfo("en-US");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = cultura;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = cultura;

builder.Services.AddControllersWithViews()
    .AddMvcOptions(options =>
    {
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(_ => "Seleccione o ingrese un valor.");
        options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((_, campo) => $"Ingrese un valor válido para {campo}.");
        options.ModelBindingMessageProvider.SetValueMustBeANumberAccessor(campo => $"Ingrese un número válido para {campo}.");
    });

builder.Services.AddDbContext<MarketPlaceProContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MarketPlacePro")));

var app = builder.Build();

var cantidadUsuarios = app.Configuration.GetValue<int>("DatosIniciales:CantidadUsuarios");
var cantidadProductos = app.Configuration.GetValue<int>("DatosIniciales:CantidadProductos");
if (cantidadUsuarios > 0)
{
    await using var scope = app.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<MarketPlaceProContext>();
    await context.Database.MigrateAsync();
    await MarketPlaceSeeder.InicializarAsync(context, cantidadUsuarios, cantidadProductos);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
