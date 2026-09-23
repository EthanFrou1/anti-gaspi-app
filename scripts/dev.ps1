<#
.SYNOPSIS
    Lance tout l'environnement de développement en une commande (Windows).

.DESCRIPTION
    Par défaut :
      1. vérifie les outils (Docker, .NET, Node.js) et démarre Docker Desktop si besoin ;
      2. prépare la configuration locale au premier lancement (.env, user-secrets, .env.local) ;
      3. démarre PostgreSQL et applique les migrations ;
      4. ouvre l'API dans une nouvelle fenêtre et attend qu'elle réponde ;
      5. installe les dépendances mobiles si package-lock.json a changé ;
      6. lance Expo dans cette fenêtre (QR code à scanner avec Expo Go).

    Aucun secret n'est affiché, et le pare-feu Windows n'est jamais modifié.

.PARAMETER Target
    device   (défaut) : téléphone réel sur le même Wi-Fi. L'API écoute sur le réseau local
                        et l'URL de l'API est mise à jour avec l'IP actuelle du PC.
    emulator          : émulateur Android. L'API n'écoute que sur ce PC (plus sûr).

.PARAMETER Test
    Lance tous les tests (API + mobile) au lieu de démarrer l'app.

.PARAMETER Stop
    Arrête l'API et PostgreSQL.

.EXAMPLE
    .\dev.cmd                    # tout démarrer pour un téléphone réel
    .\dev.cmd -Target emulator   # tout démarrer pour l'émulateur Android
    .\dev.cmd -Test              # lancer tous les tests
    .\dev.cmd -Stop              # tout arrêter
#>
[CmdletBinding()]
param(
    [ValidateSet('device', 'emulator')]
    [string]$Target = 'device',
    [switch]$Test,
    [switch]$Stop
)

$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
$ApiDir = Join-Path $Root 'api'
$ApiProject = Join-Path $ApiDir 'src\Api'
$MobileDir = Join-Path $Root 'mobile'
$ApiPort = 5122

# ---------------------------------------------------------------------------
# Affichage
# ---------------------------------------------------------------------------

function Write-Step([string]$Message) { Write-Host "`n==> $Message" -ForegroundColor Cyan }
function Write-Ok([string]$Message) { Write-Host "    OK  $Message" -ForegroundColor Green }
function Write-Info([string]$Message) { Write-Host "    $Message" }
function Write-Warn([string]$Message) { Write-Host "    /!\ $Message" -ForegroundColor Yellow }

function Stop-WithError([string]$Message) {
    Write-Host "`n    ERREUR : $Message" -ForegroundColor Red
    exit 1
}

# Windows PowerShell 5.1 transforme la sortie d'erreur (stderr) d'une commande externe
# en erreur bloquante quand $ErrorActionPreference vaut 'Stop' et que la sortie est
# redirigée. Or docker, dotnet et npm y écrivent leur progression. Pour les commandes
# externes, on relâche donc ce réglage et on se fie à leur code de retour.
function Invoke-Native([scriptblock]$Command) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $Command
    }
    finally {
        $ErrorActionPreference = $previous
    }
}

# Lance une commande externe et s'arrête si elle échoue.
function Invoke-Checked([string]$Description, [scriptblock]$Command) {
    Invoke-Native $Command
    if ($LASTEXITCODE -ne 0) {
        Stop-WithError "$Description a échoué (code $LASTEXITCODE)."
    }
}

# Comme Invoke-Native, mais sans afficher la sortie : seul le succès compte.
function Test-NativeSucceeds([scriptblock]$Command) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $Command *> $null
        return $LASTEXITCODE -eq 0
    }
    finally {
        $ErrorActionPreference = $previous
    }
}

# Écrit un fichier texte en UTF-8 SANS BOM (sinon Expo lirait mal la première ligne).
function Write-TextFile([string]$Path, [string[]]$Lines) {
    [System.IO.File]::WriteAllLines($Path, $Lines, (New-Object System.Text.UTF8Encoding $false))
}

# ---------------------------------------------------------------------------
# Prérequis
# ---------------------------------------------------------------------------

function Assert-Command([string]$Name, [string]$InstallHint) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Stop-WithError "'$Name' est introuvable. $InstallHint"
    }
}

