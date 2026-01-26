using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SAE_Dynamics_RGF.Data;
using static SAE_Dynamics_RGF.Data.DataverseService;

namespace SAE_Dynamics_RGF.Pages
{
    public class MoreModel : PageModel
    {
        private readonly DataverseService _dataverseService;

        public SiteWebContent SiteContent { get; private set; } = new SiteWebContent();

        public MoreModel(DataverseService dataverseService)
        {
            _dataverseService = dataverseService;
        }

        public void OnGet()
        {
            SiteContent = _dataverseService.GetSiteWebContent() ?? new SiteWebContent();
        }
    }
}
