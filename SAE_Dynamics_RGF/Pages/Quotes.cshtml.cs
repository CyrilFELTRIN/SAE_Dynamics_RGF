using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using SAE_Dynamics_RGF.Data;
using static SAE_Dynamics_RGF.Data.DataverseService;
using System;

namespace SAE_Dynamics_RGF.Pages
{
    public class QuotesModel : PageModel
    {
        private readonly DataverseService _dataverseService;

        public List<Quote> Quotes { get; set; } = new();

        public QuotesModel(DataverseService dataverseService)
        {
            _dataverseService = dataverseService;
        }

        public void OnGet()
        {
            if (HttpContext.Session.GetString("IsLoggedIn") != "true")
            {
                Response.Redirect("/Login");
                return;
            }

            var contactIdRaw = HttpContext.Session.GetString("ContactId");
            if (!Guid.TryParse(contactIdRaw, out var contactId))
            {
                Response.Redirect("/Login");
                return;
            }

            Quotes = _dataverseService.GetQuotesForContact(contactId);
        }
    }
}
