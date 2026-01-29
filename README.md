# Documentation Complète - SAE Dynamics RGF

## Table des matières
1. [Présentation du projet](#présentation-du-projet)
2. [Architecture et technologies](#architecture-et-technologies)
3. [Structure des fichiers et dossiers](#structure-des-fichiers-et-dossiers)
4. [Conventions de nommage et commentaires](#conventions-de-nommage-et-commentaires)
5. [Fonctionnalités principales](#fonctionnalités-principales)
6. [Configuration et déploiement](#configuration-et-déploiement)
7. [Guide de développement](#guide-de-développement)

---

## Présentation du projet

### Vue d'ensemble
**SAE Dynamics RGF** est une application web de commerce électronique spécialisée dans la vente de montres de luxe sous la marque **ROLIX**. L'application est développée en ASP.NET Core 8.0 et utilise Microsoft Dataverse comme base de données principale.

### Objectifs métiers
- Présenter un catalogue de montres haut de gamme
- Gérer l'authentification et les comptes clients
- Permettre la consultation des produits avec détails et images
- Gérer les devis, commandes et factures
- Offrir un espace client personnalisé
- Gérer les demandes de service après-vente

---

## Architecture et technologies

### Stack technique principal

#### Backend
- **Framework**: ASP.NET Core 8.0 (.NET 8.0)
- **Architecture**: Razor Pages avec contrôleurs API
- **Langage**: C# 12.0
- **Base de données**: Microsoft Dataverse (Power Platform)
- **Authentification**: Session-based avec identifiants personnalisés

#### Frontend
- **Framework**: Razor Pages (server-side rendering)
- **CSS Framework**: Bootstrap 5.3.0
- **JavaScript**: Vanilla JS avec fonctionnalités modernes
- **Polices**: Google Fonts (Poppins)
- **Icônes**: Font Awesome 6.4.0

#### Infrastructure
- **Hébergement**: Configuré pour Azure/App Service
- **Connexion Dataverse**: OAuth avec Application ID
- **Gestion des assets**: Static files (wwwroot)

### Architecture applicative

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Frontend      │    │   Backend        │    │   Dataverse     │
│   (Razor Pages) │◄──►│   (ASP.NET Core) │◄──►│   (CRM/ERP)     │
│                 │    │                  │    │                 │
│ - UI/UX         │    │ - Controllers    │    │ - Contacts      │
│ - Sessions      │    │ - Services       │    │ - Products      │
│ - JavaScript    │    │ - Models         │    │ - Orders        │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

### Pattern architectural
- **Pattern**: MVC avec Razor Pages
- **Injection de dépendances**: Configuré dans Program.cs
- **Services singleton**: DataverseService
- **Gestion d'état**: Sessions ASP.NET Core
- **Séparation des responsabilités**: Controllers, Services, Models

---

## Structure des fichiers et dossiers

### Arborescence complète

```
SAE_Dynamics_RGF/
├── Controllers/                    # Contrôleurs API
│   └── ProductController.cs        # API pour les images produits
├── Data/                          # Services et accès aux données
│   └── DataverseService.cs        # Service principal Dataverse
├── Pages/                         # Pages Razor (UI)
│   ├── Index.cshtml               # Page d'accueil
│   ├── Login.cshtml               # Connexion/Inscription
│   ├── Products.cshtml            # Catalogue produits
│   ├── ProductDetail.cshtml       # Détail produit
│   ├── Profile.cshtml             # Espace client
│   ├── Invoices.cshtml            # Factures
│   ├── Quotes.cshtml              # Devis
│   ├── Salesorder.cshtml          # Commandes
│   ├── SavRequests.cshtml         # SAV
│   ├── News.cshtml                # Actualités
│   ├── More.cshtml                # Plus d'infos
│   ├── Privacy.cshtml             # Politique de confidentialité
│   ├── Requests.cshtml            # Demandes
│   ├── Error.cshtml               # Page d'erreur
│   ├── Logout.cshtml              # Déconnexion
│   ├── Shared/                    # Composants partagés
│   │   ├── _Layout.cshtml         # Layout principal
│   │   ├── _Layout.cshtml.css     # Styles du layout
│   │   └── _ValidationScriptsPartial.cshtml
│   ├── _ViewImports.cshtml        # Imports Razor
│   └── _ViewStart.cshtml          # Démarrage des vues
├── Properties/                    # Propriétés du projet
├── wwwroot/                       # Fichiers statiques
│   ├── css/
│   │   └── site.css               # Styles personnalisés
│   ├── js/
│   │   └── site.js                # JavaScript personnalisé
│   ├── images/                    # Images statiques
│   ├── lib/                       # Bibliothèques client
│   └── sounds/                    # Fichiers audio
├── Program.cs                     # Configuration et démarrage
├── SAE_Dynamics_RGF.csproj        # Fichier projet
├── appsettings.json               # Configuration
└── appsettings.Development.json   # Configuration dev
```

### Description des fichiers clés

#### Fichiers de configuration
- **`Program.cs`**: Point d'entrée, configuration des services, middleware et pipelines
- **`SAE_Dynamics_RGF.csproj`**: Dépendances NuGet et configuration du projet
- **`appsettings.json`**: Configuration production (URL Dataverse, AppId, etc.)
- **`appsettings.Development.json`**: Configuration spécifique au développement

#### Services principaux
- **`DataverseService.cs`**: Service central d'accès aux données Dataverse (2072 lignes)
  - Gestion des contacts (authentification, inscription)
  - Gestion des produits (catalogue, images, prix)
  - Gestion des devis, commandes, factures
  - Gestion des demandes SAV
  - Cache des devises et optimisations

#### Contrôleurs
- **`ProductController.cs`**: API REST pour les images produits
  - Endpoint: `/api/product/image/{productId}`
  - Gestion du cache HTTP
  - Images par défaut en cas d'erreur

#### Pages principales
- **`Index.cshtml`**: Page d'accueil avec carousel et présentation
- **`Login.cshtml`**: Authentification et inscription des clients
- **`Products.cshtml`**: Catalogue avec filtres et recherche
- **`ProductDetail.cshtml`**: Fiche produit détaillée avec avis
- **`Profile.cshtml`**: Espace client avec onglets (info, devis, commandes, factures, SAV)

---

## Conventions de nommage et commentaires

### Conventions de nommage

#### C# Code-behind
- **Classes**: PascalCase (ex: `IndexModel`, `ProductDetailModel`)
- **Propriétés**: PascalCase (ex: `FeaturedProducts`, `ProductNumber`)
- **Méthodes**: PascalCase (ex: `OnGet()`, `OnPostRegister()`)
- **Champs privés**: CamelCase avec underscore (ex: `_dataverseService`)
- **Constantes**: PascalCase (ex: `DataverseUrl`, `DataverseAppId`)

#### Entités Dataverse
- **Tables**: PascalCase (ex: `contact`, `product`, `invoice`)
- **Champs personnalisés**: préfixe `crda6_` (ex: `crda6_identifiant`, `crda6_motdepasse`)
- **Champs standards**: lowercase (ex: `firstname`, `lastname`, `emailaddress1`)

#### Frontend
- **Fichiers**: PascalCase pour les pages (ex: `ProductDetail.cshtml`)
- **Classes CSS**: kebab-case (ex: `hero-section`, `rolix-carousel`)
- **Variables JavaScript**: camelCase (ex: `featuredProducts`, `normalize`)
- **ID HTML**: kebab-case (ex: `featuredProductsCarousel`)

### Style de commentaires

#### Commentaires dans le code
```csharp
// ---------------------
// 🔧 Services
// ---------------------
builder.Services.AddRazorPages();

// ✅ Gestion des sessions
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // session valable 30 min
});

// ⚠️ Ordre important : session AVANT Razor Pages
app.UseSession();
```

#### Documentation XML
```csharp
/// <summary>
/// Récupère les bytes d'une image produit depuis Dataverse
/// </summary>
/// <param name="productId">ID du produit</param>
/// <returns>Bytes de l'image ou null si indisponible</returns>
public byte[] GetProductImageBytes(Guid productId)
```

#### Commentaires JavaScript
```javascript
// Fonction pour normaliser une chaîne (suppression des accents, passage en minuscules)
function normalize(str) {
    return (str || '')
        .toString()
        .normalize('NFD')
        .replace(/[\u0300-\u036f]/g, ''); // Supprime les accents
}
```

### Patterns de nommage spécifiques

#### Sessions
- `IsLoggedIn`: Booléen de connexion
- `UserName`: Nom complet de l'utilisateur
- `UserIdentifiant`: Identifiant unique
- `ContactId`: ID GUID du contact

#### Modèles
- **Product**: `ProductId`, `ProductName`, `ProductNumber`, `Price`
- **Contact**: `ContactId`, `FullName`, `Identifiant`
- **Quote**: `QuoteId`, `QuoteNumber`, `TotalAmount`, `StateCode`

---

## Fonctionnalités principales

### 1. Gestion des utilisateurs

#### Authentification
- Formulaire de connexion avec identifiant/mot de passe
- Validation côté serveur via Dataverse
- Gestion des sessions ASP.NET Core (30 minutes timeout)
- Redirection après connexion vers la page demandée

#### Inscription
- Formulaire d'inscription complet:
  - Prénom, nom, email
  - Identifiant unique (vérification de disponibilité)
  - Mot de passe
  - Date de naissance (optionnel)
  - Téléphone mobile (optionnel)
- Création automatique du contact dans Dataverse

#### Espace client
- Tableau de bord avec onglets:
  - **Informations**: Profil personnel
  - **Devis**: Historique des devis
  - **Commandes**: Suivi des commandes
  - **Factures**: Téléchargement des factures
  - **SAV**: Demandes de service après-vente

### 2. Catalogue produits

#### Affichage des produits
- Page d'accueil avec carousel des produits vedettes
- Catalogue complet avec pagination
- Filtres par catégorie (produits parents)
- Recherche floue avec distance de Levenshtein

#### Détail produit
- Fiche produit complète avec:
  - Images haute résolution
  - Description détaillée
  - Prix en multiple devises (EUR, CHF)
  - Spécifications techniques
  - Avis clients

#### Gestion des images
- API dédiée pour les images produits: `/api/product/image/{productId}`
- Cache HTTP (1 heure)
- Images par défaut si indisponible
- Support des images haute définition

### 3. Gestion commerciale

#### Devis (Quotes)
- Création de devis depuis les fiches produits
- Conversion automatique en commandes
- Suivi du statut (Brouillon, Actif, Fermé)
- Calcul des montants avec taxes

#### Commandes (Sales Orders)
- Gestion des commandes client
- Suivi des statuts (Nouveau, En cours, Livré, Facturé)
- Lien avec les devis et factures
- Historique complet

#### Factures (Invoices)
- Génération automatique depuis les commandes
- Support multi-devises
- Téléchargement des factures PDF
- Suivi des paiements

### 4. Service Après-Vente

#### Demandes SAV
- Formulaire de demande de service
- Suivi des statuts (Nouveau, En cours, Résolu, Fermé)
- Historique des interventions
- Communication avec le service client

#### Avis clients
- Système d'évaluation des produits
- Notes de 1 à 5 étoiles
- Commentaires textuels
- Affichage sur les fiches produits

### 5. Fonctionnalités techniques

#### Recherche avancée
- Recherche floue avec tolerance aux fautes de frappe
- Normalisation des chaînes (suppression accents)
- Distance de Levenshtein pour motifs longs
- Recherche en temps réel

#### Multi-devises
- Support EUR et CHF
- Conversion automatique des prix
- Cache des codes de devises
- Sélecteur de devise persistant

#### Thème et UI
- Design moderne avec Bootstrap 5
- Thème clair/sombre
- Palette de couleurs cohérente (violet principal)
- Interface responsive mobile-first

#### Performance
- Cache des résultats Dataverse
- Optimisation des requêtes
- Compression des assets
- Cache HTTP pour les images

---

## Configuration et déploiement

### Configuration requise

#### Environnement de développement
- **.NET 8.0 SDK** ou supérieur
- **Visual Studio 2022** ou VS Code
- **Accès Dataverse** avec permissions appropriées
- **Power Platform CLI** (optionnel)

#### Variables d'environnement
```json
{
  "Dataverse": {
    "Url": "https://org1ebedd82.crm12.dynamics.com/",
    "AppId": "51f81489-12ee-4a9e-aaae-a2591f45987d",
    "RedirectUri": "http://localhost"
  }
}
```

### Déploiement

#### Azure App Service
1. Créer une ressource App Service
2. Configurer les variables d'environnement
3. Déployer via ZIP deploy ou GitHub Actions
4. Configurer le domaine SSL

#### Configuration production
- Activer HTTPS redirection
- Configurer HSTS
- Optimiser les headers de cache
- Surveiller les logs d'application

### Sécurité

#### Authentification
- Sessions sécurisées avec HTTP-only cookies
- Timeout de 30 minutes
- Validation des entrées utilisateur
- Protection contre les attaques CSRF

#### Données
- Connexion sécurisée à Dataverse via OAuth
- Chiffrement des mots de passe (stockage en clair à améliorer)
- Validation des permissions utilisateur

---

## Guide de développement

### Bonnes pratiques

#### Code C#
- Utiliser l'injection de dépendances
- Implémenter IDisposable pour les services
- Gérer les exceptions avec try-catch
- Logger les erreurs avec Console.WriteLine (à améliorer avec ILogger)

#### Frontend
- Utiliser les classes Bootstrap pour la responsivité
- Optimiser les images pour le web
- Minifier les fichiers CSS/JS en production
- Utiliser les attributs data-* pour l'internationalisation

#### Dataverse
- Utiliser des requêtes optimisées avec ColumnSet
- Implémenter un cache pour les données fréquemment accédées
- Gérer les connexions avec retry pattern
- Valider les données avant insertion

### Ajout de nouvelles fonctionnalités

#### 1. Créer une nouvelle page
1. Ajouter les fichiers `.cshtml` et `.cshtml.cs` dans `/Pages`
2. Hériter de `PageModel`
3. Implémenter `OnGet()` et `OnPost()` si nécessaire
4. Ajouter le lien dans le layout

#### 2. Ajouter un endpoint API
1. Créer un contrôleur dans `/Controllers`
2. Hériter de `ControllerBase`
3. Ajouter les attributs de routage `[Route]` et `[HttpGet/Post]`
4. Injecter les services nécessaires

#### 3. Étendre DataverseService
1. Ajouter les méthodes publiques nécessaires
2. Utiliser les patterns existants pour les requêtes
3. Gérer les exceptions de manière cohérente
4. Documenter avec commentaires XML

### Debugging et monitoring

#### Logs d'application
- Logs de connexion Dataverse dans la console
- Messages de debug dans Profile.cshtml.cs
- Logs d'erreurs dans les try-catch

#### Points de vigilance
- Vérifier la connexion Dataverse au démarrage
- Surveiller les timeouts de session
- Optimiser les requêtes N+1
- Gérer les images volumineuses

### Tests recommandés

#### Tests unitaires
- Tester les méthodes de DataverseService
- Valider la logique métier
- Tester les conversions de devises

#### Tests d'intégration
- Tester l'authentification complète
- Valider le flux de commande
- Tester l'upload d'images

#### Tests UI
- Tests de responsivité
- Validation des formulaires
- Tests d'accessibilité

---

## Conclusion

**SAE Dynamics RGF** est une application web moderne et complète pour la vente de montres de luxe, intégrant parfaitement ASP.NET Core 8.0 avec Microsoft Dataverse. L'architecture est bien structurée, le code suit les bonnes pratiques .NET, et l'interface utilisateur offre une expérience client de haute qualité.

### Points forts
- Architecture moderne et maintenable
- Intégration native avec Dataverse
- UI/UX soignée et responsive
- Gestion complète du cycle commercial
- Performance optimisée avec cache

### Axes d'amélioration
- Implémenter un vrai système de logging
- Ajouter des tests unitaires et d'intégration
- Améliorer la sécurité des mots de passe
- Optimiser le SEO
- Ajouter un système de monitoring

Cette documentation servira de référence pour la maintenance, l'évolution et la formation des nouveaux développeurs sur le projet.
