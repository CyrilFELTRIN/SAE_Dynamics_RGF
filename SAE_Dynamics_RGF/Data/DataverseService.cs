/*
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;

namespace SAE_Dynamics_RGF.Data
{
    // Ajout de l'interface IDisposable pour gérer la libération du ServiceClient
    public class DataverseService : IDisposable
    {
        // Remplacement de _connectionString par le ServiceClient lui-même pour persister la connexion
        private readonly ServiceClient _serviceClient;
        
        // La chaîne de connexion est maintenant utilisée uniquement pour l'initialisation unique
        private const string DataverseUrl = "https://butinfofeltrincyrilsandbox.crm12.dynamics.com/";
        //private const string DataverseAppId = "aaf9be65-10c2-415d-8c19-fb1489021330";
        private const string DataverseAppId = "51f81489-12ee-4a9e-aaae-a2591f45987d"; 

        public DataverseService()
        {
            // 1. Chaîne de connexion modifiée : RETRAIT de "LoginPrompt=Always"
            // Le client va tenter de réutiliser les jetons existants après la première connexion.
            string connectionString =
                $"AuthType=OAuth;" +
                $"Url={DataverseUrl};" +
                $"AppId={DataverseAppId};" +
                $"RedirectUri=http://localhost;"; 

            Console.WriteLine("🔌 Tentative d'initialisation de Dataverse...");
            
            try
            {
                // Initialisation unique du client
                _serviceClient = new ServiceClient(connectionString);

                if (_serviceClient == null || !_serviceClient.IsReady)
                {
                    Console.WriteLine("❌ Connexion Dataverse échouée ou non prête. Vérifiez les logs.");
                }
                else
                {
                    Console.WriteLine("✅ Connexion Dataverse initialisée avec succès.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(" Erreur critique à l'initialisation de Dataverse : " + ex.Message);
                _serviceClient = null; // S'assurer que le client est null en cas d'échec
            }
        }

        // Propriété utilitaire pour vérifier l'état avant d'appeler des méthodes
        public bool IsConnected => _serviceClient?.IsReady == true;

        public List<Product> GetProducts()
        {
            var products = new List<Product>();
            if (!IsConnected) return products;

            try
            {
                var query = new QueryExpression("product")
                {
                    ColumnSet = new ColumnSet("name", "productnumber", "parentproductid", "crda6_image", "statecode", "crda6_nouveaute"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("statecode", ConditionOperator.Equal, 0),
                            new ConditionExpression("parentproductid", ConditionOperator.NotNull)
                        }
                    }
                };

                var result = _serviceClient.RetrieveMultiple(query);
                Console.WriteLine($"📦 Produits récupérés : {result.Entities.Count}");

                foreach (var entity in result.Entities)
                {
                    var parentRef = entity.GetAttributeValue<EntityReference>("parentproductid");
                    if (parentRef == null) continue;

                    var product = new Product
                    {
                        Name = entity.GetAttributeValue<string>("name") ?? "Sans nom",
                        ProductNumber = entity.GetAttributeValue<string>("productnumber") ?? "N/A",
                        Category = parentRef.Name ?? "Sans parent",
                        ImageUrl = "/images/no-image.jpg", // Valeur par défaut
                        IsNew = entity.GetAttributeValue<bool?>("crda6_nouveaute") == true
                    };

                    // Récupère l'image en base64 si elle existe
                    if (entity.Contains("crda6_image"))
                    {
                        try
                        {
                            var imageBytes = entity.GetAttributeValue<byte[]>("crda6_image");
                            if (imageBytes != null && imageBytes.Length > 0)
                            {
                                string base64Image = Convert.ToBase64String(imageBytes);
                                product.ImageUrl = $"data:image/png;base64,{base64Image}";
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ Erreur lors de la récupération de l'image : {ex.Message}");
                        }
                    }

                    products.Add(product);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("⚠️ Erreur Dataverse (GetProducts) : " + ex.Message);
                if (ex.InnerException != null)
                    Console.WriteLine("➡️ Détail : " + ex.InnerException.Message);
            }

            return products;
        }

        public List<Product> GetParentProducts()
        {
            var parents = new List<Product>();
            if (!IsConnected) return parents;
            
            try
            {
                var query = new QueryExpression("product")
                {
                    ColumnSet = new ColumnSet("name", "productnumber", "crda6_image", "statecode", "crda6_nouveaute"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("parentproductid", ConditionOperator.Null),
                            new ConditionExpression("statecode", ConditionOperator.Equal, 0)
                        }
                    }
                };

                var result = _serviceClient.RetrieveMultiple(query);

                foreach (var entity in result.Entities)
                {
                    var product = new Product
                    {
                        Name = entity.GetAttributeValue<string>("name") ?? "Sans nom",
                        ProductNumber = entity.GetAttributeValue<string>("productnumber") ?? "N/A",
                        Category = "Sans parent",
                        ImageUrl = "/images/no-image.jpg", // Valeur par défaut
                        IsNew = entity.GetAttributeValue<bool?>("crda6_nouveaute") == true
                    };

                    if (entity.Contains("crda6_image"))
                    {
                        try
                        {
                            var imageBytes = entity.GetAttributeValue<byte[]>("crda6_image");
                            if (imageBytes != null && imageBytes.Length > 0)
                            {
                                string base64Image = Convert.ToBase64String(imageBytes);
                                product.ImageUrl = $"data:image/png;base64,{base64Image}";
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ Erreur lors de la récupération de l'image (parent) : {ex.Message}");
                        }
                    }

                    parents.Add(product);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("⚠️ Erreur récupération parents : " + ex.Message);
            }

            return parents;
        }

        public List<Product> GetNewProducts()
        {
            var products = new List<Product>();
            if (!IsConnected) return products;

            try
            {
                var query = new QueryExpression("product")
                {
                    ColumnSet = new ColumnSet("name", "productnumber", "parentproductid", "crda6_image", "statecode", "crda6_nouveaute"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("statecode", ConditionOperator.Equal, 0),
                            new ConditionExpression("crda6_nouveaute", ConditionOperator.Equal, true)
                        }
                    }
                };

                var result = _serviceClient.RetrieveMultiple(query);

                foreach (var entity in result.Entities)
                {
                    var parentRef = entity.GetAttributeValue<EntityReference>("parentproductid");

                    var product = new Product
                    {
                        Name = entity.GetAttributeValue<string>("name") ?? "Sans nom",
                        ProductNumber = entity.GetAttributeValue<string>("productnumber") ?? "N/A",
                        Category = parentRef?.Name ?? "Sans parent",
                        ImageUrl = "/images/no-image.jpg",
                        IsNew = true
                    };

                    if (entity.Contains("crda6_image"))
                    {
                        try
                        {
                            var imageBytes = entity.GetAttributeValue<byte[]>("crda6_image");
                            if (imageBytes != null && imageBytes.Length > 0)
                            {
                                string base64Image = Convert.ToBase64String(imageBytes);
                                product.ImageUrl = $"data:image/png;base64,{base64Image}";
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ Erreur lors de la récupération de l'image (nouveauté) : {ex.Message}");
                        }
                    }

                    products.Add(product);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("⚠️ Erreur Dataverse (GetNewProducts) : " + ex.Message);
                if (ex.InnerException != null)
                    Console.WriteLine("➡️ Détail : " + ex.InnerException.Message);
            }

            return products;
        }

        public List<Quote> GetQuotes()
        {
            var quotes = new List<Quote>();

            if (!IsConnected) return quotes;

            try
            {
                // Remplacer l'initialisation par l'utilisation de _serviceClient
                var query = new QueryExpression("quote")
                {
                    ColumnSet = new ColumnSet("name", "pricelevelid", "customerid", "statecode", "totalamount", "quotenumber", "createdon")
                };

                var result = _serviceClient.RetrieveMultiple(query);

                foreach (var entity in result.Entities)
                {
                    var quote = new Quote
                    {
                        Name = entity.GetAttributeValue<string>("name"),
                        QuoteNumber = entity.GetAttributeValue<string>("quotenumber"),
                        TotalAmount = entity.Contains("totalamount")
                            ? entity.GetAttributeValue<Money>("totalamount").Value
                            : 0m,
                        CustomerId = entity.Contains("customerid")
                            ? entity.GetAttributeValue<EntityReference>("customerid").Name
                            : "Client inconnu",
                        PriceLevelId = entity.Contains("pricelevelid")
                            ? entity.GetAttributeValue<EntityReference>("pricelevelid").Name
                            : "Non défini",
                        StateCode = entity.Contains("statecode")
                            ? entity.FormattedValues["statecode"]
                            : "Inconnu",
                        CreatedOn = entity.Contains("createdon")
                            ? entity.GetAttributeValue<DateTime>("createdon")
                            : DateTime.MinValue
                    };

                    quotes.Add(quote);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur lors de la récupération des devis : " + ex.Message);
            }

            return quotes;
        }

        public List<SalesOrder> GetSalesOrders()
        {
            var orders = new List<SalesOrder>();

            if (!IsConnected) return orders;

            try
            {
                // Remplacer l'initialisation par l'utilisation de _serviceClient
                var query = new QueryExpression("salesorder")
                {
                    ColumnSet = new ColumnSet("name", "ordernumber", "customerid", "pricelevelid", "statecode", "totalamount", "createdon")
                };

                var result = _serviceClient.RetrieveMultiple(query);

                foreach (var entity in result.Entities)
                {
                    var order = new SalesOrder
                    {
                        Name = entity.GetAttributeValue<string>("name"),
                        OrderNumber = entity.GetAttributeValue<string>("ordernumber"),
                        CustomerId = entity.Contains("customerid")
                            ? entity.GetAttributeValue<EntityReference>("customerid").Name
                            : "Client inconnu",
                        PriceLevelId = entity.Contains("pricelevelid")
                            ? entity.GetAttributeValue<EntityReference>("pricelevelid").Name
                            : "Non défini",
                        StateCode = entity.Contains("statecode")
                            ? entity.FormattedValues["statecode"]
                            : "Inconnu",
                        TotalAmount = entity.Contains("totalamount")
                            ? entity.GetAttributeValue<Money>("totalamount").Value
                            : 0m,
                        CreatedOn = entity.Contains("createdon")
                            ? entity.GetAttributeValue<DateTime>("createdon")
                            : DateTime.MinValue
                    };

                    orders.Add(order);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur lors de la récupération des commandes : " + ex.Message);
            }

            return orders;
        }

        // Implementation de IDisposable : nécessaire pour libérer les ressources du ServiceClient
        public void Dispose()
        {
            _serviceClient?.Dispose();
            // Demander au garbage collector d'éviter d'appeler le finaliseur pour l'objet.
            GC.SuppressFinalize(this); 
        }


        // --- Classes de Modèle (inchangées) ---

        public class Product
        {
            public string Name { get; set; }
            public string ProductNumber { get; set; }
            public string Category { get; set; }
            public string ImageUrl { get; set; }
            public bool IsNew { get; set; }
        }

        public class SalesOrder
        {
            public string Name { get; set; }
            public string OrderNumber { get; set; }
            public string CustomerId { get; set; }
            public string PriceLevelId { get; set; }
            public string StateCode { get; set; }
            public decimal TotalAmount { get; set; }
            public DateTime CreatedOn { get; set; }
        }

        public class Quote
        {
            public string Name { get; set; }
            public string QuoteNumber { get; set; }
            public string CustomerId { get; set; }
            public string PriceLevelId { get; set; }
            public string StateCode { get; set; }
            public decimal TotalAmount { get; set; }
            public DateTime CreatedOn { get; set; }
        }
    }
}

*/

