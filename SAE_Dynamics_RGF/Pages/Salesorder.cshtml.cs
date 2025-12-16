using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SAE_Dynamics_RGF.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using static SAE_Dynamics_RGF.Data.DataverseService;

namespace SAE_Dynamics_RGF.Pages
{
    public class SalesOrdersModel : PageModel
    {
        private readonly DataverseService _dataverseService;

        public List<SalesOrder> SalesOrders { get; set; } = new();
        public bool HasOrders => SalesOrders.Any();

        public SalesOrdersModel(DataverseService dataverseService)
        {
            _dataverseService = dataverseService;
        }

        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetString("IsLoggedIn") != "true")
            {
                return RedirectToPage("/Login");
            }

            if (!Guid.TryParse(HttpContext.Session.GetString("ContactId"), out var contactId))
            {
                return RedirectToPage("/Login");
            }

            SalesOrders = _dataverseService.GetSalesOrdersForContact(contactId);
            return Page();
        }
    }
}