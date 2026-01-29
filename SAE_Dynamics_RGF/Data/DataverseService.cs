using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
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

        public void Dispose()
        {
            _serviceClient?.Dispose();
        }

        public Contact AuthenticateContact(string identifiant, string password)
        {
            if (!IsConnected) return null;
            if (string.IsNullOrWhiteSpace(identifiant) || string.IsNullOrWhiteSpace(password)) return null;

            try
            {
                var query = new QueryExpression("contact")
                {
                    ColumnSet = new ColumnSet("firstname", "lastname", "crda6_identifiant", "crda6_motdepasse"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("crda6_identifiant", ConditionOperator.Equal, identifiant)
                        }
                    }
                };

                var result = _serviceClient.RetrieveMultiple(query);
                if (result.Entities.Count == 0) return null;

                var entity = result.Entities[0];
                var storedPassword = entity.GetAttributeValue<string>("crda6_motdepasse");
                
                if (string.IsNullOrEmpty(storedPassword) || storedPassword != password)
                    return null;

                return new Contact
                {
                    Id = entity.Id,
                    FullName = $"{entity.GetAttributeValue<string>("firstname")} {entity.GetAttributeValue<string>("lastname")}".Trim(),
                    Identifiant = entity.GetAttributeValue<string>("crda6_identifiant")
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur lors de l'authentification du contact : " + ex.Message);
                return null;
            }
        }

        public (Contact Contact, string ErrorMessage) RegisterContact(
            string firstName,
            string lastName,
            string email,
            string identifiant,
            string password,
            DateTime? birthDate,
            string mobilePhone)
        {
            if (!IsConnected) return (null, "Connexion Dataverse indisponible.");

            try
            {
                // Vérifier si l'identifiant existe déjà
                var existingQuery = new QueryExpression("contact")
                {
                    ColumnSet = new ColumnSet("contactid"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("crda6_identifiant", ConditionOperator.Equal, identifiant)
                        }
                    }
                };

                var existingResult = _serviceClient.RetrieveMultiple(existingQuery);
                if (existingResult.Entities.Count > 0)
                    return (null, "Cet identifiant est déjà utilisé.");

                var contact = new Entity("contact")
                {
                    ["firstname"] = firstName,
                    ["lastname"] = lastName,
                    ["emailaddress1"] = email,
                    ["crda6_identifiant"] = identifiant,
                    ["crda6_motdepasse"] = password
                };

                if (birthDate.HasValue)
                {
                    contact["birthdate"] = birthDate.Value;
                }

                if (!string.IsNullOrWhiteSpace(mobilePhone))
                {
                    contact["mobilephone"] = mobilePhone;
                }

                var contactId = _serviceClient.Create(contact);
                if (contactId == Guid.Empty)
                    return (null, "Création du contact impossible.");

                return (new Contact
                {
                    Id = contactId,
                    FullName = $"{firstName} {lastName}".Trim(),
                    Identifiant = identifiant
                }, string.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur lors de l'inscription du contact : " + ex.Message);
                return (null, ex.Message);
            }
        }

        public Dictionary<string, string> GetStateCodeOptions(string entityName)
        {
            var options = new Dictionary<string, string>();
            if (!IsConnected) return options;

            try
            {
                // Récupérer les métadonnées du champ statecode pour l'entité
                var attributeRequest = new RetrieveAttributeRequest
                {
                    EntityLogicalName = entityName,
                    LogicalName = "statecode",
                    RetrieveAsIfPublished = true
                };

                var attributeResponse = (RetrieveAttributeResponse)_serviceClient.Execute(attributeRequest);
                var stateAttribute = (StateAttributeMetadata)attributeResponse.AttributeMetadata;

                // Parcourir les options de statut
                foreach (var option in stateAttribute.OptionSet.Options)
                {
                    var optionMetadata = option as OptionMetadata;
                    if (optionMetadata != null)
                    {
                        options.Add(optionMetadata.Value.ToString(), optionMetadata.Label?.UserLocalizedLabel?.Label ?? $"Status {optionMetadata.Value}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la récupération des options statecode pour {entityName}: {ex.Message}");
            }

            return options;
        }

        public byte[] GetProductImageBytes(Guid productId)
        {
            return GetFullImageBytes("product", productId, "crda6_image");
        }

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
                        // Utiliser une URL d'image au lieu du base64 pour de meilleures performances
                        product.ImageUrl = $"/api/product/image/{entity.Id}";
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
                        // Utiliser une URL d'image au lieu du base64 pour de meilleures performances
                        product.ImageUrl = $"/api/product/image/{entity.Id}";
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
                        // Utiliser une URL d'image au lieu du base64 pour de meilleures performances
                        product.ImageUrl = $"/api/product/image/{entity.Id}";
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
                        // Utiliser une URL d'image au lieu du base64 pour de meilleures performances
                        product.ImageUrl = $"/api/product/image/{entity.Id}";
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
                            : DateTime.MinValue,
                        ProductName = "Non spécifié",
                        ProductNumber = "",
                        ProductId = Guid.Empty
                    };

                    // Récupérer les informations du produit depuis les lignes du devis
                    var detailQuery = new QueryExpression("quotedetail")
                    {
                        ColumnSet = new ColumnSet("productid"),
                        Criteria = new FilterExpression
                        {
                            Conditions = { new ConditionExpression("quoteid", ConditionOperator.Equal, entity.Id) }
                        },
                        TopCount = 1
                    };

                    var detailResult = _serviceClient.RetrieveMultiple(detailQuery);
                    if (detailResult.Entities.Count > 0)
                    {
                        var productRef = detailResult.Entities[0].GetAttributeValue<EntityReference>("productid");
                        if (productRef != null && productRef.Id != Guid.Empty)
                        {
                            quote.ProductId = productRef.Id;
                            // Récupérer les détails du produit
                            var productQuery = new QueryExpression("product")
                            {
                                ColumnSet = new ColumnSet("name", "productnumber"),
                                Criteria = new FilterExpression
                                {
                                    Conditions = { new ConditionExpression("productid", ConditionOperator.Equal, productRef.Id) }
                                }
                            };

                            var productResult = _serviceClient.RetrieveMultiple(productQuery);
                            if (productResult.Entities.Count > 0)
                            {
                                var productEntity = productResult.Entities[0];
                                quote.ProductName = productEntity.GetAttributeValue<string>("name") ?? "Non spécifié";
                                quote.ProductNumber = productEntity.GetAttributeValue<string>("productnumber") ?? "";
                            }
                        }
                    }

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
                    ColumnSet = new ColumnSet("name", "ordernumber", "customerid", "pricelevelid", "statecode", "statuscode", "totalamount", "createdon", "quoteid"),
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
                    // Récupérer les informations du produit depuis les lignes de commande
                    string productName = "Non spécifié";
                    string productNumber = "";
                    string quoteNumber = "";
                    EntityReference productRef = null;

                    // Récupérer le numéro de devis associé
                    if (entity.Contains("quoteid"))
                    {
                        var quoteRef = entity.GetAttributeValue<EntityReference>("quoteid");
                        if (quoteRef != null)
                        {
                            var quoteQuery = new QueryExpression("quote")
                            {
                                ColumnSet = new ColumnSet("quotenumber"),
                                Criteria = new FilterExpression
                                {
                                    Conditions = { new ConditionExpression("quoteid", ConditionOperator.Equal, quoteRef.Id) }
                                }
                            };

                            var quoteResult = _serviceClient.RetrieveMultiple(quoteQuery);
                            if (quoteResult.Entities.Count > 0)
                            {
                                quoteNumber = quoteResult.Entities[0].GetAttributeValue<string>("quotenumber") ?? "";
                            }
                        }
                    }

                    // Récupérer les informations du produit depuis les lignes de commande
                    var detailQuery = new QueryExpression("salesorderdetail")
                    {
                        ColumnSet = new ColumnSet("productid"),
                        Criteria = new FilterExpression
                        {
                            Conditions = { new ConditionExpression("salesorderid", ConditionOperator.Equal, entity.Id) }
                        },
                        TopCount = 1
                    };

                    var detailResult = _serviceClient.RetrieveMultiple(detailQuery);
                    if (detailResult.Entities.Count > 0)
                    {
                        productRef = detailResult.Entities[0].GetAttributeValue<EntityReference>("productid");
                        if (productRef != null && productRef.Id != Guid.Empty)
                        {
                            // Récupérer les détails du produit
                            var productQuery = new QueryExpression("product")
                            {
                                ColumnSet = new ColumnSet("name", "productnumber"),
                                Criteria = new FilterExpression
                                {
                                    Conditions = { new ConditionExpression("productid", ConditionOperator.Equal, productRef.Id) }
                                }
                            };

                            var productResult = _serviceClient.RetrieveMultiple(productQuery);
                            if (productResult.Entities.Count > 0)
                            {
                                var productEntity = productResult.Entities[0];
                                productName = productEntity.GetAttributeValue<string>("name") ?? "Non spécifié";
                                productNumber = productEntity.GetAttributeValue<string>("productnumber") ?? "";
                            }
                        }
                    }

                    var order = new SalesOrder
                    {
                        Id = entity.Id,
                        Name = entity.GetAttributeValue<string>("name"),
                        OrderNumber = entity.GetAttributeValue<string>("ordernumber"),
                        InvoiceNumber = null, // Le champ n'existe pas dans Dataverse
                        QuoteNumber = quoteNumber,
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
                            : DateTime.MinValue,
                        ProductName = productName,
                        ProductNumber = productNumber,
                        ProductId = productRef != null ? productRef.Id : Guid.Empty
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
                        Id = entity.Id,
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

        public List<SalesOrderLine> GetSalesOrderLines(Guid salesOrderId)
        {
            var lines = new List<SalesOrderLine>();
            if (!IsConnected) return lines;
            if (salesOrderId == Guid.Empty) return lines;

            try
            {
                var query = new QueryExpression("salesorderdetail")
                {
                    ColumnSet = new ColumnSet("productid", "quantity"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("salesorderid", ConditionOperator.Equal, salesOrderId)
                        }
                    }
                };

                var result = _serviceClient.RetrieveMultiple(query);
                foreach (var entity in result.Entities)
                {
                    var productRef = entity.GetAttributeValue<EntityReference>("productid");
                    if (productRef == null) continue;

                    var line = new SalesOrderLine
                    {
                        ProductId = productRef.Id,
                        ProductName = productRef.Name,
                        Quantity = entity.GetAttributeValue<decimal?>("quantity") ?? 0m
                    };

                    lines.Add(line);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur lors de la récupération des lignes de commande : " + ex.Message);
            }

            return lines;
        }

        private string GenerateSavName(Guid contactId, string clientDescription)
        {
            if (!IsConnected) return clientDescription ?? "Demande SAV";
            
            try
            {
                // Récupérer le nom du client
                string clientName = "Client";
                try
                {
                    var contact = _serviceClient.Retrieve("contact", contactId, new ColumnSet("firstname", "lastname"));
                    var firstName = contact.GetAttributeValue<string>("firstname") ?? "";
                    var lastName = contact.GetAttributeValue<string>("lastname") ?? "";
                    clientName = $"{firstName} {lastName}".Trim();
                    if (string.IsNullOrWhiteSpace(clientName))
                        clientName = "Client";
                }
                catch
                {
                    // Si on ne peut pas récupérer le nom, on utilise "Client"
                }

                // Compter les demandes SAV existantes pour ce client
                int savCount = 0;
                try
                {
                    var query = new QueryExpression("crda6_sav")
                    {
                        ColumnSet = new ColumnSet("crda6_name"),
                        Criteria = new FilterExpression
                        {
                            Conditions =
                            {
                                new ConditionExpression("crda6_clients", ConditionOperator.Equal, contactId)
                            }
                        }
                    };

                    var result = _serviceClient.RetrieveMultiple(query);
                    savCount = result.Entities.Count;
                }
                catch
                {
                    // Si erreur, on considère qu'il n'y a pas de demandes existantes
                }

                // Générer le numéro SAV incrémenté
                string savNumber = $"SAV{(savCount + 1):D2}";
                
                // Nettoyer la description du client
                string cleanDescription = (clientDescription ?? "").Trim();
                if (string.IsNullOrWhiteSpace(cleanDescription))
                    cleanDescription = "Demande SAV";

                // Assembler le nom final
                string finalName = $"{clientName} - {savNumber} - {cleanDescription}";
                
                // Limiter la longueur si nécessaire (max 100 caractères pour les champs Dataverse)
                if (finalName.Length > 100)
                {
                    cleanDescription = cleanDescription.Length > 30 ? cleanDescription.Substring(0, 27) + "..." : cleanDescription;
                    finalName = $"{clientName} - {savNumber} - {cleanDescription}";
                    if (finalName.Length > 100)
                    {
                        clientName = clientName.Length > 20 ? clientName.Substring(0, 17) + "..." : clientName;
                        finalName = $"{clientName} - {savNumber} - {cleanDescription}";
                    }
                }

                return finalName;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur génération nom SAV: {ex.Message}");
                return clientDescription ?? "Demande SAV";
            }
        }

        public (Guid? SavId, string ErrorMessage) CreateSavRequest(
            Guid contactId,
            Guid productId,
            string name,
            string description,
            DateTime? purchaseDate,
            int? diagnostic = null,
            byte[] photoData = null,
            string photoFileName = null)
        {
            if (!IsConnected) return (null, "Connexion Dataverse indisponible.");
            if (contactId == Guid.Empty) return (null, "Client invalide.");
            if (productId == Guid.Empty) return (null, "Produit invalide.");

            name = (name ?? string.Empty).Trim();
            description = (description ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(name)) return (null, "Le nom est obligatoire.");

            try
            {
                // Générer automatiquement le nom de la demande SAV
                string autoGeneratedName = GenerateSavName(contactId, name);
                
                var sav = new Entity("crda6_sav")
                {
                    ["crda6_name"] = autoGeneratedName,
                    ["crda6_clients"] = new EntityReference("contact", contactId),
                    ["crda6_produitconcerne"] = new EntityReference("product", productId)
                };

                if (!string.IsNullOrWhiteSpace(description))
                {
                    sav["crda6_description"] = description;
                }

                if (purchaseDate.HasValue)
                {
                    sav["crda6_datedachat"] = purchaseDate.Value.Date;
                }

                if (diagnostic.HasValue)
                {
                    sav["crda6_diagnostic"] = new OptionSetValue(diagnostic.Value);
                }

                if (photoData != null && !string.IsNullOrEmpty(photoFileName))
                {
                    try
                    {
                        // Méthode 1: Essayer direct sur l'entité
                        sav["crda6_photo"] = photoData;
                        Console.WriteLine($"Photo ajoutée directement - Taille: {photoData.Length} bytes, Nom: {photoFileName}");
                    }
                    catch (Exception photoEx)
                    {
                        Console.WriteLine($"Erreur ajout direct photo: {photoEx.Message}");
                        // La photo sera ajoutée via annotation après la création de l'entité
                    }
                }

                var savId = _serviceClient.Create(sav);
                if (savId == Guid.Empty) return (null, "Création de la demande SAV impossible.");

                // Créer l'annotation (pièce jointe) après la création de l'entité principale
                if (photoData != null && !string.IsNullOrEmpty(photoFileName))
                {
                    try
                    {
                        var annotation = new Entity("annotation")
                        {
                            ["objectid"] = new EntityReference("crda6_sav", savId),
                            ["objecttypecode"] = 10001, // Code numérique pour entité personnalisée
                            ["objectidtypecode"] = 10001, // Doit être identique à objecttypecode
                            ["subject"] = "Photo SAV",
                            ["filename"] = photoFileName,
                            ["mimetype"] = GetMimeType(photoFileName),
                            ["documentbody"] = Convert.ToBase64String(photoData),
                            ["notetext"] = "Photo jointe à la demande SAV"
                        };
                        
                        var annotationId = _serviceClient.Create(annotation);
                        Console.WriteLine($"Annotation créée avec succès - ID: {annotationId}");
                    }
                    catch (Exception annotationEx)
                    {
                        Console.WriteLine($"Erreur création annotation: {annotationEx.Message}");
                        // Ne pas échouer la création SAV si l'annotation échoue
                    }
                }

                return (savId, string.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur lors de la création de la demande SAV : " + ex.Message);
                return (null, ex.Message);
            }
        }

        public List<SavRequest> GetSavRequestsForContact(Guid contactId)
        {
            var items = new List<SavRequest>();
            if (!IsConnected) return items;
            if (contactId == Guid.Empty) return items;

            try
            {
                var query = new QueryExpression("crda6_sav")
                {
                    ColumnSet = new ColumnSet(
                        "crda6_name",
                        "crda6_clients",
                        "crda6_produitconcerne",
                        "crda6_description",
                        "crda6_datedachat",
                        "createdon"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("crda6_clients", ConditionOperator.Equal, contactId)
                        }
                    },
                    Orders =
                    {
                        new OrderExpression("createdon", OrderType.Descending)
                    }
                };

                var result = _serviceClient.RetrieveMultiple(query);
                foreach (var entity in result.Entities)
                {
                    var productRef = entity.GetAttributeValue<EntityReference>("crda6_produitconcerne");
                    string productName = productRef?.Name ?? "Non spécifié";
                    string productNumber = "";

                    if (productRef != null)
                    {
                        // Récupérer les détails du produit
                        var productQuery = new QueryExpression("product")
                        {
                            ColumnSet = new ColumnSet("productnumber"),
                            Criteria = new FilterExpression
                            {
                                Conditions = { new ConditionExpression("productid", ConditionOperator.Equal, productRef.Id) }
                            }
                        };

                        var productResult = _serviceClient.RetrieveMultiple(productQuery);
                        if (productResult.Entities.Count > 0)
                        {
                            var productEntity = productResult.Entities[0];
                            productNumber = productEntity.GetAttributeValue<string>("productnumber") ?? "";
                        }
                    }

                    items.Add(new SavRequest
                    {
                        Id = entity.Id,
                        Name = entity.GetAttributeValue<string>("crda6_name") ?? string.Empty,
                        ProductName = productName,
                        ProductNumber = productNumber,
                        PurchaseDate = entity.GetAttributeValue<DateTime?>("crda6_datedachat"),
                        Description = entity.GetAttributeValue<string>("crda6_description") ?? string.Empty,
                        CreatedOn = entity.GetAttributeValue<DateTime?>("createdon"),
                        ProductId = productRef != null ? productRef.Id : Guid.Empty
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur lors de la récupération des demandes SAV : " + ex.Message);
            }

            return items;
        }

        public List<CurrencyOption> GetCurrencies()
        {
            var currencies = new List<CurrencyOption>();
            if (!IsConnected) return currencies;

            try
            {
                var query = new QueryExpression("transactioncurrency")
                {
                    ColumnSet = new ColumnSet("isocurrencycode", "currencyname")
                };

                var result = _serviceClient.RetrieveMultiple(query);
                foreach (var entity in result.Entities)
                {
                    currencies.Add(new CurrencyOption
                    {
                        Id = entity.Id,
                        Name = entity.GetAttributeValue<string>("currencyname") ?? "Inconnue",
                        IsoCode = entity.GetAttributeValue<string>("isocurrencycode") ?? "N/A"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur lors de la récupération des devises : " + ex.Message);
            }

            return currencies;
        }

        public SiteWebContent GetSiteWebContent()
        {
            if (!IsConnected) return new SiteWebContent();

            try
            {
                var query = new QueryExpression("crda6_siteweb")
                {
                    ColumnSet = new ColumnSet(
                        //"crda6_title", "crda6_content", "createdon",
                        // More page fields
                        "crda6_more_badge_fr", "crda6_more_badge_en",
                        "crda6_more_hero_title_fr", "crda6_more_hero_title_en",
                        "crda6_more_hero_subtitle_fr", "crda6_more_hero_subtitle_en",
                        "crda6_more_hero_image",
                        "crda6_more_story_title_fr", "crda6_more_story_title_en",
                        "crda6_more_story_text_fr", "crda6_more_story_text_en",
                        "crda6_more_values_title_fr", "crda6_more_values_title_en",
                        "crda6_more_values_text_fr", "crda6_more_values_text_en",
                        "crda6_more_team_title_fr", "crda6_more_team_title_en",
                        "crda6_more_team_text_fr", "crda6_more_team_text_en",
                        "crda6_more_next_title_fr", "crda6_more_next_title_en",
                        "crda6_more_next_text_fr", "crda6_more_next_text_en",
                        // News page fields
                        "crda6_news_badge_fr", "crda6_news_badge_en",
                        "crda6_news_hero_title_fr", "crda6_news_hero_title_en",
                        "crda6_news_hero_subtitle_fr", "crda6_news_hero_subtitle_en",
                        "crda6_news_hero_image",
                        "crda6_news_article1_image",
                        "crda6_news_article1_title_fr", "crda6_news_article1_title_en",
                        "crda6_news_article1_text_fr", "crda6_news_article1_text_en",
                        "crda6_news_article1_date",
                        "crda6_news_article1_readtime_fr", "crda6_news_article1_readtime_en",
                        "crda6_news_article2_image",
                        "crda6_news_article2_title_fr", "crda6_news_article2_title_en",
                        "crda6_news_article2_text_fr", "crda6_news_article2_text_en"
                    ),
                    TopCount = 1
                };

                var result = _serviceClient.RetrieveMultiple(query);
                if (result.Entities.Count > 0)
                {
                    var entity = result.Entities[0];
                    return new SiteWebContent
                    {
                        Title = entity.GetAttributeValue<string>("crda6_title") ?? "Contenu non disponible",
                        Content = entity.GetAttributeValue<string>("crda6_content") ?? "Le contenu est en cours de chargement.",
                        CreatedOn = entity.GetAttributeValue<DateTime?>("createdon"),

                        // More page properties
                        MoreBadgeFr = entity.GetAttributeValue<string>("crda6_more_badge_fr") ?? "À propos",
                        MoreBadgeEn = entity.GetAttributeValue<string>("crda6_more_badge_en") ?? "About",
                        MoreHeroTitleFr = entity.GetAttributeValue<string>("crda6_more_hero_title_fr") ?? "Découvrez notre histoire",
                        MoreHeroTitleEn = entity.GetAttributeValue<string>("crda6_more_hero_title_en") ?? "Discover our story",
                        MoreHeroSubtitleFr = entity.GetAttributeValue<string>("crda6_more_hero_subtitle_fr") ?? "Plus de 10 ans d'expertise à votre service",
                        MoreHeroSubtitleEn = entity.GetAttributeValue<string>("crda6_more_hero_subtitle_en") ?? "Over 10 years of expertise at your service",
                        MoreHeroImageUrl = entity.GetAttributeValue<string>("crda6_more_hero_image"),
                        MoreStoryTitleFr = entity.GetAttributeValue<string>("crda6_more_story_title_fr") ?? "Notre histoire",
                        MoreStoryTitleEn = entity.GetAttributeValue<string>("crda6_more_story_title_en") ?? "Our story",
                        MoreStoryTextFr = entity.GetAttributeValue<string>("crda6_more_story_text_fr") ?? "Depuis notre création, nous nous engageons à fournir les meilleures solutions.",
                        MoreStoryTextEn = entity.GetAttributeValue<string>("crda6_more_story_text_en") ?? "Since our creation, we have been committed to providing the best solutions.",
                        MoreValuesTitleFr = entity.GetAttributeValue<string>("crda6_more_values_title_fr") ?? "Nos valeurs",
                        MoreValuesTitleEn = entity.GetAttributeValue<string>("crda6_more_values_title_en") ?? "Our values",
                        MoreValuesTextFr = entity.GetAttributeValue<string>("crda6_more_values_text_fr") ?? "Innovation, qualité et satisfaction client sont nos piliers.",
                        MoreValuesTextEn = entity.GetAttributeValue<string>("crda6_more_values_text_en") ?? "Innovation, quality and customer satisfaction are our pillars.",
                        MoreTeamTitleFr = entity.GetAttributeValue<string>("crda6_more_team_title_fr") ?? "Notre équipe",
                        MoreTeamTitleEn = entity.GetAttributeValue<string>("crda6_more_team_title_en") ?? "Our team",
                        MoreTeamTextFr = entity.GetAttributeValue<string>("crda6_more_team_text_fr") ?? "Des experts passionnés à votre écoute.",
                        MoreTeamTextEn = entity.GetAttributeValue<string>("crda6_more_team_text_en") ?? "Passionate experts listening to you.",
                        MoreNextTitleFr = entity.GetAttributeValue<string>("crda6_more_next_title_fr") ?? "Prochaines étapes",
                        MoreNextTitleEn = entity.GetAttributeValue<string>("crda6_more_next_title_en") ?? "Next steps",
                        MoreNextTextFr = entity.GetAttributeValue<string>("crda6_more_next_text_fr") ?? "Contactez-nous pour discuter de vos projets.",
                        MoreNextTextEn = entity.GetAttributeValue<string>("crda6_more_next_text_en") ?? "Contact us to discuss your projects.",

                        // News page properties
                        NewsBadgeFr = entity.GetAttributeValue<string>("crda6_news_badge_fr") ?? "Actualités",
                        NewsBadgeEn = entity.GetAttributeValue<string>("crda6_news_badge_en") ?? "News",
                        NewsHeroTitleFr = entity.GetAttributeValue<string>("crda6_news_hero_title_fr") ?? "Dernières actualités",
                        NewsHeroTitleEn = entity.GetAttributeValue<string>("crda6_news_hero_title_en") ?? "Latest news",
                        NewsHeroSubtitleFr = entity.GetAttributeValue<string>("crda6_news_hero_subtitle_fr") ?? "Restez informé de nos dernières nouveautés",
                        NewsHeroSubtitleEn = entity.GetAttributeValue<string>("crda6_news_hero_subtitle_en") ?? "Stay informed about our latest news",
                        NewsHeroImageUrl = entity.GetAttributeValue<string>("crda6_news_hero_image"),
                        NewsArticle1ImageUrl = entity.GetAttributeValue<string>("crda6_news_article1_image"),
                        NewsArticle1TitleFr = entity.GetAttributeValue<string>("crda6_news_article1_title_fr") ?? "Nouveau lancement",
                        NewsArticle1TitleEn = entity.GetAttributeValue<string>("crda6_news_article1_title_en") ?? "New launch",
                        NewsArticle1TextFr = entity.GetAttributeValue<string>("crda6_news_article1_text_fr") ?? "Découvrez nos dernières innovations.",
                        NewsArticle1TextEn = entity.GetAttributeValue<string>("crda6_news_article1_text_en") ?? "Discover our latest innovations.",
                        NewsArticle1Date = entity.GetAttributeValue<DateTime?>("crda6_news_article1_date")?.ToString("dd MMMM yyyy") ?? DateTime.Now.ToString("dd MMMM yyyy"),
                        NewsArticle1ReadTimeFr = entity.GetAttributeValue<string>("crda6_news_article1_readtime_fr") ?? "",
                        NewsArticle1ReadTimeEn = entity.GetAttributeValue<string>("crda6_news_article1_readtime_en") ?? "",
                        NewsArticle2ImageUrl = entity.GetAttributeValue<string>("crda6_news_article2_image"),
                        NewsArticle2TitleFr = entity.GetAttributeValue<string>("crda6_news_article2_title_fr") ?? "Événement à venir",
                        NewsArticle2TitleEn = entity.GetAttributeValue<string>("crda6_news_article2_title_en") ?? "Upcoming event",
                        NewsArticle2TextFr = entity.GetAttributeValue<string>("crda6_news_article2_text_fr") ?? "Rejoignez-nous pour notre prochain événement.",
                        NewsArticle2TextEn = entity.GetAttributeValue<string>("crda6_news_article2_text_en") ?? "Join us for our next event."
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur lors de la récupération du contenu site web : " + ex.Message);
            }

            // Return default content if nothing found or error
            return new SiteWebContent
            {
                Title = "Contenu par défaut",
                Content = "Le contenu est en cours de chargement.",

                // More page defaults
                MoreBadgeFr = "À propos",
                MoreBadgeEn = "About",
                MoreHeroTitleFr = "Découvrez notre histoire",
                MoreHeroTitleEn = "Discover our story",
                MoreHeroSubtitleFr = "Plus de 10 ans d'expertise à votre service",
                MoreHeroSubtitleEn = "Over 10 years of expertise at your service",
                MoreStoryTitleFr = "Notre histoire",
                MoreStoryTitleEn = "Our story",
                MoreStoryTextFr = "Depuis notre création, nous nous engageons à fournir les meilleures solutions.",
                MoreStoryTextEn = "Since our creation, we have been committed to providing the best solutions.",
                MoreValuesTitleFr = "Nos valeurs",
                MoreValuesTitleEn = "Our values",
                MoreValuesTextFr = "Innovation, qualité et satisfaction client sont nos piliers.",
                MoreValuesTextEn = "Innovation, quality and customer satisfaction are our pillars.",
                MoreTeamTitleFr = "Notre équipe",
                MoreTeamTitleEn = "Our team",
                MoreTeamTextFr = "Des experts passionnés à votre écoute.",
                MoreTeamTextEn = "Passionate experts listening to you.",
                MoreNextTitleFr = "Prochaines étapes",
                MoreNextTitleEn = "Next steps",
                MoreNextTextFr = "Contactez-nous pour discuter de vos projets.",
                MoreNextTextEn = "Contact us to discuss your projects.",

                // News page defaults
                NewsBadgeFr = "Actualités",
                NewsBadgeEn = "News",
                NewsHeroTitleFr = "Dernières actualités",
                NewsHeroTitleEn = "Latest news",
                NewsHeroSubtitleFr = "Restez informé de nos dernières nouveautés",
                NewsHeroSubtitleEn = "Stay informed about our latest news",
                NewsArticle1TitleFr = "Nouveau lancement",
                NewsArticle1TitleEn = "New launch",
                NewsArticle1TextFr = "Découvrez nos dernières innovations.",
                NewsArticle1TextEn = "Discover our latest innovations.",
                NewsArticle1Date = DateTime.Now.ToString("dd MMMM yyyy"),
                NewsArticle1ReadTimeFr = "",
                NewsArticle1ReadTimeEn = "",
                NewsArticle2TitleFr = "Événement à venir",
                NewsArticle2TitleEn = "Upcoming event",
                NewsArticle2TextFr = "Rejoignez-nous pour notre prochain événement.",
                NewsArticle2TextEn = "Join us for our next event."
            };
        }

        public List<Avis> GetAvisByProduct(Guid productId)
        {
            var avisList = new List<Avis>();
            if (!IsConnected || productId == Guid.Empty) return avisList;

            try
            {
                var query = new QueryExpression("crda6_avis")
                {
                    ColumnSet = new ColumnSet("crda6_name", "crda6_description", "crda6_note", "createdon", "createdby"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("crda6_produit", ConditionOperator.Equal, productId)
                        }
                    },
                    Orders =
                    {
                        new OrderExpression("createdon", OrderType.Descending)
                    }
                };

                var result = _serviceClient.RetrieveMultiple(query);
                foreach (var entity in result.Entities)
                {
                    var createdByRef = entity.GetAttributeValue<EntityReference>("createdby");
                    avisList.Add(new Avis
                    {
                        Id = entity.Id,
                        Title = entity.GetAttributeValue<string>("crda6_name") ?? "Sans titre",
                        Description = entity.GetAttributeValue<string>("crda6_description") ?? "Sans description",
                        Note = entity.GetAttributeValue<int?>("crda6_note"),
                        CreatedOn = entity.GetAttributeValue<DateTime?>("createdon"),
                        CreatedByName = createdByRef?.Name ?? "Anonyme"
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur lors de la récupération des avis : " + ex.Message);
            }

            return avisList;
        }

        public (Guid? AvisId, string ErrorMessage) CreateAvis(
            Guid contactId,
            Guid productId,
            string title,
            string description,
            int note)
        {
            if (!IsConnected) return (null, "Connexion Dataverse indisponible.");
            if (contactId == Guid.Empty) return (null, "Client invalide.");
            if (productId == Guid.Empty) return (null, "Produit invalide.");

            title = (title ?? string.Empty).Trim();
            description = (description ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(title)) return (null, "Le titre est obligatoire.");

            try
            {
                var avis = new Entity("crda6_avis")
                {
                    ["crda6_name"] = title,
                    ["crda6_client"] = new EntityReference("contact", contactId),
                    ["crda6_produit"] = new EntityReference("product", productId),
                    ["crda6_note"] = note
                };

                if (!string.IsNullOrWhiteSpace(description))
                {
                    avis["crda6_description"] = description;
                }

                var avisId = _serviceClient.Create(avis);
                if (avisId == Guid.Empty) return (null, "Création de l'avis impossible.");
                return (avisId, string.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur lors de la création de l'avis : " + ex.Message);
                return (null, ex.Message);
            }
        }

        public List<SalesOrder> GetInvoicesForContact(Guid contactId)
        {
            var invoices = new List<SalesOrder>();
            if (!IsConnected) return invoices;
            if (contactId == Guid.Empty) return invoices;

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

                // Requête pour les factures depuis l'entité invoice
                var query = new QueryExpression("invoice")
                {
                    ColumnSet = new ColumnSet("name", "invoicenumber", "customerid", "statecode", "statuscode", "totalamount", "createdon", "salesorderid"),
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
                    // Récupérer les informations du produit depuis la commande associée
                    string productName = "Non spécifié";
                    string productNumber = "";
                    string quoteNumber = "";
                    string orderNumber = "";
                    EntityReference productRef = null;

                    // Récupérer le numéro de commande associé
                    if (entity.Contains("salesorderid"))
                    {
                        var orderRef = entity.GetAttributeValue<EntityReference>("salesorderid");
                        if (orderRef != null)
                        {
                            // Récupérer les détails de la commande
                            var orderQuery = new QueryExpression("salesorder")
                            {
                                ColumnSet = new ColumnSet("ordernumber", "quoteid"),
                                Criteria = new FilterExpression
                                {
                                    Conditions = { new ConditionExpression("salesorderid", ConditionOperator.Equal, orderRef.Id) }
                                }
                            };

                            var orderResult = _serviceClient.RetrieveMultiple(orderQuery);
                            if (orderResult.Entities.Count > 0)
                            {
                                var orderEntity = orderResult.Entities[0];
                                orderNumber = orderEntity.GetAttributeValue<string>("ordernumber") ?? "";

                                // Récupérer le numéro de devis associé à la commande
                                if (orderEntity.Contains("quoteid"))
                                {
                                    var quoteRef = orderEntity.GetAttributeValue<EntityReference>("quoteid");
                                    if (quoteRef != null)
                                    {
                                        var quoteQuery = new QueryExpression("quote")
                                        {
                                            ColumnSet = new ColumnSet("quotenumber"),
                                            Criteria = new FilterExpression
                                            {
                                                Conditions = { new ConditionExpression("quoteid", ConditionOperator.Equal, quoteRef.Id) }
                                            }
                                        };

                                        var quoteResult = _serviceClient.RetrieveMultiple(quoteQuery);
                                        if (quoteResult.Entities.Count > 0)
                                        {
                                            quoteNumber = quoteResult.Entities[0].GetAttributeValue<string>("quotenumber") ?? "";
                                        }
                                    }
                                }
                            }

                            // Récupérer les informations du produit depuis les lignes de commande
                            var detailQuery = new QueryExpression("salesorderdetail")
                            {
                                ColumnSet = new ColumnSet("productid"),
                                Criteria = new FilterExpression
                                {
                                    Conditions = { new ConditionExpression("salesorderid", ConditionOperator.Equal, orderRef.Id) }
                                },
                                TopCount = 1
                            };

                            var detailResult = _serviceClient.RetrieveMultiple(detailQuery);
                            if (detailResult.Entities.Count > 0)
                            {
                                EntityReference outerProductRef = detailResult.Entities[0].GetAttributeValue<EntityReference>("productid");
                                if (outerProductRef != null && outerProductRef.Id != Guid.Empty)
                                {
                                    productRef = outerProductRef;
                                    // Récupérer les détails du produit
                                    var productQuery = new QueryExpression("product")
                                    {
                                        ColumnSet = new ColumnSet("name", "productnumber"),
                                        Criteria = new FilterExpression
                                        {
                                            Conditions = { new ConditionExpression("productid", ConditionOperator.Equal, productRef.Id) }
                                        }
                                    };

                                    var productResult = _serviceClient.RetrieveMultiple(productQuery);
                                    if (productResult.Entities.Count > 0)
                                    {
                                        var productEntity = productResult.Entities[0];
                                        productName = productEntity.GetAttributeValue<string>("name") ?? "Non spécifié";
                                        productNumber = productEntity.GetAttributeValue<string>("productnumber") ?? "";
                                    }
                                }
                            }
                        }
                    }

                    var invoice = new SalesOrder
                    {
                        Id = entity.Id,
                        Name = entity.GetAttributeValue<string>("name"),
                        OrderNumber = orderNumber,
                        InvoiceNumber = entity.GetAttributeValue<string>("invoicenumber"),
                        QuoteNumber = quoteNumber,
                        CustomerId = entity.Contains("customerid")
                            ? entity.GetAttributeValue<EntityReference>("customerid").Name
                            : "Client inconnu",
                        PriceLevelId = "Non défini",
                        StateCode = entity.Contains("statecode")
                            ? entity.FormattedValues["statecode"]
                            : "Inconnu",
                        StatusCode = entity.GetAttributeValue<OptionSetValue>("statuscode")?.Value,
                        TotalAmount = entity.Contains("totalamount")
                            ? entity.GetAttributeValue<Money>("totalamount").Value
                            : 0m,
                        CreatedOn = entity.Contains("createdon")
                            ? entity.GetAttributeValue<DateTime>("createdon")
                            : DateTime.MinValue,
                        ProductName = productName,
                        ProductNumber = productNumber,
                        ProductId = productRef != null ? productRef.Id : Guid.Empty
                    };

                    invoices.Add(invoice);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur lors de la récupération des factures : " + ex.Message);
            }

            return invoices;
        }

        public (Guid? OpportunityId, string ErrorMessage) CreateOpportunityWithProduct(
            Guid contactId,
            Guid productId,
            Guid currencyId,
            string rubrique,
            string description)
        {
            if (!IsConnected) return (null, "Connexion Dataverse indisponible.");
            if (contactId == Guid.Empty) return (null, "Client invalide.");
            if (productId == Guid.Empty) return (null, "Produit invalide.");

            rubrique = (rubrique ?? string.Empty).Trim();
            description = (description ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(rubrique)) return (null, "La rubrique est obligatoire.");

            try
            {
                var opportunity = new Entity("opportunity")
                {
                    ["name"] = rubrique,
                    ["parentcontactid"] = new EntityReference("contact", contactId),
                    ["transactioncurrencyid"] = new EntityReference("transactioncurrency", currencyId),
                    ["description"] = description
                };

                var opportunityId = _serviceClient.Create(opportunity);
                if (opportunityId == Guid.Empty) return (null, "Création de l'opportunité impossible.");

                // Ajouter le produit à l'opportunité
                try
                {
                    var defaultUom = GetDefaultUom();
                    if (defaultUom != null)
                    {
                        // Récupérer le prix du produit
                        var productQuery = new QueryExpression("product")
                        {
                            ColumnSet = new ColumnSet("price", "defaultuomid"),
                            Criteria = new FilterExpression
                            {
                                Conditions = { new ConditionExpression("productid", ConditionOperator.Equal, productId) }
                            }
                        };

                        var productResult = _serviceClient.RetrieveMultiple(productQuery);
                        if (productResult.Entities.Count > 0)
                        {
                            var productEntity = productResult.Entities[0];
                            var productPrice = productEntity.GetAttributeValue<Money>("price")?.Value ?? 0m;
                            var productUomId = productEntity.GetAttributeValue<EntityReference>("defaultuomid")?.Id;
                            
                            var opportunityProduct = new Entity("opportunityproduct")
                            {
                                ["opportunityid"] = new EntityReference("opportunity", opportunityId),
                                ["productid"] = new EntityReference("product", productId),
                                ["uomid"] = new EntityReference("uom", productUomId ?? defaultUom.Id),
                                ["quantity"] = 1m,
                                ["priceperunit"] = new Money(productPrice),
                                ["ispriceoverridden"] = false
                            };
                            
                            _serviceClient.Create(opportunityProduct);
                            Console.WriteLine($"Produit ajouté à l'opportunité avec succès. Prix: {productPrice}");
                        }
                        else
                        {
                            Console.WriteLine("Produit non trouvé pour l'ajout à l'opportunité.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Aucune unité de mesure trouvée pour ajouter le produit à l'opportunité.");
                    }
                }
                catch (Exception productEx)
                {
                    Console.WriteLine("Erreur lors de l'ajout du produit à l'opportunité : " + productEx.Message);
                    // Ne pas échouer si l'ajout du produit échoue
                }

                return (opportunityId, string.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur lors de la création de l'opportunité : " + ex.Message);
                return (null, ex.Message);
            }
        }

        public List<Opportunity> GetOpportunitiesForContact(Guid contactId)
        {
            var opportunities = new List<Opportunity>();
            if (!IsConnected) return opportunities;
            if (contactId == Guid.Empty) return opportunities;

            try
            {
                var query = new QueryExpression("opportunity")
                {
                    ColumnSet = new ColumnSet("name", "statecode", "statuscode", "createdon", "estimatedvalue"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("parentcontactid", ConditionOperator.Equal, contactId)
                        }
                    },
                    Orders =
                    {
                        new OrderExpression("createdon", OrderType.Descending)
                    }
                };

                var result = _serviceClient.RetrieveMultiple(query);
                foreach (var entity in result.Entities)
                {
                    opportunities.Add(new Opportunity
                    {
                        Id = entity.Id,
                        Name = entity.GetAttributeValue<string>("name") ?? "Sans nom",
                        StateCode = entity.Contains("statecode") ? entity.FormattedValues["statecode"] : "Inconnu",
                        StatusCode = entity.GetAttributeValue<OptionSetValue>("statuscode")?.Value,
                        CreatedOn = entity.GetAttributeValue<DateTime>("createdon"),
                        EstimatedValue = entity.GetAttributeValue<Money>("estimatedvalue")?.Value ?? 0m
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur lors de la récupération des opportunités : " + ex.Message);
            }

            return opportunities;
        }

        public bool HasUserReviewedProduct(Guid contactId, Guid productId)
        {
            if (!IsConnected || contactId == Guid.Empty || productId == Guid.Empty)
                return false;

            try
            {
                var query = new QueryExpression("crda6_avis")
                {
                    ColumnSet = new ColumnSet("crda6_avisid"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression("crda6_client", ConditionOperator.Equal, contactId),
                            new ConditionExpression("crda6_produit", ConditionOperator.Equal, productId)
                        }
                    },
                    TopCount = 1
                };

                var result = _serviceClient.RetrieveMultiple(query);
                return result.Entities.Count > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la vérification de l'existence d'un avis : {ex.Message}");
                return false;
            }
        }

        private EntityReference GetDefaultUom()
        {
            try
            {
                // Récupérer la première unité de mesure disponible
                var uomQuery = new QueryExpression("uom")
                {
                    ColumnSet = new ColumnSet("uomid", "name"),
                    TopCount = 1
                };

                var uomResult = _serviceClient.RetrieveMultiple(uomQuery);
                if (uomResult.Entities.Count > 0)
                {
                    return new EntityReference("uom", uomResult.Entities[0].Id);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur lors de la récupération de l'unité de mesure : " + ex.Message);
            }
            
            return null; // Retourner null si aucune unité trouvée
        }

        private string GetMimeType(string fileName)
        {
            var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }

        public class SalesOrder
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string OrderNumber { get; set; }
            public string InvoiceNumber { get; set; }
            public string QuoteNumber { get; set; }
            public string CustomerId { get; set; }
            public string PriceLevelId { get; set; }
            public string StateCode { get; set; }
            public int? StatusCode { get; set; }
            public decimal TotalAmount { get; set; }
            public DateTime CreatedOn { get; set; }
            public List<SalesOrderLine> Lines { get; set; } = new();
            public string ProductName { get; set; }
            public string ProductNumber { get; set; }
            public Guid ProductId { get; set; }
        }

        public class SalesOrderLine
        {
            public Guid ProductId { get; set; }
            public string ProductName { get; set; }
            public decimal Quantity { get; set; }
        }

        public class SavRequest
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string ProductName { get; set; }
            public string ProductNumber { get; set; }
            public DateTime? PurchaseDate { get; set; }
            public string Description { get; set; }
            public DateTime? CreatedOn { get; set; }
            public Guid ProductId { get; set; }
        }

        public class CurrencyOption
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string IsoCode { get; set; }
        }

        public class Opportunity
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public string StateCode { get; set; }
            public int? StatusCode { get; set; }
            public DateTime CreatedOn { get; set; }
            public decimal EstimatedValue { get; set; }
        }

        public class Avis
        {
            public Guid Id { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public int? Note { get; set; }
            public DateTime? CreatedOn { get; set; }
            public string CreatedByName { get; set; }
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

        public class Quote
        {
            public string Name { get; set; }
            public string QuoteNumber { get; set; }
            public decimal TotalAmount { get; set; }
            public string CustomerId { get; set; }
            public string PriceLevelId { get; set; }
            public string StateCode { get; set; }
            public int? StatusCode { get; set; }
            public DateTime CreatedOn { get; set; }
            public string ProductName { get; set; }
            public string ProductNumber { get; set; }
            public Guid ProductId { get; set; }
        }

        public class SiteWebContent
        {
            public string Title { get; set; }
            public string Content { get; set; }
            public DateTime? CreatedOn { get; set; }

            // More page properties
            public string MoreBadgeFr { get; set; }
            public string MoreBadgeEn { get; set; }
            public string MoreHeroTitleFr { get; set; }
            public string MoreHeroTitleEn { get; set; }
            public string MoreHeroSubtitleFr { get; set; }
            public string MoreHeroSubtitleEn { get; set; }
            public string MoreHeroImageUrl { get; set; }
            public string MoreStoryTitleFr { get; set; }
            public string MoreStoryTitleEn { get; set; }
            public string MoreStoryTextFr { get; set; }
            public string MoreStoryTextEn { get; set; }
            public string MoreValuesTitleFr { get; set; }
            public string MoreValuesTitleEn { get; set; }
            public string MoreValuesTextFr { get; set; }
            public string MoreValuesTextEn { get; set; }
            public string MoreTeamTitleFr { get; set; }
            public string MoreTeamTitleEn { get; set; }
            public string MoreTeamTextFr { get; set; }
            public string MoreTeamTextEn { get; set; }
            public string MoreNextTitleFr { get; set; }
            public string MoreNextTitleEn { get; set; }
            public string MoreNextTextFr { get; set; }
            public string MoreNextTextEn { get; set; }

            // News page properties
            public string NewsBadgeFr { get; set; }
            public string NewsBadgeEn { get; set; }
            public string NewsHeroTitleFr { get; set; }
            public string NewsHeroTitleEn { get; set; }
            public string NewsHeroSubtitleFr { get; set; }
            public string NewsHeroSubtitleEn { get; set; }
            public string NewsHeroImageUrl { get; set; }
            public string NewsArticle1ImageUrl { get; set; }
            public string NewsArticle1TitleFr { get; set; }
            public string NewsArticle1TitleEn { get; set; }
            public string NewsArticle1TextFr { get; set; }
            public string NewsArticle1TextEn { get; set; }
            public string NewsArticle1Date { get; set; }
            public string NewsArticle1ReadTimeFr { get; set; }
            public string NewsArticle1ReadTimeEn { get; set; }
            public string NewsArticle2ImageUrl { get; set; }
            public string NewsArticle2TitleFr { get; set; }
            public string NewsArticle2TitleEn { get; set; }
            public string NewsArticle2TextFr { get; set; }
            public string NewsArticle2TextEn { get; set; }
        }

        public class Contact
        {
            public Guid Id { get; set; }
            public string FullName { get; set; }
            public string Identifiant { get; set; }
        }
    }
}