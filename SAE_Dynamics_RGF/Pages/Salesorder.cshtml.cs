using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Http;
using SAE_Dynamics_RGF.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using static SAE_Dynamics_RGF.Data.DataverseService;

namespace SAE_Dynamics_RGF.Pages
{
    public class SalesOrdersModel : PageModel
    {
        private readonly DataverseService _dataverseService;

        public List<SalesOrder> SalesOrders { get; set; } = new();
        public bool HasOrders => SalesOrders.Any();

        [BindProperty]
        public Guid SavOrderId { get; set; }

        [BindProperty]
        public Guid SavProductId { get; set; }

        [BindProperty]
        public string SavName { get; set; }

        [BindProperty]
        public string SavDescription { get; set; }

        [BindProperty]
        public int? SavDiagnostic { get; set; }

        [BindProperty]
        public IFormFile SavPhoto { get; set; }

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

            foreach (var order in SalesOrders)
            {
                if (order.Id != Guid.Empty)
                {
                    order.Lines = _dataverseService.GetSalesOrderLines(order.Id);
                }
            }

            return Page();
        }

        public async Task<IActionResult> OnPostCreateSavFromOrder()
        {
            var currentPath = (Request?.Path.Value ?? string.Empty) + (Request?.QueryString.ToString() ?? string.Empty);
            if (HttpContext.Session.GetString("IsLoggedIn") != "true")
            {
                return RedirectToPage("/Login", new { ReturnUrl = currentPath });
            }

            if (!Guid.TryParse(HttpContext.Session.GetString("ContactId"), out var contactId))
            {
                return RedirectToPage("/Login", new { ReturnUrl = currentPath });
            }

            var orders = _dataverseService.GetSalesOrdersForContact(contactId);
            var order = orders.FirstOrDefault(o => o.Id == SavOrderId);
            if (order == null)
            {
                return RedirectToPage();
            }

            byte[] photoData = null;
            string photoFileName = null;

            if (SavPhoto != null)
            {
                // Vérifier la taille du fichier (max 5MB)
                if (SavPhoto.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError(string.Empty, "La photo ne doit pas dépasser 5MB.");
                    return Page();
                }

                // Vérifier le type de fichier
                if (!SavPhoto.ContentType.StartsWith("image/"))
                {
                    ModelState.AddModelError(string.Empty, "Veuillez sélectionner un fichier image valide.");
                    return Page();
                }

                using (var memoryStream = new MemoryStream())
                {
                    await SavPhoto.CopyToAsync(memoryStream);
                    photoData = memoryStream.ToArray();
                }
                photoFileName = SavPhoto.FileName;
            }

            var (savId, error) = _dataverseService.CreateSavRequest(
                contactId,
                SavProductId,
                SavName,
                SavDescription,
                order.CreatedOn,
                SavDiagnostic,
                photoData,
                photoFileName);

            if (savId == null)
            {
                return RedirectToPage();
            }

            return RedirectToPage("/Profile", new { tab = "sav" });
        }
    }
}