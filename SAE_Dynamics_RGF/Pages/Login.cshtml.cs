using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http; // pour HttpContext.Session
using SAE_Dynamics_RGF.Data;
using System;

namespace SAE_Dynamics_RGF.Pages
{
    public class LoginModel : PageModel
    {
        private readonly DataverseService _dataverseService;

        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; }

        [BindProperty]
        public string Crda6Id { get; set; }

        [BindProperty]
        public string Crda6Password { get; set; }

        [BindProperty]
        public string RegisterFirstName { get; set; }

        [BindProperty]
        public string RegisterLastName { get; set; }

        [BindProperty]
        public string RegisterEmail { get; set; }

        [BindProperty]
        public string RegisterIdentifiant { get; set; }

        [BindProperty]
        public string RegisterPassword { get; set; }

        [BindProperty]
        public DateTime? RegisterBirthDate { get; set; }

        [BindProperty]
        public string RegisterMobilePhone { get; set; }

        public string ErrorMessage { get; set; }

        public string RegisterErrorMessage { get; set; }

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
                HttpContext.Session.SetString("UserIdentifiant", contact.Identifiant ?? Crda6Id);
                HttpContext.Session.SetString("ContactId", contact.Id.ToString());

                if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                {
                    return Redirect(ReturnUrl);
                }

                return RedirectToPage("/Products");
            }
            else
            {
                ErrorMessage = "Identifiant ou mot de passe incorrect.";
                return Page();
            }
        }

        public IActionResult OnPostRegister()
        {
            var (contact, error) = _dataverseService.RegisterContact(
                RegisterFirstName,
                RegisterLastName,
                RegisterEmail,
                RegisterIdentifiant,
                RegisterPassword,
                RegisterBirthDate,
                RegisterMobilePhone);

            if (contact == null)
            {
                RegisterErrorMessage = string.IsNullOrWhiteSpace(error) ? "Inscription impossible." : error;
                return Page();
            }

            HttpContext.Session.SetString("IsLoggedIn", "true");
            HttpContext.Session.SetString("UserName", contact.FullName);
            HttpContext.Session.SetString("UserIdentifiant", contact.Identifiant ?? RegisterIdentifiant);
            HttpContext.Session.SetString("ContactId", contact.Id.ToString());

            if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            {
                return Redirect(ReturnUrl);
            }

            return RedirectToPage("/Products");
        }
    }
}
