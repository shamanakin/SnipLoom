# SnipLoom Publish Script
# Creates a portable, self-contained single-file executable

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$ProjectFile = Join-Path $ProjectRoot "src\SnipLoom\SnipLoom.csproj"
$DistDir = Join-Path $ProjectRoot "dist"
$PublishDir = Join-Path $DistDir "publish"
$ZipName = "SnipLoom-$Runtime.zip"
$ZipPath = Join-Path $DistDir $ZipName

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "  SnipLoom Publish Script" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Clean previous build
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path $DistDir) {
    Remove-Item -Recurse -Force $DistDir
}
New-Item -ItemType Directory -Path $DistDir | Out-Null

# Publish
Write-Host "Publishing $Configuration build for $Runtime..." -ForegroundColor Yellow
dotnet publish $ProjectFile `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

# Find the exe
$ExePath = Get-ChildItem -Path $PublishDir -Filter "SnipLoom.exe" | Select-Object -First 1

if (-not $ExePath) {
    Write-Host "Error: SnipLoom.exe not found in publish output!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Published executable: $($ExePath.FullName)" -ForegroundColor Green
Write-Host "Size: $([math]::Round($ExePath.Length / 1MB, 2)) MB" -ForegroundColor Gray

# Create zip
Write-Host ""
Write-Host "Creating zip archive..." -ForegroundColor Yellow
Compress-Archive -Path $ExePath.FullName -DestinationPath $ZipPath -Force

Write-Host ""
Write-Host "======================================" -ForegroundColor Green
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""
Write-Host "Output: $ZipPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "Upload this zip to GitHub Releases." -ForegroundColor Gray
