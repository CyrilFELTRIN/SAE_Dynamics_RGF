using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http; // pour HttpContext.Session
using SAE_Dynamics_RGF.Data;

namespace SAE_Dynamics_RGF.Pages
{
    public class LoginModel : PageModel
    {
        private readonly DataverseService _dataverseService;

        [BindProperty]
        public string Crda6Id { get; set; }

        [BindProperty]
        public string Crda6Password { get; set; }

        public string ErrorMessage { get; set; }

        public LoginModel(DataverseService dataverseService)
        {
            _dataverseService = dataverseService;
        }

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            var contact = _dataverseService.AuthenticateContact(Crda6Id, Crda6Password);

            if (contact != null)
            {
                // ✅ Enregistre que l'utilisateur est connecté
                HttpContext.Session.SetString("IsLoggedIn", "true");
                HttpContext.Session.SetString("UserName", contact.FullName);
                HttpContext.Session.SetString("ContactId", contact.Id.ToString());

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
