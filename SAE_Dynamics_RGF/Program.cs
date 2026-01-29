using SAE_Dynamics_RGF.Data;

var builder = WebApplication.CreateBuilder(args);

// ---------------------
// 🔧 Services
// ---------------------
builder.Services.AddRazorPages();
builder.Services.AddControllers(); // Ajouter le support des contrôleurs API

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
// 🌐 Test de connexion Dataverse au démarrage (version asynchrone)
// ---------------------
using (var scope = app.Services.CreateScope())
{
    var dataverseService = scope.ServiceProvider.GetRequiredService<DataverseService>();
    
    // Lancer la vérification en arrière-plan pour ne pas bloquer le démarrage
    _ = Task.Run(async () =>
    {
        try
        {
            await Task.Delay(1000); // Attendre que l'application soit démarrée
            var products = dataverseService.GetProducts();
            Console.WriteLine($"✅ Produits récupérés : {products.Count}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Erreur lors de la récupération des produits : " + ex.Message);
        }
    });
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
app.MapControllers(); // Ajouter le routage des contrôleurs API

app.Run();
