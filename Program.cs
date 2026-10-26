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

builder.Services.AddScoped<AlmacenImagenes>();
var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MarketPlaceProContext>();
    await context.Database.MigrateAsync();
    if (app.Configuration.GetValue<bool>("DatosDemostracion:Habilitados"))
        await MarketPlaceSeeder.InicializarAsync(context);
}
if (args.Contains("--inicializar", StringComparer.OrdinalIgnoreCase)) return;

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
