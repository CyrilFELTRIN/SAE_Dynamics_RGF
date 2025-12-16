using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SAE_Dynamics_RGF.Pages
{
    public class LogoutModel : PageModel
    {
        public IActionResult OnPost()
        {
            // 🔒 Supprime toutes les données de session
            HttpContext.Session.Clear();

            // 🔁 Redirige vers la page de connexion
            return RedirectToPage("/Login");
        }

        public IActionResult OnGet()
        {
            // Permet aussi de déconnecter en cas d’accès direct via URL
            HttpContext.Session.Clear();
            return RedirectToPage("/Login");
        }
    }
}
