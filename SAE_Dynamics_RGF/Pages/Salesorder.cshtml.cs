using Microsoft.AspNetCore.Mvc.RazorPages;
using SAE_Dynamics_RGF.Data;
using System.Collections.Generic;
using static SAE_Dynamics_RGF.Data.DataverseService;

namespace SAE_Dynamics_RGF.Pages
{
    public class SalesOrdersModel : PageModel
    {
        private readonly DataverseService _dataverseService;

        public List<SalesOrder> SalesOrders { get; set; }

        public SalesOrdersModel(DataverseService dataverseService)
        {
            _dataverseService = dataverseService;
        }

        public void OnGet()
        {
            SalesOrders = _dataverseService.GetSalesOrders();
        }
    }
}
