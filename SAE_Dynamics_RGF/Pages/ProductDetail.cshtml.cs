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
    public class ProductDetailModel : PageModel
    {
        private readonly DataverseService _dataverseService;

        public Product Product { get; set; }
        [BindProperty(SupportsGet = true)]
        public string ProductNumber { get; set; }

        public List<CurrencyOption> Currencies { get; set; } = new();

        public List<Avis> AvisList { get; set; } = new();

        [BindProperty]
        public string ReviewTitle { get; set; }

        [BindProperty]
        public string ReviewDescription { get; set; }

        [BindProperty]
        public int? ReviewNote { get; set; }

        [BindProperty]
        public string QuoteRubrique { get; set; }

        [BindProperty]
        public string QuoteDescription { get; set; }

        [BindProperty]
        public Guid CurrencyId { get; set; }

        public string QuoteErrorMessage { get; set; }
        public string QuoteSuccessMessage { get; set; }

        public string ReviewErrorMessage { get; set; }
        public string ReviewSuccessMessage { get; set; }
        public bool HasUserReviewed { get; set; }

        public ProductDetailModel(DataverseService dataverseService)
        {
            _dataverseService = dataverseService;
        }

        public void OnGet()
        {
            if (!string.IsNullOrEmpty(ProductNumber))
            {
                var key = ProductNumber.Trim();

                Product = _dataverseService.GetProducts()
                    .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.ProductNumber)
                                         && p.ProductNumber.Trim().Equals(key, System.StringComparison.OrdinalIgnoreCase));

                if (Product == null)
                {
                    Product = _dataverseService.GetParentProducts()
                        .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.ProductNumber)
                                             && p.ProductNumber.Trim().Equals(key, System.StringComparison.OrdinalIgnoreCase));
                }
            }

            Currencies = _dataverseService.GetCurrencies();

            if (Product != null)
            {
                AvisList = _dataverseService.GetAvisByProduct(Product.Id);
                
                // Vérifier si l'utilisateur connecté a déjà posté un avis
                if (HttpContext.Session.GetString("IsLoggedIn") == "true" && 
                    Guid.TryParse(HttpContext.Session.GetString("ContactId"), out var contactId))
                {
                    HasUserReviewed = _dataverseService.HasUserReviewedProduct(contactId, Product.Id);
                }
            }
        }

        public IActionResult OnPostAddReview()
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

            if (string.IsNullOrWhiteSpace(ProductNumber))
            {
                return RedirectToPage("/Products");
            }

            var key = ProductNumber.Trim();
            Product = _dataverseService.GetProducts()
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.ProductNumber)
                                     && p.ProductNumber.Trim().Equals(key, StringComparison.OrdinalIgnoreCase));

            if (Product == null)
            {
                Product = _dataverseService.GetParentProducts()
                    .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.ProductNumber)
                                         && p.ProductNumber.Trim().Equals(key, StringComparison.OrdinalIgnoreCase));
            }

            Currencies = _dataverseService.GetCurrencies();

            if (Product == null)
            {
                ReviewErrorMessage = "Produit introuvable.";
                return Page();
            }

            if (!ReviewNote.HasValue)
            {
                ReviewErrorMessage = "La note est obligatoire.";
                AvisList = _dataverseService.GetAvisByProduct(Product.Id);
                return Page();
            }

            if (ReviewNote.Value < 0 || ReviewNote.Value > 10)
            {
                ReviewErrorMessage = "La note doit être un entier entre 0 et 10.";
                AvisList = _dataverseService.GetAvisByProduct(Product.Id);
                return Page();
            }

            var (avisId, error) = _dataverseService.CreateAvis(
                contactId,
                Product.Id,
                ReviewTitle,
                ReviewDescription,
                ReviewNote.Value);

            if (avisId == null)
            {
                ReviewErrorMessage = string.IsNullOrWhiteSpace(error) ? "Création de l'avis impossible." : error;
                AvisList = _dataverseService.GetAvisByProduct(Product.Id);
                return Page();
            }

            return RedirectToPage("/ProductDetail", new { ProductNumber = ProductNumber });
        }

        public IActionResult OnPostRequestQuote()
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

            if (string.IsNullOrWhiteSpace(ProductNumber))
            {
                return RedirectToPage("/Products");
            }

            var key = ProductNumber.Trim();
            Product = _dataverseService.GetProducts()
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.ProductNumber)
                                     && p.ProductNumber.Trim().Equals(key, StringComparison.OrdinalIgnoreCase));

            if (Product == null)
            {
                Product = _dataverseService.GetParentProducts()
                    .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.ProductNumber)
                                         && p.ProductNumber.Trim().Equals(key, StringComparison.OrdinalIgnoreCase));
            }

            Currencies = _dataverseService.GetCurrencies();

            if (Product == null)
            {
                QuoteErrorMessage = "Produit introuvable.";
                return Page();
            }

            var (opportunityId, error) = _dataverseService.CreateOpportunityWithProduct(
                contactId,
                Product.Id,
                CurrencyId,
                QuoteRubrique,
                QuoteDescription);

            if (opportunityId == null)
            {
                QuoteErrorMessage = string.IsNullOrWhiteSpace(error) ? "Demande impossible." : error;
                return Page();
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                QuoteErrorMessage = error;
                return Page();
            }

            return RedirectToPage("/Requests");
        }
    }
}
