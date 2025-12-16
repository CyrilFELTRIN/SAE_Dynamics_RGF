using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SAE_Dynamics_RGF.Data;
using static SAE_Dynamics_RGF.Data.DataverseService;

namespace SAE_Dynamics_RGF.Pages
{
    public class QuotesModel : PageModel
    {
        private readonly DataverseService _dataverseService;

        public List<Quote> Quotes { get; set; } = new();

        public QuotesModel(DataverseService dataverseService)
        {
            _dataverseService = dataverseService;
        }

        public void OnGet()
        {
            Quotes = _dataverseService.GetQuotes();
        }
    }
}
