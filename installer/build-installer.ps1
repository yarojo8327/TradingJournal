# ══════════════════════════════════════════════════════════════════════════
#  TradingJournal — Script de construcción del instalador beta
#  Uso: .\build-installer.ps1 [-Version "0.2.0"] [-SkipPublish]
# ══════════════════════════════════════════════════════════════════════════

param(
    [string]$Version    = "0.1.0",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$Root       = Split-Path $PSScriptRoot -Parent
$PublishDir = "$PSScriptRoot\publish"
$OutputDir  = "$PSScriptRoot\output"
$IssScript  = "$PSScriptRoot\TradingJournal.iss"
$IsccExe    = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$Project    = "$Root\src\Application.WPF\Application.WPF.csproj"

Write-Host ""
Write-Host "═══════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  TradingJournal Installer Builder" -ForegroundColor Cyan
Write-Host "  Versión: $Version Beta" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# ── 1. Verificar Inno Setup ───────────────────────────────────────────────
if (-not (Test-Path $IsccExe)) {
    Write-Error "Inno Setup no encontrado en: $IsccExe`nDescargue desde https://jrsoftware.org/isdl.php"
    exit 1
}

# ── 2. Publicar la aplicación ─────────────────────────────────────────────
if (-not $SkipPublish) {
    Write-Host "▶ Publicando aplicación (self-contained, win-x64)..." -ForegroundColor Yellow

    if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }

    dotnet publish $Project `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishReadyToRun=true `
        -o $PublishDir

    if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish falló"; exit 1 }

    $fileCount = (Get-ChildItem $PublishDir -Recurse -File).Count
    Write-Host "   ✓ $fileCount archivos publicados" -ForegroundColor Green
}
else {
    Write-Host "⏭  Saltando publicación (--SkipPublish)" -ForegroundColor DarkYellow
}

# ── 3. Actualizar versión en el script .iss ──────────────────────────────
Write-Host "▶ Actualizando versión en TradingJournal.iss..." -ForegroundColor Yellow
$issContent = Get-Content $IssScript -Raw
$issContent = $issContent -replace '#define AppVersion\s+"[^"]+"', "#define AppVersion   `"$Version`""
Set-Content $IssScript $issContent -Encoding UTF8
Write-Host "   ✓ Versión actualizada a $Version" -ForegroundColor Green

# ── 4. Crear directorio de salida ────────────────────────────────────────
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory $OutputDir | Out-Null }

# ── 5. Compilar instalador ────────────────────────────────────────────────
Write-Host "▶ Compilando instalador con Inno Setup..." -ForegroundColor Yellow
& $IsccExe $IssScript

if ($LASTEXITCODE -ne 0) { Write-Error "ISCC.exe falló"; exit 1 }

# ── 6. Resultado ─────────────────────────────────────────────────────────
$installer = Get-ChildItem "$OutputDir\*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host ""
Write-Host "═══════════════════════════════════════════" -ForegroundColor Green
Write-Host "  ✅ INSTALADOR GENERADO" -ForegroundColor Green
Write-Host "  Archivo: $($installer.Name)" -ForegroundColor White
Write-Host "  Tamaño:  $([math]::Round($installer.Length / 1MB, 1)) MB" -ForegroundColor White
Write-Host "  Ruta:    $($installer.FullName)" -ForegroundColor White
Write-Host "═══════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
