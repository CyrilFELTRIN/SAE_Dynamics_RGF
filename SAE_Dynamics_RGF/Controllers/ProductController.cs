using Microsoft.AspNetCore.Mvc;
using Microsoft.PowerPlatform.Dataverse.Client;
using SAE_Dynamics_RGF.Data;
using System.Net.Mime;

namespace SAE_Dynamics_RGF.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly DataverseService _dataverseService;

        public ProductController(DataverseService dataverseService)
        {
            _dataverseService = dataverseService;
        }

        [HttpGet("image/{productId:guid}")]
        public IActionResult GetProductImage(Guid productId)
        {
            try
            {
                // Rendre la méthode GetFullImageBytes publique pour y accéder directement
                var imageBytes = _dataverseService.GetProductImageBytes(productId);
                
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    // Retourner une image par défaut
                    return Redirect("/images/no-image.jpg");
                }

                // Retourner l'image avec le bon type MIME et cache
                Response.Headers.Add("Cache-Control", "public, max-age=3600"); // Cache 1 heure
                return File(imageBytes, "image/jpeg");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la récupération de l'image {productId}: {ex.Message}");
                // En cas d'erreur, retourner l'image par défaut
                return Redirect("/images/no-image.jpg");
            }
        }
    }
}
