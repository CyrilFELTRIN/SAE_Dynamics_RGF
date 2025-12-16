using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SAE_Dynamics_RGF.Data;
using System.Collections.Generic;

namespace SAE_Dynamics_RGF.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly DataverseService _dataverseService;

        public List<DataverseService.Product> FeaturedProducts { get; set; } = new();
        public List<DataverseService.Product> NewProducts { get; set; } = new();

        public IndexModel(ILogger<IndexModel> logger, DataverseService dataverseService)
        {
            _logger = logger;
            _dataverseService = dataverseService;
        }

        public void OnGet()
        {
            FeaturedProducts = _dataverseService.GetFeaturedProducts();
            NewProducts = _dataverseService.GetNewProducts();
        }
    }
}