function Assert-Prerequisites {
    Write-Step 'Vérification des outils'
    Assert-Command 'docker' 'Installe Docker Desktop : https://www.docker.com/products/docker-desktop/'
    Assert-Command 'dotnet' 'Installe le SDK .NET 10 : winget install Microsoft.DotNet.SDK.10'
    Assert-Command 'node' 'Installe Node.js LTS : winget install OpenJS.NodeJS.LTS'
    Write-Ok 'Docker, .NET et Node.js sont installés'
}

function Start-DockerIfNeeded {
    Write-Step 'Docker Desktop'
    if (Test-NativeSucceeds { docker info }) {
        Write-Ok 'Docker tourne déjà'
        return
    }

    $dockerDesktop = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
    if (-not (Test-Path $dockerDesktop)) {
        Stop-WithError 'Docker ne répond pas et Docker Desktop est introuvable. Lance-le manuellement.'
    }

    Write-Info 'Démarrage de Docker Desktop (jusqu''à 2 minutes)...'
    Start-Process $dockerDesktop
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Seconds 2
        if (Test-NativeSucceeds { docker info }) {
            Write-Ok 'Docker est prêt'
            return
        }
    }
    Stop-WithError 'Docker ne répond toujours pas. Ouvre Docker Desktop et relance le script.'
}

# ---------------------------------------------------------------------------
# Configuration locale (créée au premier lancement, jamais écrasée ensuite)
# ---------------------------------------------------------------------------

function New-RandomSecret([int]$Bytes) {
    $buffer = New-Object byte[] $Bytes
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($buffer)
    return [Convert]::ToBase64String($buffer)
}

# Lit un fichier KEY=VALUE (ignore les commentaires).
function Read-EnvFile([string]$Path) {
    $values = @{}
    foreach ($line in Get-Content $Path) {
        if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)$') {
            $values[$Matches[1]] = $Matches[2].Trim()
        }
    }
    return $values
}

function Initialize-RootEnv {
    $envFile = Join-Path $Root '.env'
    if (Test-Path $envFile) {
        Write-Ok '.env existe déjà (identifiants PostgreSQL)'
        return
    }

    # Mot de passe alphanumérique : pas de caractère à échapper dans la chaîne de connexion.
    $password = (New-RandomSecret 24) -replace '[^A-Za-z0-9]', ''
    $content = (Get-Content (Join-Path $Root '.env.example')) -replace '^POSTGRES_PASSWORD=.*$', "POSTGRES_PASSWORD=$password"
    Write-TextFile $envFile $content
    Write-Ok '.env créé avec un mot de passe PostgreSQL aléatoire'
}

function Get-UserSecretKeys {
    $output = Invoke-Native { dotnet user-secrets list --project $ApiProject }
    $keys = @()
    foreach ($line in $output) {
        if ($line -match '^(.+?) = ') {
            $keys += $Matches[1]
        }
    }
    return $keys
}

function Initialize-UserSecrets {
    $keys = Get-UserSecretKeys
    $db = Read-EnvFile (Join-Path $Root '.env')

    if ($keys -notcontains 'ConnectionStrings:Default') {
        $port = if ($db['POSTGRES_PORT']) { $db['POSTGRES_PORT'] } else { '5432' }
        $connection = "Host=localhost;Port=$port;Database=$($db['POSTGRES_DB']);Username=$($db['POSTGRES_USER']);Password=$($db['POSTGRES_PASSWORD'])"
        Invoke-Checked 'Enregistrement de la chaîne de connexion' {
            dotnet user-secrets set 'ConnectionStrings:Default' $connection --project $ApiProject | Out-Null
        }
        Write-Ok 'Chaîne de connexion enregistrée dans les user-secrets'
    }
    else {
        Write-Ok 'Chaîne de connexion déjà présente dans les user-secrets'
    }

    if ($keys -notcontains 'Jwt:SigningKey') {
        $signingKey = New-RandomSecret 64
        Invoke-Checked 'Enregistrement de la clé JWT' {
            dotnet user-secrets set 'Jwt:SigningKey' $signingKey --project $ApiProject | Out-Null
        }
        Write-Ok 'Clé de signature JWT générée dans les user-secrets'
    }
    else {
        Write-Ok 'Clé JWT déjà présente dans les user-secrets'
    }
}

# IP du PC sur le réseau local (interface active avec une passerelle).
function Get-LanInfo {
    $config = Get-NetIPConfiguration |
        Where-Object { $null -ne $_.IPv4DefaultGateway -and $_.NetAdapter.Status -eq 'Up' } |
        Select-Object -First 1
    if ($null -eq $config) {
        return $null
    }
    $netProfile = Get-NetConnectionProfile -InterfaceIndex $config.InterfaceIndex -ErrorAction SilentlyContinue
    return [pscustomobject]@{
        Ip        = @($config.IPv4Address)[0].IPAddress
        Interface = $config.InterfaceAlias
        Category  = if ($netProfile) { [string]$netProfile.NetworkCategory } else { 'Inconnu' }
    }
}

