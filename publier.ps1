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

# ---------------------------------------------------------------------
# Icone du programme
#
# Le logo du magasin devient l'icone de l'executable : barre des taches,
# raccourci du bureau, Explorateur. Sans cette conversion, le magasin
# voyait son logo dans le logiciel mais l'embleme dessine sur son
# raccourci — deux emblemes pour un seul commerce.
#
# La conversion doit avoir lieu avant la compilation : le fichier projet
# lit « logo.ico » au moment ou il fabrique l'executable.
# ---------------------------------------------------------------------
function Convertir-LogoEnIcone {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    Add-Type -AssemblyName System.Drawing

    # Windows choisit dans le fichier la taille qui lui convient. N'y mettre
    # que le grand format ferait reduire l'image a la volee dans la barre
    # des taches, ou elle paraitrait sale.
    $tailles = @(16, 24, 32, 48, 64, 128, 256)

    $image = $null
    $vignettes = @()

    try {
        $image = [System.Drawing.Image]::FromFile($Source)

        foreach ($taille in $tailles) {
            $carre = New-Object System.Drawing.Bitmap $taille, $taille
            $dessin = [System.Drawing.Graphics]::FromImage($carre)

            $dessin.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $dessin.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $dessin.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $dessin.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $dessin.Clear([System.Drawing.Color]::Transparent)
            $dessin.DrawImage($image, 0, 0, $taille, $taille)
            $dessin.Dispose()

            $memoire = New-Object System.IO.MemoryStream
            $carre.Save($memoire, [System.Drawing.Imaging.ImageFormat]::Png)
            $carre.Dispose()

            $vignettes += ,@($taille, $memoire.ToArray())
            $memoire.Dispose()
        }
    }
    finally {
        if ($image) { $image.Dispose() }
    }

    # Assemblage du conteneur ICO : un en-tete, un repertoire de six octets
    # par image, puis les images elles-memes, au format PNG.
    $flux = New-Object System.IO.MemoryStream
    $ecrivain = New-Object System.IO.BinaryWriter $flux

    $ecrivain.Write([UInt16]0)                     # reserve
    $ecrivain.Write([UInt16]1)                     # type : icone
    $ecrivain.Write([UInt16]$vignettes.Count)

    $decalage = 6 + 16 * $vignettes.Count

    foreach ($vignette in $vignettes) {
        $taille = $vignette[0]
        $octets = $vignette[1]

        # Zero designe 256 : un octet ne va pas plus loin.
        $ecrivain.Write([Byte]($(if ($taille -ge 256) { 0 } else { $taille })))
        $ecrivain.Write([Byte]($(if ($taille -ge 256) { 0 } else { $taille })))
        $ecrivain.Write([Byte]0)                   # palette
        $ecrivain.Write([Byte]0)                   # reserve
        $ecrivain.Write([UInt16]1)                 # plans
        $ecrivain.Write([UInt16]32)                # bits par pixel
        $ecrivain.Write([UInt32]$octets.Length)
        $ecrivain.Write([UInt32]$decalage)

        $decalage += $octets.Length
    }

    foreach ($vignette in $vignettes) {
        $ecrivain.Write($vignette[1])
    }

    $ecrivain.Flush()
    [System.IO.File]::WriteAllBytes($Destination, $flux.ToArray())
    $ecrivain.Dispose()
    $flux.Dispose()
}

$nomsLogoSource = @("logo.png", "logo.jpg", "logo.jpeg", "logo.bmp")
$logoSource = $nomsLogoSource | Where-Object { Test-Path (Join-Path $PSScriptRoot $_) } | Select-Object -First 1
$cheminIcone = Join-Path $PSScriptRoot "src\GestionMagasin.App\logo.ico"

