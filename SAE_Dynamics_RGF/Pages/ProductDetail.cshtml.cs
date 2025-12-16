using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SAE_Dynamics_RGF.Data;
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
        }
    }
}