using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Crm.Sdk.Messages; // Nécessaire pour le téléchargement HD
using System;
using System.Collections.Generic;

namespace SAE_Dynamics_RGF.Data
{
    public class DataverseService : IDisposable
    {
        private readonly ServiceClient _serviceClient;

        private const string DataverseUrl = "https://butinfofeltrincyrilsandbox.crm12.dynamics.com/";
        private const string DataverseAppId = "51f81489-12ee-4a9e-aaae-a2591f45987d";

        public DataverseService()
        {
            string connectionString =
                $"AuthType=OAuth;" +
                $"Url={DataverseUrl};" +
                $"AppId={DataverseAppId};" +
                $"RedirectUri=http://localhost;";

            Console.WriteLine("🔌 Tentative d'initialisation de Dataverse...");

            try
            {
                _serviceClient = new ServiceClient(connectionString);

                if (_serviceClient == null || !_serviceClient.IsReady)
                {
                    Console.WriteLine("❌ Connexion Dataverse échouée ou non prête. Vérifiez les logs.");
                }
                else
                {
                    Console.WriteLine("✅ Connexion Dataverse initialisée avec succès.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(" Erreur critique à l'initialisation de Dataverse : " + ex.Message);
                _serviceClient = null;
            }
        }

        public bool IsConnected => _serviceClient?.IsReady == true;

        /// <summary>
        /// Méthode utilitaire pour télécharger l'image en taille réelle (Full Resolution)
        /// au lieu de la miniature renvoyée par défaut par RetrieveMultiple.
        /// </summary>
        private byte[] GetFullImageBytes(string entityName, Guid recordId, string attributeName)
        {
            if (!IsConnected) return null;

            try
            {
                // 1. Initialiser la demande de téléchargement
                var fileRequest = new InitializeFileBlocksDownloadRequest
                {
                    Target = new EntityReference(entityName, recordId),
                    FileAttributeName = attributeName
                };

                var fileResponse = (InitializeFileBlocksDownloadResponse)_serviceClient.Execute(fileRequest);

                // 2. Télécharger les blocs
                var fileBytes = new List<byte>();
                long offset = 0;
                long blockSize = 4 * 1024 * 1024; // 4 MB max par bloc (standard)

                while (offset < fileResponse.FileSizeInBytes)
                {
                    var downloadRequest = new DownloadBlockRequest
                    {
                        FileContinuationToken = fileResponse.FileContinuationToken,
                        Offset = offset,
                        BlockLength = blockSize
                    };

                    var downloadResponse = (DownloadBlockResponse)_serviceClient.Execute(downloadRequest);

                    if (downloadResponse.Data != null)
                    {
                        fileBytes.AddRange(downloadResponse.Data);
                    }

                    offset += blockSize;
                }

                return fileBytes.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Impossible de télécharger l'image HD pour {recordId} : {ex.Message}");
                return null;
            }
        }

        public List<Product> GetProducts()
        {
            var products = new List<Product>();
            if (!IsConnected) return products;

            try
            {
                var query = new QueryExpression("product")
                {
                    ColumnSet = new ColumnSet("name", "productnumber", "parentproductid", "crda6_image", "statecode", "crda6_nouveaute"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("statecode", ConditionOperator.Equal, 0),
                            new ConditionExpression("parentproductid", ConditionOperator.NotNull)
                        }
                    }
                };

                var result = _serviceClient.RetrieveMultiple(query);
                Console.WriteLine($"📦 Produits récupérés : {result.Entities.Count}");

                foreach (var entity in result.Entities)
                {
                    var parentRef = entity.GetAttributeValue<EntityReference>("parentproductid");
                    if (parentRef == null) continue;

                    var product = new Product
                    {
                        Name = entity.GetAttributeValue<string>("name") ?? "Sans nom",
                        ProductNumber = entity.GetAttributeValue<string>("productnumber") ?? "N/A",
                        Category = parentRef.Name ?? "Sans parent",
                        ImageUrl = "/images/no-image.jpg",
                        IsNew = entity.GetAttributeValue<bool?>("crda6_nouveaute") == true
                    };

                    // Modification : Utilisation de GetFullImageBytes pour la HD
                    if (entity.Contains("crda6_image"))
                    {
                        var imageBytes = GetFullImageBytes("product", entity.Id, "crda6_image");
                        if (imageBytes != null && imageBytes.Length > 0)
                        {
                            string base64Image = Convert.ToBase64String(imageBytes);
                            product.ImageUrl = $"data:image/png;base64,{base64Image}";
                        }
                    }

                    products.Add(product);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("⚠️ Erreur Dataverse (GetProducts) : " + ex.Message);
                if (ex.InnerException != null)
                    Console.WriteLine("➡️ Détail : " + ex.InnerException.Message);
            }

            return products;
        }

        public List<Product> GetParentProducts()
        {
            var parents = new List<Product>();
            if (!IsConnected) return parents;

            try
            {
                var query = new QueryExpression("product")
                {
                    ColumnSet = new ColumnSet("name", "productnumber", "crda6_image", "statecode", "crda6_nouveaute"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("parentproductid", ConditionOperator.Null),
                            new ConditionExpression("statecode", ConditionOperator.Equal, 0)
                        }
                    }
                };

                var result = _serviceClient.RetrieveMultiple(query);

                foreach (var entity in result.Entities)
                {
                    var product = new Product
                    {
                        Name = entity.GetAttributeValue<string>("name") ?? "Sans nom",
                        ProductNumber = entity.GetAttributeValue<string>("productnumber") ?? "N/A",
                        Category = "Sans parent",
                        ImageUrl = "/images/no-image.jpg",
                        IsNew = entity.GetAttributeValue<bool?>("crda6_nouveaute") == true
                    };

                    // Modification : Utilisation de GetFullImageBytes pour la HD
                    if (entity.Contains("crda6_image"))
                    {
                        var imageBytes = GetFullImageBytes("product", entity.Id, "crda6_image");
                        if (imageBytes != null && imageBytes.Length > 0)
                        {
                            string base64Image = Convert.ToBase64String(imageBytes);
                            product.ImageUrl = $"data:image/png;base64,{base64Image}";
                        }
                    }

                    parents.Add(product);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("⚠️ Erreur récupération parents : " + ex.Message);
            }

            return parents;
        }

        public List<Product> GetNewProducts()
        {
            var products = new List<Product>();
            if (!IsConnected) return products;

            try
            {
                var query = new QueryExpression("product")
                {
                    ColumnSet = new ColumnSet("name", "productnumber", "parentproductid", "crda6_image", "statecode", "crda6_nouveaute"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("statecode", ConditionOperator.Equal, 0),
                            new ConditionExpression("crda6_nouveaute", ConditionOperator.Equal, true)
                        }
                    }
                };

                var result = _serviceClient.RetrieveMultiple(query);

                foreach (var entity in result.Entities)
                {
                    var parentRef = entity.GetAttributeValue<EntityReference>("parentproductid");

                    var product = new Product
                    {
                        Name = entity.GetAttributeValue<string>("name") ?? "Sans nom",
                        ProductNumber = entity.GetAttributeValue<string>("productnumber") ?? "N/A",
                        Category = parentRef?.Name ?? "Sans parent",
                        ImageUrl = "/images/no-image.jpg",
                        IsNew = true
                    };

                    // Modification : Utilisation de GetFullImageBytes pour la HD
                    if (entity.Contains("crda6_image"))
                    {
                        var imageBytes = GetFullImageBytes("product", entity.Id, "crda6_image");
                        if (imageBytes != null && imageBytes.Length > 0)
                        {
                            string base64Image = Convert.ToBase64String(imageBytes);
                            product.ImageUrl = $"data:image/png;base64,{base64Image}";
                        }
                    }

                    products.Add(product);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("⚠️ Erreur Dataverse (GetNewProducts) : " + ex.Message);
                if (ex.InnerException != null)
                    Console.WriteLine("➡️ Détail : " + ex.InnerException.Message);
            }

            return products;
        }

        public List<Quote> GetQuotes()
        {
            var quotes = new List<Quote>();
            if (!IsConnected) return quotes;

            try
            {
                var query = new QueryExpression("quote")
                {
                    ColumnSet = new ColumnSet("name", "pricelevelid", "customerid", "statecode", "totalamount", "quotenumber", "createdon")
                };

                var result = _serviceClient.RetrieveMultiple(query);

                foreach (var entity in result.Entities)
                {
                    var quote = new Quote
                    {
                        Name = entity.GetAttributeValue<string>("name"),
                        QuoteNumber = entity.GetAttributeValue<string>("quotenumber"),
                        TotalAmount = entity.Contains("totalamount")
                            ? entity.GetAttributeValue<Money>("totalamount").Value
                            : 0m,
                        CustomerId = entity.Contains("customerid")
                            ? entity.GetAttributeValue<EntityReference>("customerid").Name
                            : "Client inconnu",
                        PriceLevelId = entity.Contains("pricelevelid")
                            ? entity.GetAttributeValue<EntityReference>("pricelevelid").Name
                            : "Non défini",
                        StateCode = entity.Contains("statecode")
                            ? entity.FormattedValues["statecode"]
                            : "Inconnu",
                        CreatedOn = entity.Contains("createdon")
                            ? entity.GetAttributeValue<DateTime>("createdon")
                            : DateTime.MinValue
                    };

                    quotes.Add(quote);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur lors de la récupération des devis : " + ex.Message);
            }

            return quotes;
        }

        public List<SalesOrder> GetSalesOrders()
        {
            var orders = new List<SalesOrder>();
            if (!IsConnected) return orders;

            try
            {
                var query = new QueryExpression("salesorder")
                {
                    ColumnSet = new ColumnSet("name", "ordernumber", "customerid", "pricelevelid", "statecode", "totalamount", "createdon")
                };

                var result = _serviceClient.RetrieveMultiple(query);

                foreach (var entity in result.Entities)
                {
                    var order = new SalesOrder
                    {
                        Name = entity.GetAttributeValue<string>("name"),
                        OrderNumber = entity.GetAttributeValue<string>("ordernumber"),
                        CustomerId = entity.Contains("customerid")
                            ? entity.GetAttributeValue<EntityReference>("customerid").Name
                            : "Client inconnu",
                        PriceLevelId = entity.Contains("pricelevelid")
                            ? entity.GetAttributeValue<EntityReference>("pricelevelid").Name
                            : "Non défini",
                        StateCode = entity.Contains("statecode")
                            ? entity.FormattedValues["statecode"]
                            : "Inconnu",
                        TotalAmount = entity.Contains("totalamount")
                            ? entity.GetAttributeValue<Money>("totalamount").Value
                            : 0m,
                        CreatedOn = entity.Contains("createdon")
                            ? entity.GetAttributeValue<DateTime>("createdon")
                            : DateTime.MinValue
                    };

                    orders.Add(order);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur lors de la récupération des commandes : " + ex.Message);
            }

            return orders;
        }

        public void Dispose()
        {
            _serviceClient?.Dispose();
            GC.SuppressFinalize(this);
        }

        // --- Classes de Modèle ---

        public class Product
        {
            public string Name { get; set; }
            public string ProductNumber { get; set; }
            public string Category { get; set; }
            public string ImageUrl { get; set; }
            public bool IsNew { get; set; }
        }

        public class SalesOrder
        {
            public string Name { get; set; }
            public string OrderNumber { get; set; }
            public string CustomerId { get; set; }
            public string PriceLevelId { get; set; }
            public string StateCode { get; set; }
            public decimal TotalAmount { get; set; }
            public DateTime CreatedOn { get; set; }
        }

        public class Quote
        {
            public string Name { get; set; }
            public string QuoteNumber { get; set; }
            public string CustomerId { get; set; }
            public string PriceLevelId { get; set; }
            public string StateCode { get; set; }
            public decimal TotalAmount { get; set; }
            public DateTime CreatedOn { get; set; }
        }
    }
}