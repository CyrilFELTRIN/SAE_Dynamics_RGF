using Microsoft.AspNetCore.Mvc.RazorPages;
using SAE_Dynamics_RGF.Data;
using System.Collections.Generic;
using static SAE_Dynamics_RGF.Data.DataverseService;

namespace SAE_Dynamics_RGF.Pages
{
    public class ProductsModel : PageModel
    {
        private readonly DataverseService _dataverseService;

        public List<Product> Products { get; set; } = new();
        public List<Product> ParentProducts { get; set; } = new(); // Pour le filtre

        public ProductsModel(DataverseService dataverseService)
        {
            _dataverseService = dataverseService;
        }

        public void OnGet()
        {
            Products = _dataverseService.GetProducts();
            ParentProducts = _dataverseService.GetParentProducts();
        }
    }
}
