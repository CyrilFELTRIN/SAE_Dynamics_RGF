using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SAE_Dynamics_RGF.Data;
using System.Collections.Generic;
using System.Linq;
using static SAE_Dynamics_RGF.Data.DataverseService;

namespace SAE_Dynamics_RGF.Pages
{
    public class SavRequestsModel : PageModel
    {
        private readonly DataverseService _dataverseService;

        public List<SavRequest> SavRequests { get; set; } = new();

        public SavRequestsModel(DataverseService dataverseService)
        {
            _dataverseService = dataverseService;
        }

        public void OnGet()
        {
            // Récupérer l'ID du contact depuis la session
            if (HttpContext.Session.GetString("IsLoggedIn") == "true" && 
                Guid.TryParse(HttpContext.Session.GetString("ContactId"), out var contactId))
            {
                SavRequests = _dataverseService.GetSavRequestsForContact(contactId);
            }
        }
    }
}
