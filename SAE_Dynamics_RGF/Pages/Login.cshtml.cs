using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http; // pour HttpContext.Session

namespace SAE_Dynamics_RGF.Pages
{
    public class LoginModel : PageModel
    {
        [BindProperty]
        public string Crda6Id { get; set; }

        [BindProperty]
        public string Crda6Password { get; set; }

        public string ErrorMessage { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            // Exemple : à remplacer par ta vérification Dataverse
            if (Crda6Id == "lisean" && Crda6Password == "1234")
            {
                // ✅ Enregistre que l'utilisateur est connecté
                HttpContext.Session.SetString("IsLoggedIn", "true");
                HttpContext.Session.SetString("UserName", Crda6Id);

                return RedirectToPage("/Products");
            }
            else
            {
                ErrorMessage = "Identifiant ou mot de passe incorrect.";
                return Page();
            }
        }
    }
}
