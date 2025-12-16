using SAE_Dynamics_RGF.Data;

var builder = WebApplication.CreateBuilder(args);

// ---------------------
// 🔧 Services
// ---------------------
builder.Services.AddRazorPages();

// ✅ Gestion des sessions
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // session valable 30 min
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ✅ Pour que HttpContext soit accessible dans les Razor Pages (ex: _Layout.cshtml)
builder.Services.AddHttpContextAccessor();

// ✅ Service Dataverse
builder.Services.AddSingleton<DataverseService>();

var app = builder.Build();

// ---------------------
// 🌐 Test de connexion Dataverse au démarrage
// ---------------------
using (var scope = app.Services.CreateScope())
{
    var dataverseService = scope.ServiceProvider.GetRequiredService<DataverseService>();
    try
    {
        var products = dataverseService.GetProducts();
        Console.WriteLine($"✅ Produits récupérés : {products.Count}");
        foreach (var p in products)
        {
            Console.WriteLine($"Nom : {p.Name}, Catégorie : {p.Category}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("❌ Erreur lors de la récupération des produits : " + ex.Message);
    }
}

// ---------------------
// ⚙️ Pipeline de requêtes HTTP
// ---------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ⚠️ Ordre important : session AVANT Razor Pages
app.UseSession();

app.UseAuthorization();

// ✅ Routes Razor Pages
app.MapRazorPages();

app.Run();
