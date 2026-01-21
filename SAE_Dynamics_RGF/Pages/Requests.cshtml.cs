using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using SAE_Dynamics_RGF.Data;
using System;
using System.Collections.Generic;
using static SAE_Dynamics_RGF.Data.DataverseService;

namespace SAE_Dynamics_RGF.Pages
{
    public class RequestsModel : PageModel
    {
        private readonly DataverseService _dataverseService;

        public List<Opportunity> Opportunities { get; set; } = new();

        public RequestsModel(DataverseService dataverseService)
        {
            _dataverseService = dataverseService;
        }

        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetString("IsLoggedIn") != "true")
            {
                var returnUrl = (Request?.Path.Value ?? string.Empty) + (Request?.QueryString.ToString() ?? string.Empty);
                return RedirectToPage("/Login", new { ReturnUrl = returnUrl });
            }

            if (!Guid.TryParse(HttpContext.Session.GetString("ContactId"), out var contactId))
            {
                var returnUrl = (Request?.Path.Value ?? string.Empty) + (Request?.QueryString.ToString() ?? string.Empty);
                return RedirectToPage("/Login", new { ReturnUrl = returnUrl });
            }

            Opportunities = _dataverseService.GetOpportunitiesForContact(contactId);
            return Page();
        }
    }
}
