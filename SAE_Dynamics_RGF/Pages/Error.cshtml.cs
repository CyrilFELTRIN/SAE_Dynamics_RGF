using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SAE_Dynamics_RGF.Pages
{
    public class ErrorModel : PageModel
    {
        public int StatusCode { get; set; }

        public void OnGet()
        {
            // Récupère le code d'erreur depuis l'objet Features
            var statusCodeFeature = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodeReExecuteFeature>();
            StatusCode = HttpContext.Response.StatusCode;

            // Si tu veux, tu peux essayer de récupérer le code original
            var originalStatusCode = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodeReExecuteFeature>()?.OriginalPath;
        }
    }
}
