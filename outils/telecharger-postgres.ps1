# =====================================================================
#  Récupère les binaires PostgreSQL pour Windows et n'en garde que ce
#  qui est nécessaire au serveur embarqué.
#
#  À exécuter une seule fois sur le poste de développement. Le dossier
#  « pgsql » produit est ensuite livré à côté de l'application : le poste
#  du magasin n'installe rien.
#
#  Utilisation :
#      .\outils\telecharger-postgres.ps1
#      .\outils\telecharger-postgres.ps1 -Version "17.2-1"
# =====================================================================

param(
    [string]$Version = "17.2-1",
    [string]$Destination = "pgsql",

    # Conserve la documentation et les en-têtes de développement.
    # Sans ce commutateur, environ 200 Mo sont écartés.
    [switch]$Complet
)

$ErrorActionPreference = "Stop"

$url = "https://get.enterprisedb.com/postgresql/postgresql-$Version-windows-x64-binaries.zip"
$archive = Join-Path $env:TEMP "postgresql-$Version-binaries.zip"
$extraction = Join-Path $env:TEMP "postgresql-$Version-extrait"

Write-Host ""
Write-Host "Recuperation de PostgreSQL $Version pour Windows" -ForegroundColor Cyan
Write-Host ""

if (Test-Path $Destination) {
    Write-Host "Le dossier « $Destination » existe deja. Suppression..." -ForegroundColor Gray
    Remove-Item $Destination -Recurse -Force
}

if (-not (Test-Path $archive)) {
    Write-Host "Telechargement (environ 350 Mo, une seule fois)..." -ForegroundColor Gray

    try {
        Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing
    }
    catch {
        Write-Host ""
        Write-Host "Le telechargement a echoue." -ForegroundColor Red
        Write-Host "  URL tentee : $url"
        Write-Host ""
        Write-Host "Recuperez l'archive a la main sur :" -ForegroundColor Yellow
        Write-Host "  https://www.enterprisedb.com/download-postgresql-binaries"
        Write-Host "Choisissez « Win x86-64 », puis relancez ce script apres avoir"
        Write-Host "place le fichier ici :"
        Write-Host "  $archive"
        exit 1
    }
}
else {
    Write-Host "Archive deja presente, telechargement ignore." -ForegroundColor Gray
}

Write-Host "Extraction..." -ForegroundColor Gray

if (Test-Path $extraction) {
    Remove-Item $extraction -Recurse -Force
}

Expand-Archive -Path $archive -DestinationPath $extraction -Force

$source = Join-Path $extraction "pgsql"

if (-not (Test-Path $source)) {
    Write-Host "L'archive ne contient pas le dossier attendu « pgsql »." -ForegroundColor Red
    exit 1
}

New-Item -ItemType Directory -Path $Destination | Out-Null

# Seuls ces trois dossiers sont utiles a l'execution. « doc », « include »
# et « symbols » ne servent qu'au developpement de PostgreSQL lui-meme.
$aGarder = @("bin", "lib", "share")

if ($Complet) {
    $aGarder = Get-ChildItem $source -Directory | Select-Object -ExpandProperty Name
}

foreach ($dossier in $aGarder) {
    $chemin = Join-Path $source $dossier

    if (Test-Path $chemin) {
        Copy-Item $chemin (Join-Path $Destination $dossier) -Recurse
    }
}

Remove-Item $extraction -Recurse -Force

# Verification : sans ces trois programmes, le serveur embarque ne peut rien.
$indispensables = @("initdb.exe", "pg_ctl.exe", "postgres.exe", "pg_dump.exe", "pg_restore.exe")
$manquants = @()

foreach ($outil in $indispensables) {
    if (-not (Test-Path (Join-Path $Destination "bin\$outil"))) {
        $manquants += $outil
    }
}

if ($manquants.Count -gt 0) {
    Write-Host ""
    Write-Host "Programmes manquants : $($manquants -join ', ')" -ForegroundColor Red
    exit 1
}

$taille = [math]::Round((Get-ChildItem $Destination -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB, 0)

Write-Host ""
Write-Host "PostgreSQL est pret dans « $Destination » ($taille Mo)." -ForegroundColor Green
Write-Host ""
Write-Host "Ce dossier doit etre livre a cote de l'executable de l'application." -ForegroundColor Cyan
Write-Host ""
