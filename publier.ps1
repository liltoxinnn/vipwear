# =====================================================================
#  Prépare le dossier de livraison de Gestion Magasin.
#
#  Produit une version autonome : le poste du magasin n'a besoin
#  d'aucune installation de .NET, seulement de PostgreSQL.
#
#  Utilisation :
#      .\publier.ps1
#      .\publier.ps1 -Version "1.1.0"
# =====================================================================

param(
    [string]$Version = "1.0.0",
    [string]$Destination = "livraison",

    # Version allégée : le poste du magasin doit alors avoir le
    # « .NET 10 Desktop Runtime » installé, mais l'archive passe
    # d'environ 70 Mo à moins de 15 Mo.
    [switch]$Allegee
)

$ErrorActionPreference = "Stop"

$nomDossier = "GestionMagasin-$Version"
$cheminSortie = Join-Path $Destination $nomDossier

Write-Host ""
Write-Host "Compilation de Gestion Magasin $Version..." -ForegroundColor Cyan
Write-Host ""

if (Test-Path $cheminSortie) {
    Remove-Item $cheminSortie -Recurse -Force
}

$autonome = -not $Allegee

if ($autonome) {
    Write-Host "Mode autonome : aucune installation .NET requise sur le poste du magasin." -ForegroundColor Gray
} else {
    Write-Host "Mode allégé : le .NET 10 Desktop Runtime devra être installé sur le poste." -ForegroundColor Yellow
}

# En mode autonome, le moteur .NET est embarqué dans le dossier livré.
dotnet publish src/GestionMagasin.App/GestionMagasin.App.csproj `
    -c Release `
    -r win-x64 `
    --self-contained $autonome `
    -p:Version=$Version `
    -o $cheminSortie

if ($LASTEXITCODE -ne 0) {
    Write-Host "La compilation a échoué." -ForegroundColor Red
    exit 1
}

# Le fichier de configuration locale ne doit jamais être livré : il
# contient les identifiants du poste de développement.
$configLocale = Join-Path $cheminSortie "appsettings.Local.json"
if (Test-Path $configLocale) {
    Remove-Item $configLocale -Force
}

# La configuration livrée ne contient aucun identifiant. Le poste du magasin
# a son propre mot de passe PostgreSQL, choisi à son installation : le
# logiciel le demande au premier démarrage. Livrer un mot de passe par défaut
# reviendrait à le publier, et masquerait cette étape si le magasin utilisait
# par hasard le même.
$configLivree = Join-Path $cheminSortie "appsettings.json"

@'
{
  "ConnectionStrings": {
    "BaseDonnees": ""
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
      }
    }
  }
}
'@ | Set-Content -Path $configLivree -Encoding UTF8

Write-Host "Configuration livree sans identifiants : le magasin saisira les siens au premier demarrage." -ForegroundColor Gray

# Guide d'installation destiné au magasin.
$guide = "GUIDE-INSTALLATION.md"
if (Test-Path $guide) {
    Copy-Item $guide (Join-Path $cheminSortie $guide)
}

# Archive prête à être envoyée.
$archive = Join-Path $Destination "$nomDossier.zip"
if (Test-Path $archive) {
    Remove-Item $archive -Force
}

Compress-Archive -Path "$cheminSortie\*" -DestinationPath $archive

$tailleMo = [math]::Round((Get-Item $archive).Length / 1MB, 1)

Write-Host ""
Write-Host "Livraison prête." -ForegroundColor Green
Write-Host ""
Write-Host "  Dossier : $cheminSortie"
Write-Host "  Archive : $archive ($tailleMo Mo)"
Write-Host ""
Write-Host "À transmettre au client :" -ForegroundColor Cyan
Write-Host "  1. L'archive ci-dessus"
Write-Host "  2. Le guide GUIDE-INSTALLATION.md (déjà inclus dans l'archive)"
Write-Host ""
if ($autonome) {
    Write-Host "Le poste du magasin n'a besoin que de PostgreSQL." -ForegroundColor Yellow
} else {
    Write-Host "Le poste du magasin a besoin de PostgreSQL ET du .NET 10 Desktop Runtime." -ForegroundColor Yellow
}

Write-Host "Le logiciel demandera lui-meme les informations de connexion au premier demarrage."
Write-Host ""
