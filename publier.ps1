# =====================================================================
#  Prépare le dossier de livraison de VIP MEN’S STORE.
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
    [switch]$Allegee,

    # Livre sans PostgreSQL. Le magasin devra alors l'installer lui-même et
    # saisir ses identifiants au premier démarrage.
    [switch]$SansBaseDeDonnees,

    # Active le mode diagnostic : toutes les exceptions sont journalisées.
    # À n'employer que pour retrouver un incident précis, jamais en
    # exploitation courante.
    [switch]$Diagnostic
)

$ErrorActionPreference = "Stop"

$nomDossier = "GestionMagasin-$Version"
$cheminSortie = Join-Path $Destination $nomDossier

Write-Host ""
Write-Host "Compilation de VIP MEN’S STORE $Version..." -ForegroundColor Cyan
Write-Host ""

# ---------------------------------------------------------------------
#  Outils necessaires SUR CE POSTE.
#
#  Ce sont ceux du poste qui construit, pas ceux du magasin : le paquet
#  livre n'exige rien. Sans ce controle, l'absence du SDK se manifeste par
#  un mur d'anglais au milieu de la sortie, qui ne dit ni ce qui manque ni
#  ou le prendre.
# ---------------------------------------------------------------------

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue

if (-not $dotnet) {
    Write-Host "Le SDK .NET n'est pas installe sur ce poste." -ForegroundColor Red
    Write-Host ""
    Write-Host "Il sert uniquement a construire la livraison. Le magasin," -ForegroundColor Yellow
    Write-Host "lui, n'installera rien." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Installez le « .NET 10 SDK » (et non le Runtime seul) :" -ForegroundColor Cyan
    Write-Host "    https://dotnet.microsoft.com/download/dotnet/10.0"
    Write-Host ""
    Write-Host "Fermez puis rouvrez PowerShell apres l'installation."
    Write-Host ""
    exit 1
}

$sdks = @(& dotnet --list-sdks 2>$null)

if ($sdks.Count -eq 0) {
    Write-Host "Aucun SDK .NET n'est installe : seul le Runtime est present." -ForegroundColor Red
    Write-Host ""
    Write-Host "Le Runtime execute les applications, il n'en construit pas." -ForegroundColor Yellow
    Write-Host "Installez le « .NET 10 SDK » :" -ForegroundColor Cyan
    Write-Host "    https://dotnet.microsoft.com/download/dotnet/10.0"
    Write-Host ""
    Write-Host "Fermez puis rouvrez PowerShell apres l'installation."
    Write-Host ""
    exit 1
}

if (-not ($sdks | Where-Object { $_ -match "^10\." })) {
    Write-Host "Le SDK .NET 10 est absent. Versions trouvees sur ce poste :" -ForegroundColor Red
    Write-Host ""
    foreach ($sdk in $sdks) {
        Write-Host "    $sdk" -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "Le logiciel vise net10.0-windows : une version anterieure ne" -ForegroundColor Yellow
    Write-Host "sait pas le construire." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Installez le « .NET 10 SDK » :" -ForegroundColor Cyan
    Write-Host "    https://dotnet.microsoft.com/download/dotnet/10.0"
    Write-Host ""
    exit 1
}


# Une version precedemment publiee peut encore tourner et retenir ses
# fichiers : la suppression echouerait alors sur un « Access denied »
# incomprehensible. On ferme donc ce qui traine avant de reconstruire.
foreach ($nom in @("GestionMagasin", "postgres")) {
    $processus = Get-Process -Name $nom -ErrorAction SilentlyContinue

    if ($processus) {
        Write-Host "Fermeture de $nom encore en cours d'execution..." -ForegroundColor Gray
        $processus | Stop-Process -Force -ErrorAction SilentlyContinue
    }
}

Start-Sleep -Milliseconds 800

if (Test-Path $cheminSortie) {
    try {
        Remove-Item $cheminSortie -Recurse -Force -ErrorAction Stop
    }
    catch {
        Write-Host ""
        Write-Host "Impossible de supprimer la livraison precedente :" -ForegroundColor Red
        Write-Host "  $cheminSortie"
        Write-Host ""
        Write-Host "Un programme retient encore ses fichiers. Fermez toute fenetre" -ForegroundColor Yellow
        Write-Host "de VIP MEN’S STORE, ainsi que l'explorateur ouvert sur ce dossier,"
        Write-Host "puis relancez. En dernier recours, supprimez le dossier a la main."
        Write-Host ""
        exit 1
    }
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

# Le niveau de detail suit le mode demande : en diagnostic, tout est
# journalise. Sans cela, le paquet livre museler les traces posees pour
# retrouver un incident.
$niveau = if ($Diagnostic) { "Debug" } else { "Information" }

# Dans un bloc de texte PowerShell, les guillemets sont deja litteraux :
# les doubler les ecrirait deux fois et produirait un fichier illisible.
$configuration = @"
{
  "ConnectionStrings": {
    "BaseDonnees": ""
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "$niveau",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
      }
    }
  }
}
"@