if ($logoSource) {
    try {
        Convertir-LogoEnIcone `
            -Source (Join-Path $PSScriptRoot $logoSource) `
            -Destination $cheminIcone

        Write-Host "Icone du programme tiree de $logoSource." -ForegroundColor Green
    }
    catch {
        Write-Host "Le logo n'a pas pu servir d'icone : $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "L'embleme dessine sera employe a la place." -ForegroundColor Yellow

        if (Test-Path $cheminIcone) { Remove-Item $cheminIcone -Force }
    }
}
elseif (Test-Path $cheminIcone) {
    # Le logo a ete retire depuis la derniere publication : l'icone qui en
    # decoulait ne doit pas survivre.
    Remove-Item $cheminIcone -Force
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
# Les quatre noms que le logiciel sait lire, dans son ordre de preference.
$nomsLogo = @("logo.png", "logo.jpg", "logo.jpeg", "logo.bmp")

$logo = $nomsLogo | Where-Object { Test-Path (Join-Path $PSScriptRoot $_) } | Select-Object -First 1

if ($logo) {
    Copy-Item (Join-Path $PSScriptRoot $logo) (Join-Path $cheminSortie $logo)
    Write-Host "Logo du magasin inclus : $logo" -ForegroundColor Green
}
else {
    Write-Host "Aucun logo a la racine : le blason dessine sera utilise." -ForegroundColor DarkGray

    # Windows masque les extensions connues : un fichier enregistre sous le
    # nom « logo.png » devient « logo.png.png », que rien ne distingue a
    # l'ecran. Montrer ce qui se trouve reellement la evite de chercher
    # longtemps une erreur invisible.
    $proches = @(Get-ChildItem -Path $PSScriptRoot -Filter "logo*" -File -ErrorAction SilentlyContinue)

    if ($proches.Count -gt 0) {
        Write-Host ""
        Write-Host "  Ces fichiers sont pourtant presents :" -ForegroundColor Yellow
        foreach ($fichier in $proches) {
            Write-Host "    $($fichier.Name)" -ForegroundColor Yellow
        }
        Write-Host ""
        Write-Host "  Renommez-en un en « logo.png » exactement." -ForegroundColor Yellow
        Write-Host "  Windows masque les extensions : affichez-les dans l'Explorateur" -ForegroundColor Yellow
        Write-Host "  (Affichage > Afficher > Extensions de noms de fichiers)." -ForegroundColor Yellow
        Write-Host ""
    }
    else {
        Write-Host "  Attendu ici : $(Join-Path $PSScriptRoot 'logo.png')" -ForegroundColor DarkGray
    }
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

Add-Type -AssemblyName System.IO.Compression.FileSystem

# CreateFromDirectory plutot que Compress-Archive : ce dernier est lent sur
# les milliers de fichiers de PostgreSQL, et surtout il ecrit les noms
# d'entrees avec des barres inverses selon la version de PowerShell. La
# verification qui suit cherchait alors « pgsql/ » dans une archive qui
# contenait « pgsql\ », et refusait un paquet parfaitement complet.
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    (Resolve-Path $cheminSortie).Path,
    (Join-Path (Resolve-Path $Destination).Path "$nomDossier.zip"))

# L'archive est relue : c'est elle qui part chez le client, pas le dossier.
if (-not $SansBaseDeDonnees) {
    $lecture = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path $archive).Path)

    try {
        # Les deux separateurs sont admis : leur choix depend de l'outil qui a
        # ecrit l'archive, jamais de son contenu.
        $noms = $lecture.Entries | ForEach-Object { $_.FullName -replace "\\", "/" }

        $entrees = @($noms | Where-Object { $_ -like "pgsql/*" })
        $programme = @($noms | Where-Object { $_ -eq "GestionMagasin.exe" })

        if ($entrees.Count -lt 100 -or $programme.Count -eq 0) {
            Write-Host ""
            Write-Host "L'ARCHIVE EST INCOMPLETE : ne la livrez pas." -ForegroundColor Red
            Write-Host "  fichiers pgsql dans l'archive : $($entrees.Count)" -ForegroundColor Red
            Write-Host "  programme present : $($programme.Count -gt 0)" -ForegroundColor Red
            Write-Host ""
            Write-Host "  premieres entrees de l'archive :" -ForegroundColor Yellow
            foreach ($nom in ($noms | Select-Object -First 5)) {
                Write-Host "    $nom" -ForegroundColor Gray
            }
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
