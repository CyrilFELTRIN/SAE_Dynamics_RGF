using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Crm.Sdk.Messages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAE_Dynamics_RGF.Data
{
    public class DataverseService : IDisposable
    {
        private readonly ServiceClient _serviceClient;
        private readonly Dictionary<Guid, string> _currencyCodeCache = new();

        private const string DataverseUrl = "https://butinfofeltrincyrilsandbox.crm12.dynamics.com/";
        private const string DataverseAppId = "51f81489-12ee-4a9e-aaae-a2591f45987d";

        public DataverseService()
        {
            string connectionString =
                $"AuthType=OAuth;" +
                $"Url={DataverseUrl};" +
                $"AppId={DataverseAppId};" +
                $"RedirectUri=http://localhost;";

            Console.WriteLine(" Tentative d'initialisation de Dataverse...");

            try
            {
                _serviceClient = new ServiceClient(connectionString);

                if (_serviceClient == null || !_serviceClient.IsReady)
                {
                    Console.WriteLine(" Connexion Dataverse échouée ou non prête. Vérifiez les logs.");
                }
                else
                {
                    Console.WriteLine(" Connexion Dataverse initialisée avec succès.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(" Erreur critique à l'initialisation de Dataverse : " + ex.Message);
                _serviceClient = null;
            }
        }

        public bool IsConnected => _serviceClient?.IsReady == true;

        private byte[] GetFullImageBytes(string entityName, Guid recordId, string attributeName)
        {
            if (!IsConnected) return null;

            try
            {
                var fileRequest = new InitializeFileBlocksDownloadRequest
                {
                    Target = new EntityReference(entityName, recordId),
                    FileAttributeName = attributeName
                };

                var fileResponse = (InitializeFileBlocksDownloadResponse)_serviceClient.Execute(fileRequest);

                var fileBytes = new List<byte>();
                long offset = 0;
                long blockSize = 4 * 1024 * 1024;

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
                Console.WriteLine($" Impossible de télécharger l'image HD pour {recordId} : {ex.Message}");
                return null;
            }
        }

        private string NormalizeCurrencyCode(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var v = raw.Trim();

            if (v.Equals("EUR", StringComparison.OrdinalIgnoreCase)) return "EUR";
            if (v.Equals("CHF", StringComparison.OrdinalIgnoreCase)) return "CHF";

            if (v.Contains("EUR", StringComparison.OrdinalIgnoreCase) || v.Contains("Euro", StringComparison.OrdinalIgnoreCase)) return "EUR";
            if (v.Contains("CHF", StringComparison.OrdinalIgnoreCase) || v.Contains("Franc", StringComparison.OrdinalIgnoreCase)) return "CHF";

            return null;
        }

        private string GetIsoCurrencyCode(Guid currencyId)
        {
            if (currencyId == Guid.Empty) return null;
            if (!IsConnected) return null;

            if (_currencyCodeCache.TryGetValue(currencyId, out var cached))
            {
                return cached;
            }

            try
            {
                var entity = _serviceClient.Retrieve("transactioncurrency", currencyId, new ColumnSet("isocurrencycode"));
                var iso = entity?.GetAttributeValue<string>("isocurrencycode");
                iso = NormalizeCurrencyCode(iso) ?? iso;
                _currencyCodeCache[currencyId] = iso;
                return iso;
            }
            catch
            {
                _currencyCodeCache[currencyId] = null;
                return null;
            }
        }

        private void PopulatePricesForProducts(List<Product> products)
        {
            if (!IsConnected) return;
            if (products == null || products.Count == 0) return;

            var byId = products
                .Where(p => p != null && p.Id != Guid.Empty)
                .GroupBy(p => p.Id)
                .ToDictionary(g => g.Key, g => g.First());

            if (byId.Count == 0) return;

            try
            {
                var query = new QueryExpression("productpricelevel")
                {
                    ColumnSet = new ColumnSet("amount", "transactioncurrencyid", "pricelevelid", "productid")
                };

                var result = _serviceClient.RetrieveMultiple(query);
                foreach (var entity in result.Entities)
                {
                    var productRef = entity.GetAttributeValue<EntityReference>("productid");
                    if (productRef == null || productRef.Id == Guid.Empty) continue;
                    if (!byId.TryGetValue(productRef.Id, out var product)) continue;

                    var amount = entity.GetAttributeValue<Money>("amount")?.Value;
                    if (amount == null) continue;

                    string currencyCode = null;

                    var currencyRef = entity.GetAttributeValue<EntityReference>("transactioncurrencyid");
                    if (currencyRef != null)
                    {
                        currencyCode = GetIsoCurrencyCode(currencyRef.Id);
                        currencyCode = NormalizeCurrencyCode(currencyCode) ?? NormalizeCurrencyCode(currencyRef.Name) ?? currencyCode;
                    }

                    if (currencyCode == null)
                    {
                        var priceLevelRef = entity.GetAttributeValue<EntityReference>("pricelevelid");
                        currencyCode = NormalizeCurrencyCode(priceLevelRef?.Name);
                    }

                    if (currencyCode == "EUR")
                    {
                        product.PriceEur ??= amount.Value;
                    }
                    else if (currencyCode == "CHF")
                    {
                        product.PriceChf ??= amount.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(" Erreur Dataverse (PopulatePricesForProducts) : " + ex.Message);
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
                    ColumnSet = new ColumnSet("name", "productnumber", "parentproductid", "crda6_image", "statecode", "crda6_nouveaute", "crda6_misalaune"),
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
                Console.WriteLine($" Produits récupérés : {result.Entities.Count}");

                foreach (var entity in result.Entities)
                {
                    var parentRef = entity.GetAttributeValue<EntityReference>("parentproductid");
                    if (parentRef == null) continue;

                    var product = new Product
                    {
                        Id = entity.Id,
                        Name = entity.GetAttributeValue<string>("name") ?? "Sans nom",
                        ProductNumber = entity.GetAttributeValue<string>("productnumber") ?? "N/A",
                        Category = parentRef.Name ?? "Sans parent",
                        ImageUrl = "/images/no-image.jpg",
                        IsNew = entity.GetAttributeValue<bool?>("crda6_nouveaute") == true,
                        IsFeatured = entity.GetAttributeValue<bool?>("crda6_misalaune") == true
                    };

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

                PopulatePricesForProducts(products);
            }
            catch (Exception ex)
            {
                Console.WriteLine(" Erreur Dataverse (GetProducts) : " + ex.Message);
                if (ex.InnerException != null)
                    Console.WriteLine(" Détail : " + ex.InnerException.Message);
            }

            return products;
        }

        public List<Product> GetFeaturedProducts()
        {
            var products = new List<Product>();
            if (!IsConnected) return products;

            try
            {
                var query = new QueryExpression("product")
                {
                    ColumnSet = new ColumnSet("name", "productnumber", "parentproductid", "crda6_image", "statecode", "crda6_nouveaute", "crda6_misalaune"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("statecode", ConditionOperator.Equal, 0),
                            new ConditionExpression("crda6_misalaune", ConditionOperator.Equal, true),
                            new ConditionExpression("parentproductid", ConditionOperator.NotNull)
                        }
                    }
                };

                var result = _serviceClient.RetrieveMultiple(query);
                foreach (var entity in result.Entities)
                {
                    var parentRef = entity.GetAttributeValue<EntityReference>("parentproductid");
                    if (parentRef == null) continue;

                    var product = new Product
                    {
                        Id = entity.Id,
                        Name = entity.GetAttributeValue<string>("name") ?? "Sans nom",
                        ProductNumber = entity.GetAttributeValue<string>("productnumber") ?? "N/A",
                        Category = parentRef.Name ?? "Sans parent",
                        ImageUrl = "/images/no-image.jpg",
                        IsNew = entity.GetAttributeValue<bool?>("crda6_nouveaute") == true,
                        IsFeatured = true
                    };

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

                PopulatePricesForProducts(products);
            }
            catch (Exception ex)
            {
                Console.WriteLine(" Erreur Dataverse (GetFeaturedProducts) : " + ex.Message);
                if (ex.InnerException != null)
                    Console.WriteLine(" Détail : " + ex.InnerException.Message);
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
                    ColumnSet = new ColumnSet("name", "productnumber", "crda6_image", "statecode", "crda6_nouveaute", "crda6_misalaune"),
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
                        Id = entity.Id,
                        Name = entity.GetAttributeValue<string>("name") ?? "Sans nom",
                        ProductNumber = entity.GetAttributeValue<string>("productnumber") ?? "N/A",
                        Category = "Sans parent",
                        ImageUrl = "/images/no-image.jpg",
                        IsNew = entity.GetAttributeValue<bool?>("crda6_nouveaute") == true,
                        IsFeatured = entity.GetAttributeValue<bool?>("crda6_misalaune") == true
                    };

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

                PopulatePricesForProducts(parents);
            }
            catch (Exception ex)
            {
                Console.WriteLine(" Erreur récupération parents : " + ex.Message);
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
                    ColumnSet = new ColumnSet("name", "productnumber", "parentproductid", "crda6_image", "statecode", "crda6_nouveaute", "crda6_misalaune"),
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
                        Id = entity.Id,
                        Name = entity.GetAttributeValue<string>("name") ?? "Sans nom",
                        ProductNumber = entity.GetAttributeValue<string>("productnumber") ?? "N/A",
                        Category = parentRef?.Name ?? "Sans parent",
                        ImageUrl = "/images/no-image.jpg",
                        IsNew = true,
                        IsFeatured = entity.GetAttributeValue<bool?>("crda6_misalaune") == true
                    };

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

                PopulatePricesForProducts(products);
            }
            catch (Exception ex)
            {
                Console.WriteLine(" Erreur Dataverse (GetNewProducts) : " + ex.Message);
                if (ex.InnerException != null)
                    Console.WriteLine(" Détail : " + ex.InnerException.Message);
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
                    ColumnSet = new ColumnSet("name", "pricelevelid", "customerid", "statecode", "statuscode", "totalamount", "quotenumber", "createdon")
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
                        StatusCode = entity.GetAttributeValue<OptionSetValue>("statuscode")?.Value,
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

        public List<Quote> GetQuotesForContact(Guid contactId)
        {
            var quotes = new List<Quote>();
            if (!IsConnected) return quotes;
            if (contactId == Guid.Empty) return quotes;

            try
            {
                var accountIds = new List<Guid>();

                var accountQuery = new QueryExpression("account")
                {
                    ColumnSet = new ColumnSet(false),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("primarycontactid", ConditionOperator.Equal, contactId)
                        }
                    }
                };

                var accountResult = _serviceClient.RetrieveMultiple(accountQuery);
                foreach (var account in accountResult.Entities)
                {
                    accountIds.Add(account.Id);
                }

                var allowedCustomerIds = new List<Guid> { contactId };
                allowedCustomerIds.AddRange(accountIds);

                var query = new QueryExpression("quote")
                {
                    ColumnSet = new ColumnSet("name", "pricelevelid", "customerid", "statecode", "statuscode", "totalamount", "quotenumber", "createdon"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("customerid", ConditionOperator.In, allowedCustomerIds.Cast<object>().ToArray())
                        }
                    }
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
                        StatusCode = entity.GetAttributeValue<OptionSetValue>("statuscode")?.Value,
                        CreatedOn = entity.Contains("createdon")
                            ? entity.GetAttributeValue<DateTime>("createdon")
                            : DateTime.MinValue
                    };

                    quotes.Add(quote);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur lors de la récupération des devis (filtrés) : " + ex.Message);
            }

            return quotes;
        }

        public List<SalesOrder> GetSalesOrdersForContact(Guid contactId)
        {
            var orders = new List<SalesOrder>();
            if (!IsConnected) return orders;
            if (contactId == Guid.Empty) return orders;

            try
            {
                // Récupérer les comptes dont le contact est le contact principal
                var accountIds = new List<Guid>();
                var accountQuery = new QueryExpression("account")
                {
                    ColumnSet = new ColumnSet(false),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("primarycontactid", ConditionOperator.Equal, contactId)
                        }
                    }
                };

                var accountResult = _serviceClient.RetrieveMultiple(accountQuery);
                foreach (var account in accountResult.Entities)
                {
                    accountIds.Add(account.Id);
                }

                // Liste des IDs autorisés : le contact + ses comptes
                var allowedCustomerIds = new List<Guid> { contactId };
                allowedCustomerIds.AddRange(accountIds);

                // Requête pour les commandes
                var query = new QueryExpression("salesorder")
                {
                    ColumnSet = new ColumnSet("name", "ordernumber", "customerid", "pricelevelid", "statecode", "statuscode", "totalamount", "createdon"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("customerid", ConditionOperator.In, allowedCustomerIds.Cast<object>().ToArray())
                        }
                    }
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
                        StatusCode = entity.GetAttributeValue<OptionSetValue>("statuscode")?.Value,
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
                Console.WriteLine("Erreur lors de la récupération des commandes (filtrées) : " + ex.Message);
            }

            return orders;
        }

        public List<SalesOrder> GetSalesOrders()
        {
            var orders = new List<SalesOrder>();
            if (!IsConnected) return orders;

            try
            {
                var query = new QueryExpression("salesorder")
                {
                    ColumnSet = new ColumnSet("name", "ordernumber", "customerid", "pricelevelid", "statecode", "statuscode", "totalamount", "createdon")
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
                        StatusCode = entity.GetAttributeValue<OptionSetValue>("statuscode")?.Value,
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

        public Contact AuthenticateContact(string identifiant, string motDePasse)
        {
            if (!IsConnected) return null;
            if (string.IsNullOrWhiteSpace(identifiant) || string.IsNullOrWhiteSpace(motDePasse)) return null;

            try
            {
                var query = new QueryExpression("contact")
                {
                    ColumnSet = new ColumnSet("fullname", "crda6_identifiant"),
                    TopCount = 1,
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("crda6_identifiant", ConditionOperator.Equal, identifiant),
                            new ConditionExpression("crda6_motdepasse", ConditionOperator.Equal, motDePasse)
                        }
                    }
                };

                var result = _serviceClient.RetrieveMultiple(query);
                var entity = result?.Entities?.FirstOrDefault();
                if (entity == null) return null;

                return new Contact
                {
                    Id = entity.Id,
                    FullName = entity.GetAttributeValue<string>("fullname") ?? identifiant,
                    Identifiant = entity.GetAttributeValue<string>("crda6_identifiant")
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur lors de l'authentification (contact) : " + ex.Message);
                return null;
            }
        }

        public void Dispose()
        {
            _serviceClient?.Dispose();
            GC.SuppressFinalize(this);
        }

        public class Product
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string ProductNumber { get; set; }
            public string Category { get; set; }
            public string ImageUrl { get; set; }
            public bool IsNew { get; set; }
            public bool IsFeatured { get; set; }
            public decimal? PriceEur { get; set; }
            public decimal? PriceChf { get; set; }
        }

        public class Contact
        {
            public Guid Id { get; set; }
            public string FullName { get; set; }
            public string Identifiant { get; set; }
        }

        public class SalesOrder
        {
            public string Name { get; set; }
            public string OrderNumber { get; set; }
            public string CustomerId { get; set; }
            public string PriceLevelId { get; set; }
            public string StateCode { get; set; }
            public int? StatusCode { get; set; }
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
            public int? StatusCode { get; set; }
            public decimal TotalAmount { get; set; }
            public DateTime CreatedOn { get; set; }
        }
    }
}