Set-Content -Path $configLivree -Value $configuration -Encoding UTF8

# Le logiciel refuse de demarrer si ce fichier est mal forme : on le verifie
# ici plutot que de le decouvrir au premier lancement chez le client.
try {
    $configuration | ConvertFrom-Json | Out-Null
}
catch {
    Write-Host ""
    Write-Host "La configuration produite est illisible :" -ForegroundColor Red
    Write-Host $configuration
    exit 1
}

Write-Host "Configuration livree sans identifiants : le magasin saisira les siens au premier demarrage." -ForegroundColor Gray

# --- PostgreSQL livré avec le logiciel ---
#
# Sans ce dossier, le magasin devrait installer PostgreSQL lui-même. Sa
# presence est donc verifiee avant de constituer l'archive : un paquet
# incomplet ne se decouvrirait que chez le client.
if (-not $SansBaseDeDonnees) {
    # Le dossier « pgsql » n'est pas dans le depot : 150 Mo de binaires
    # Windows n'y ont pas leur place. Il est donc absent de tout poste ou le
    # depot vient d'etre clone. Plutot que d'expliquer a l'operateur qu'un
    # second script existe, on le lance : oublier cette etape produit un
    # paquet sans base de donnees, et l'incident n'apparait que chez le
    # client, longtemps apres.
    if (-not (Test-Path "pgsql\bin\pg_ctl.exe")) {
        Write-Host ""
        Write-Host "Le dossier « pgsql » est absent : recuperation de PostgreSQL." -ForegroundColor Yellow
        Write-Host "Environ 350 Mo, une seule fois sur ce poste." -ForegroundColor Gray
        Write-Host ""

        $recuperation = Join-Path $PSScriptRoot "outils\telecharger-postgres.ps1"

        if (-not (Test-Path $recuperation)) {
            Write-Host "Le script « outils\telecharger-postgres.ps1 » est introuvable." -ForegroundColor Red
            Write-Host "Le depot est incomplet : reclonez-le." -ForegroundColor Red
            exit 1
        }

        & $recuperation

        if ($LASTEXITCODE -ne 0 -or -not (Test-Path "pgsql\bin\pg_ctl.exe")) {
            Write-Host ""
            Write-Host "PostgreSQL n'a pas pu etre recupere : la livraison est interrompue." -ForegroundColor Red
            Write-Host "Sans lui, le client devrait installer PostgreSQL lui-meme." -ForegroundColor Red
            Write-Host ""
            Write-Host "Pour publier quand meme, sans base de donnees :" -ForegroundColor Yellow
            Write-Host "    .\publier.ps1 -SansBaseDeDonnees"
            Write-Host ""
            exit 1
        }
    }

    Write-Host "Integration de PostgreSQL dans la livraison..." -ForegroundColor Gray
    Copy-Item "pgsql" (Join-Path $cheminSortie "pgsql") -Recurse
}
else {
    Write-Host "Livraison SANS base de donnees : le magasin devra installer PostgreSQL." -ForegroundColor Yellow
}

if ($Diagnostic) {
    Set-Content -Path (Join-Path $cheminSortie "diagnostic.txt") `
        -Value "Mode diagnostic. Supprimez ce fichier pour revenir au fonctionnement normal." `
        -Encoding UTF8

    Write-Host "MODE DIAGNOSTIC : toutes les exceptions seront journalisees." -ForegroundColor Yellow
}

# Logo du magasin. Posé a la racine du depot, il est livre a cote du
# programme et remplace le blason dessine. Sans lui, le blason dessine
# s'affiche : la livraison reste complete dans les deux cas.
$logo = "logo.png"
if (Test-Path $logo) {
    Copy-Item $logo (Join-Path $cheminSortie $logo)
    Write-Host "Logo du magasin inclus." -ForegroundColor Green
} else {
    Write-Host "Aucun logo.png a la racine : le blason dessine sera utilise." -ForegroundColor DarkGray
}

