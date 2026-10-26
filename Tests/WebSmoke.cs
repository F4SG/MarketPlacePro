using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using MarketPlacePro.Controllers;
using MarketPlacePro.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class WebSmoke
{
    public static async Task RunAsync()
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(HomeController).Assembly.GetName().Name,
            ContentRootPath = root, WebRootPath = Path.Combine(root, "wwwroot"), EnvironmentName = "Development"
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(HomeController).Assembly)
            .AddMvcOptions(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        builder.Services.AddDbContext<MarketPlaceProContext>(options => options.UseSqlite(connection));
        builder.Services.AddScoped<AlmacenImagenes>();
        await using var app = builder.Build();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketPlaceProContext>();
            await db.Database.EnsureCreatedAsync(); await MarketPlaceSeeder.InicializarAsync(db);
        }
        app.UseStaticFiles(); app.UseRouting();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        using var client = app.GetTestClient();
        var cookies = new Dictionary<string, string>();
        async Task<HttpResponseMessage> Send(HttpRequestMessage request)
        {
            request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookies.Select(c => c.Key + "=" + c.Value)));
            var response = await client.SendAsync(request);
            if (response.Headers.TryGetValues("Set-Cookie", out var valores))
                foreach (var valor in valores)
                {
                    var par = valor.Split(';')[0].Split('=', 2); cookies[par[0]] = par[1];
                }
            return response;
        }
        async Task<string> Get(string path)
        {
            using var response = await Send(new HttpRequestMessage(HttpMethod.Get, path));
            var html = await response.Content.ReadAsStringAsync();
            if (response.StatusCode != HttpStatusCode.OK) throw new Exception(path + ": " + response.StatusCode + " " + html[..Math.Min(500, html.Length)]);
            return html;
        }
        string Token(string html)
        {
            var input = Regex.Matches(html, "<input[^>]+>").Select(m => m.Value)
                .Single(s => s.Contains("name=\"__RequestVerificationToken\""));
            return WebUtility.HtmlDecode(Regex.Match(input, "value=\"([^\"]+)\"").Groups[1].Value);
        }
        async Task<HttpResponseMessage> Post(string path, string html, params (string, string)[] campos)
        {
            var form = campos.Select(c => new KeyValuePair<string,string>(c.Item1,c.Item2)).ToList();
            form.Add(new("__RequestVerificationToken", Token(html)));
            return await Send(new HttpRequestMessage(HttpMethod.Post,path) { Content = new FormUrlEncodedContent(form) });
        }
        void Assert(bool condicion, string mensaje) { if (!condicion) throw new Exception(mensaje); }
        var salida = Path.GetFullPath(Path.Combine(root, "../http-preview")); Directory.CreateDirectory(salida);
        foreach (var (ruta,nombre) in new[] { ("/", "inicio"), ("/Productos", "productos"), ("/Carritos/Details/1", "carrito"),
            ("/Pedidos/Details/1", "venta"), ("/Pagos/Create?pedidoId=3", "cobro"), ("/Productos/Create", "foto"),
            ("/Categorias/Create", "categoria"), ("/Reportes/VentasPorPeriodo", "ventas"), ("/Reportes/ProductosBajoStock", "stock") })
        {
            var html = await Get(ruta);
            var archivos = new Uri(Path.Combine(root, "wwwroot") + Path.DirectorySeparatorChar).AbsoluteUri.TrimEnd('/');
            var vista = Regex.Replace(html, "(src|href)=\"/(css|js|lib|images)/", m => m.Groups[1].Value + "=\"" + archivos + "/" + m.Groups[2].Value + "/");
            await File.WriteAllTextAsync(Path.Combine(salida,nombre+".html"),vista);
        }
        var carrito = await Get("/Carritos/Details/1");
        using var creado = await Post("/Carritos/CrearPedido/1",carrito);
        Assert(creado.StatusCode == HttpStatusCode.Redirect, "No creó el pedido desde el carrito.");
        var destino = creado.Headers.Location!.ToString(); var id = destino.Split('/').Last();
        var cobro = await Get("/Pagos/Create?pedidoId="+id);
        using var pagado = await Post("/Pagos/Create",cobro,("PedidoId",id),("Metodo","qr"),("Referencia","HTTP-001"),("ConfirmarRecepcion","true"),("Total","0.01"),("Estado","pendiente"));
        Assert(pagado.StatusCode == HttpStatusCode.Redirect, "No confirmó el cobro: " + await pagado.Content.ReadAsStringAsync());
        var completado = await Get(destino);
        Assert(completado.Contains("Venta finalizada") && completado.Contains("290.00"), "El pedido no muestra la venta finalizada por su total real.");
        using var repetido = await Post("/Pagos/Create",cobro,("PedidoId",id),("Metodo","qr"),("Referencia","HTTP-001"),("ConfirmarRecepcion","true"));
        Assert(repetido.StatusCode == HttpStatusCode.Redirect,"La repetición no fue idempotente.");
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketPlaceProContext>();
            Assert((await db.Productos.FindAsync(3L))!.Stock == 6 && (await db.Productos.FindAsync(5L))!.Stock == 8, "Descuento repetido o incorrecto.");
            Assert(await db.Pagos.CountAsync(p => p.PedidoId == long.Parse(id)) == 1,"Duplicó el pago.");
        }
        using var devuelto = await Post("/Pedidos/Devolver/"+id,completado,("confirmar","true"));
        Assert(devuelto.StatusCode == HttpStatusCode.Redirect,"No devolvió la venta.");
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketPlaceProContext>();
            Assert((await db.Productos.FindAsync(3L))!.Stock == 7 && (await db.Productos.FindAsync(5L))!.Stock == 10,"Devolución no repuso stock.");
        }
        var fotoHtml = await Get("/Productos/Create");
        using var multipart = new MultipartFormDataContent();
        foreach (var c in new[] { ("__RequestVerificationToken",Token(fotoHtml)),("Nombre","Producto foto HTTP"),("Descripcion","Foto de prueba"),
            ("Precio","25.50"),("Stock","0"),("Estado","activo"),("UsuarioId","2"),("CategoriaId","1") })
            multipart.Add(new StringContent(c.Item2),c.Item1);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j0ioAAAAASUVORK5CYII=");
        using var file = new ByteArrayContent(png); file.Headers.ContentType = new MediaTypeHeaderValue("image/png"); multipart.Add(file,"foto","prueba.png");
        using var subido = await Send(new HttpRequestMessage(HttpMethod.Post,"/Productos/Create") { Content = multipart });
        Assert(subido.StatusCode == HttpStatusCode.Redirect,"No guardó la foto del producto: " + await subido.Content.ReadAsStringAsync());
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketPlaceProContext>();
            var producto = await db.Productos.SingleAsync(p => p.Nombre == "Producto foto HTTP");
            Assert(producto.Stock == 0 && producto.Estado == "agotado" && producto.Imagen!.StartsWith("/uploads/"),"Foto o disponibilidad incoherente.");
            using var imagen = await Send(new HttpRequestMessage(HttpMethod.Get,producto.Imagen));
            Assert(imagen.IsSuccessStatusCode && imagen.Content.Headers.ContentType!.MediaType == "image/png","Foto no accesible.");
            scope.ServiceProvider.GetRequiredService<AlmacenImagenes>().EliminarNueva(producto.Imagen);
        }
        using var sinToken = await Send(new HttpRequestMessage(HttpMethod.Post,"/Pagos/Create") { Content = new FormUrlEncodedContent(new Dictionary<string,string>{{"PedidoId","3"}}) });
        Assert(sinToken.StatusCode == HttpStatusCode.BadRequest,"Falta protección antifalsificación.");
        await app.StopAsync();
        Console.WriteLine("OK HTTP: 9 pantallas, carrito a pedido, cobro completo, repetición, devolución, foto multipart y antifalsificación.");
    }
}
