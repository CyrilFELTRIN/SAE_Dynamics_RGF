using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using SAE_Dynamics_RGF.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using static SAE_Dynamics_RGF.Data.DataverseService;

namespace SAE_Dynamics_RGF.Pages
{
    public class ProfileModel : PageModel
    {
        private readonly DataverseService _dataverseService;

        public string ActiveTab { get; set; } = "info";

        public string UserName { get; set; }
        public string UserIdentifiant { get; set; }

        public List<Quote> Quotes { get; set; } = new();
        public List<SalesOrder> SalesOrders { get; set; } = new();
        public List<SalesOrder> Invoices { get; set; } = new();

        public ProfileModel(DataverseService dataverseService)
        {
            _dataverseService = dataverseService;
        }

        public IActionResult OnGet(string tab)
        {
            if (HttpContext.Session.GetString("IsLoggedIn") != "true")
            {
                return RedirectToPage("/Login");
            }

            ActiveTab = NormalizeTab(tab);

            UserName = HttpContext.Session.GetString("UserName") ?? string.Empty;
            UserIdentifiant = HttpContext.Session.GetString("UserIdentifiant") ?? string.Empty;

            if (!Guid.TryParse(HttpContext.Session.GetString("ContactId"), out var contactId))
            {
                return RedirectToPage("/Login");
            }

            Quotes = _dataverseService.GetQuotesForContact(contactId);
            SalesOrders = _dataverseService.GetSalesOrdersForContact(contactId);
            Invoices = SalesOrders.Where(o => o.StatusCode == 4).ToList();

            return Page();
        }

        private static string NormalizeTab(string tab)
        {
            var t = (tab ?? string.Empty).Trim().ToLowerInvariant();
            return t switch
            {
                "info" => "info",
                "quotes" => "quotes",
                "orders" => "orders",
                "invoices" => "invoices",
                _ => "info"
            };
        }

        public string GetQuoteStatusText(Quote quote)
        {
            return quote?.StatusCode switch
            {
                0 => "Brouillon",
                1 => "Actif",
                2 => "Conclu",
                3 => "Fermé",
                _ => "En attente"
            };
        }

        public string GetOrderStatusText(SalesOrder order)
        {
            return order?.StatusCode switch
            {
                0 => "Actif",
                1 => "Envoyée",
                2 => "Annulée",
                3 => "Exécutée",
                4 => "Facturée",
                _ => "En attente"
            };
        }
    }
}