function Initialize-MobileEnv {
    $envLocal = Join-Path $MobileDir '.env.local'

    if ($Target -eq 'emulator') {
        $apiUrl = "http://10.0.2.2:$ApiPort"
    }
    else {
        $lan = Get-LanInfo
        if ($null -eq $lan) {
            Stop-WithError 'Aucune connexion réseau active trouvée. Connecte le PC au Wi-Fi.'
        }
        $apiUrl = "http://$($lan.Ip):$ApiPort"
        Write-Info "Réseau : $($lan.Interface), IP $($lan.Ip), profil $($lan.Category)"
        if ($lan.Category -eq 'Public') {
            Write-Warn 'Réseau classé « Public » : le pare-feu Windows bloquera sûrement le téléphone.'
            Write-Warn 'Voir mobile/README.md, section Dépannage (partage de connexion ou émulateur).'
        }
    }

    $line = "EXPO_PUBLIC_API_URL=$apiUrl"
    if (-not (Test-Path $envLocal)) {
        Copy-Item (Join-Path $MobileDir '.env.example') $envLocal
    }

    # Met à jour uniquement la ligne de l'URL (l'IP du PC peut changer d'un jour à l'autre).
    $content = @(Get-Content $envLocal)
    if ($content -contains $line) {
        Write-Ok "mobile/.env.local pointe déjà vers $apiUrl"
        return $false
    }
    $content = $content | Where-Object { $_ -notmatch '^EXPO_PUBLIC_API_URL=' }
    $content = @($content) + $line
    Write-TextFile $envLocal $content
    Write-Ok "mobile/.env.local mis à jour : $apiUrl"
    # URL modifiée : il faudra vider le cache d'Expo pour qu'elle soit prise en compte.
    return $true
}

# ---------------------------------------------------------------------------
# Services
# ---------------------------------------------------------------------------

function Start-Database {
    Write-Step 'PostgreSQL'
    Push-Location $Root
    try {
        Invoke-Checked 'Le démarrage de PostgreSQL' { docker compose up -d --wait }
    }
    finally {
        Pop-Location
    }
    Write-Ok 'PostgreSQL est prêt'
}

function Update-Database {
    Write-Step 'Migrations de la base'
    Push-Location $ApiDir
    try {
        Invoke-Checked 'La restauration de dotnet-ef' { dotnet tool restore | Out-Null }
        Invoke-Checked 'L''application des migrations' { dotnet ef database update --project src/Api }
    }
    finally {
        Pop-Location
    }
    Write-Ok 'Base à jour'
}

function Stop-Api {
    # Uniquement le processus compilé depuis CE repo (pas un autre programme nommé « Api »).
    $running = Get-Process -Name 'Api' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($ApiDir, [System.StringComparison]::OrdinalIgnoreCase) }
    if ($running) {
        $running | Stop-Process -Force
        Write-Ok 'Ancienne instance de l''API arrêtée'
    }
    else {
        Write-Ok 'Aucune instance de l''API en cours'
    }
}

function Test-ApiReady {
    # 127.0.0.1 et pas « localhost » : avec --urls http://0.0.0.0, l'API n'écoute qu'en IPv4.
    # « localhost » essaie d'abord l'IPv6 (::1) ; Windows réessaie une connexion refusée
    # pendant environ 2 s avant de passer à l'IPv4, et le délai de 2 s expirait à chaque fois.
    try {
        $response = Invoke-WebRequest "http://127.0.0.1:$ApiPort/openapi/v1.json" -UseBasicParsing -TimeoutSec 2
        return $response.StatusCode -eq 200
    }
    catch {
        return $false
    }
}

function Start-Api {
    Write-Step 'API'

    # device : écoute sur toutes les interfaces pour que le téléphone la joigne.
    $urls = if ($Target -eq 'device') { "http://0.0.0.0:$ApiPort" } else { "http://localhost:$ApiPort" }
    $command = "`$Host.UI.RawUI.WindowTitle = 'API anti-gaspi'; Set-Location '$ApiDir'; dotnet run --project src/Api --urls $urls"
    Start-Process powershell -ArgumentList '-NoExit', '-NoProfile', '-Command', $command | Out-Null
    Write-Info "Démarrage dans une nouvelle fenêtre ($urls)..."

    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Seconds 2
        if (Test-ApiReady) {
            Write-Ok "API prête : http://localhost:$ApiPort/openapi/v1.json"
            return
        }
    }
    Stop-WithError 'L''API ne répond pas après 2 minutes. Regarde les erreurs dans sa fenêtre.'
}