# Guide d'installation destiné au magasin.
$guide = "GUIDE-INSTALLATION.md"
if (Test-Path $guide) {
    Copy-Item $guide (Join-Path $cheminSortie $guide)
}

# ---------------------------------------------------------------------
#  Verification de ce qui a REELLEMENT ete produit.
#
#  Verifier la source ne suffit pas : une copie interrompue, un antivirus
#  qui ecarte un executable, un fichier verrouille par une instance encore
#  ouverte, et le paquet part incomplet. L'incident ne se decouvre alors
#  que chez le client, qui n'a aucun moyen de le comprendre.
# ---------------------------------------------------------------------

Write-Host ""
Write-Host "Verification du paquet..." -ForegroundColor Gray

$attendus = @("GestionMagasin.exe", "appsettings.json", "GUIDE-INSTALLATION.md")

if (-not $SansBaseDeDonnees) {
    $attendus += @(
        "pgsql\bin\pg_ctl.exe",
        "pgsql\bin\postgres.exe",
        "pgsql\bin\initdb.exe",
        "pgsql\bin\pg_dump.exe",
        "pgsql\bin\pg_restore.exe",
        "pgsql\share\postgresql.conf.sample"
    )
}

$absents = @()

foreach ($fichier in $attendus) {
    if (-not (Test-Path (Join-Path $cheminSortie $fichier))) {
        $absents += $fichier
    }
}

if ($absents.Count -gt 0) {
    Write-Host ""
    Write-Host "PAQUET INCOMPLET : ne le livrez pas." -ForegroundColor Red
    Write-Host ""
    foreach ($fichier in $absents) {
        Write-Host "  manquant : $fichier" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "Fermez toute instance du logiciel, verifiez que l'antivirus" -ForegroundColor Yellow
    Write-Host "n'ecarte rien, puis relancez la publication." -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

# Marqueur de livraison. Le logiciel s'en sert pour savoir qu'il tourne
# chez un client et non sur un poste de developpement : si le dossier
# « pgsql » venait a manquer, il le dira au lieu de chercher un PostgreSQL
# installe sur la machine, qu'un magasin n'a pas.
if (-not $SansBaseDeDonnees) {
    Set-Content -Path (Join-Path $cheminSortie "livraison.txt") `
        -Value "Paquet complet, base de donnees incluse. Version $Version." `
        -Encoding UTF8
}

# Archive prête à être envoyée.
$archive = Join-Path $Destination "$nomDossier.zip"
if (Test-Path $archive) {
    Remove-Item $archive -Force
}

Compress-Archive -Path "$cheminSortie\*" -DestinationPath $archive

# L'archive est relue : c'est elle qui part chez le client, pas le dossier.
if (-not $SansBaseDeDonnees) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $lecture = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path $archive).Path)

    try {
        $entrees = @($lecture.Entries | Where-Object { $_.FullName -like "pgsql/*" })
        $programme = @($lecture.Entries | Where-Object { $_.FullName -eq "GestionMagasin.exe" })

        if ($entrees.Count -lt 100 -or $programme.Count -eq 0) {
            Write-Host ""
            Write-Host "L'ARCHIVE EST INCOMPLETE : ne la livrez pas." -ForegroundColor Red
            Write-Host "  fichiers pgsql dans l'archive : $($entrees.Count)" -ForegroundColor Red
            Write-Host ""
            exit 1
        }

        Write-Host "  base de donnees incluse : $($entrees.Count) fichiers" -ForegroundColor Green
    }
    finally {
        $lecture.Dispose()
    }
}

Write-Host "  paquet complet." -ForegroundColor Green

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
if ($SansBaseDeDonnees) {
    if ($autonome) {
        Write-Host "Le poste du magasin doit installer PostgreSQL." -ForegroundColor Yellow
    } else {
        Write-Host "Le poste du magasin doit installer PostgreSQL ET le .NET 10 Desktop Runtime." -ForegroundColor Yellow
    }

    Write-Host "Le logiciel demandera les informations de connexion au premier demarrage."
}
elseif ($autonome) {
    Write-Host "Le poste du magasin n'a RIEN a installer." -ForegroundColor Green
    Write-Host "Decompresser, double-cliquer : le logiciel demarre sa propre base de donnees."
}
else {
    Write-Host "Le poste du magasin a besoin du .NET 10 Desktop Runtime." -ForegroundColor Yellow
    Write-Host "La base de donnees, elle, est livree avec le logiciel."
}

Write-Host ""
