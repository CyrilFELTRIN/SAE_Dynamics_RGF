using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SAE_Dynamics_RGF.Data;
using System.Collections.Generic;
using System.Linq;
using static SAE_Dynamics_RGF.Data.DataverseService;

namespace SAE_Dynamics_RGF.Pages
{
    public class InvoicesModel : PageModel
    {
        private readonly DataverseService _dataverseService;

        public List<SalesOrder> Invoices { get; set; } = new();
        public Dictionary<string, string> StateCodeOptions { get; set; } = new();

        public InvoicesModel(DataverseService dataverseService)
        {
            _dataverseService = dataverseService;
        }

        public void OnGet()
        {
            // Récupérer l'ID du contact depuis la session
            if (HttpContext.Session.GetString("IsLoggedIn") == "true" && 
                Guid.TryParse(HttpContext.Session.GetString("ContactId"), out var contactId))
            {
                Invoices = _dataverseService.GetInvoicesForContact(contactId);
                
                // Récupérer dynamiquement les options du champ statecode
                StateCodeOptions = _dataverseService.GetStateCodeOptions("invoice");
            }
        }
    }
}