function Get-LockHash {
    return (Get-FileHash (Join-Path $MobileDir 'package-lock.json') -Algorithm SHA256).Hash
}

# Réinstalle quand package-lock.json a changé depuis la dernière installation (dépendance
# ajoutée, git pull…). On compare l'empreinte du fichier, enregistrée dans node_modules après
# chaque installation réussie : plus fiable que les dates, car npm réécrit parfois le fichier
# sans le changer.
function Install-MobileDependencies {
    Write-Step 'Dépendances mobiles'
    $nodeModules = Join-Path $MobileDir 'node_modules'
    $stamp = Join-Path $nodeModules '.anti-gaspi-lock-hash'

    if ((Test-Path $stamp) -and ((Get-Content $stamp -Raw).Trim() -eq (Get-LockHash))) {
        Write-Ok 'Dépendances à jour'
        return
    }

    if (Test-Path $nodeModules) {
        Write-Info 'package-lock.json a changé depuis la dernière installation : mise à jour...'
    }
    Push-Location $MobileDir
    try {
        Invoke-Checked 'npm install' { npm install }
    }
    finally {
        Pop-Location
    }
    # Empreinte calculée APRÈS l'installation : npm a pu retoucher package-lock.json.
    Write-TextFile $stamp @(Get-LockHash)
    Write-Ok 'Dépendances installées'
}

function Start-Expo([bool]$ClearCache) {
    Write-Step 'Expo'
    Write-Info 'Scanne le QR code avec l''appareil photo (iPhone) ou Expo Go (Android).'
    Write-Info 'Ctrl+C pour arrêter Expo. Pour tout arrêter ensuite : .\dev.cmd -Stop'
    Push-Location $MobileDir
    try {
        if ($ClearCache) {
            Invoke-Native { npx expo start --clear }
        }
        else {
            Invoke-Native { npx expo start }
        }
    }
    finally {
        Pop-Location
    }
}

# ---------------------------------------------------------------------------
# Modes
# ---------------------------------------------------------------------------

function Invoke-Start {
    Assert-Prerequisites
    Start-DockerIfNeeded

    Write-Step 'Configuration locale'
    Initialize-RootEnv
    Initialize-UserSecrets
    $urlChanged = Initialize-MobileEnv

    Start-Database

    # Arrêter l'ancienne API AVANT les migrations : dotnet ef recompile le projet,
    # ce qui échoue tant qu'une instance en cours d'exécution verrouille Api.exe.
    Write-Step 'Arrêt d''une éventuelle ancienne instance de l''API'
    Stop-Api

    Update-Database
    Start-Api
    Install-MobileDependencies
    Start-Expo $urlChanged
}

function Invoke-Tests {
    Assert-Prerequisites
    Start-DockerIfNeeded

    Write-Step 'Tests de l''API (Testcontainers : un PostgreSQL de test est démarré automatiquement)'
    Push-Location $ApiDir
    try {
        # Dossier de compilation séparé : les tests passent même si l'API tourne (Api.exe verrouillé).
        $artifacts = Join-Path $env:TEMP 'anti-gaspi-test-artifacts'
        Invoke-Checked 'Les tests de l''API' { dotnet test --artifacts-path $artifacts }
    }
    finally {
        Pop-Location
    }

    Install-MobileDependencies
    Write-Step 'Tests et typecheck du mobile'
    Push-Location $MobileDir
    try {
        Invoke-Checked 'Les tests du mobile' { npm test }
        Invoke-Checked 'Le typecheck du mobile' { npm run typecheck }
    }
    finally {
        Pop-Location
    }

    Write-Host "`n    Tous les tests passent." -ForegroundColor Green
}

function Invoke-Stop {
    Write-Step 'Arrêt'
    Stop-Api
    Push-Location $Root
    try {
        Invoke-Native { docker compose stop }
    }
    finally {
        Pop-Location
    }
    Write-Ok 'PostgreSQL arrêté (les données sont conservées)'
}

if ($Stop) {
    Invoke-Stop
}
elseif ($Test) {
    Invoke-Tests
}
else {
    Invoke-Start
}
