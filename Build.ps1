# Windows Doctor — Build Script
# Requires: .NET 9 SDK  (dotnet --version should show 9.x.x)

param(
    [switch]$RunTests,
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
$SolutionFile = "$PSScriptRoot\WindowsDoctor.sln"
$UIProject    = "$PSScriptRoot\src\WindowsDoctor.UI\WindowsDoctor.UI.csproj"

function Write-Step([string]$msg) {
    Write-Host "`n==> $msg" -ForegroundColor Cyan
}

# Verify dotnet
Write-Step "Checking .NET SDK..."
$dotnetVersion = dotnet --version 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet not found. Install .NET 9 SDK from https://dotnet.microsoft.com/download"
    exit 1
}
Write-Host "dotnet $dotnetVersion"

# Restore
Write-Step "Restoring NuGet packages..."
dotnet restore $SolutionFile
if ($LASTEXITCODE -ne 0) { Write-Error "Restore failed"; exit 1 }

# Build
Write-Step "Building solution..."
dotnet build $SolutionFile -c Release --no-restore
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit 1 }

# Tests
if ($RunTests) {
    Write-Step "Running tests..."
    dotnet test "$PSScriptRoot\tests\WindowsDoctor.Tests\WindowsDoctor.Tests.csproj" -c Release --no-build --verbosity normal
    if ($LASTEXITCODE -ne 0) { Write-Warning "Some tests failed" }
}

# Publish
if ($Publish) {
    Write-Step "Publishing single-file EXE..."
    dotnet publish $UIProject `
        -c Release `
        -r win-x64 `
        --self-contained true `
        /p:PublishSingleFile=true `
        /p:EnableCompressionInSingleFile=true `
        /p:IncludeNativeLibrariesForSelfExtract=true `
        /p:PublishReadyToRun=true `
        -o "$PSScriptRoot\src\WindowsDoctor.UI\bin\publish"
    if ($LASTEXITCODE -ne 0) { Write-Error "Publish failed"; exit 1 }
    Write-Host "`n✅ Published to: $PSScriptRoot\src\WindowsDoctor.UI\bin\publish\WindowsDoctor.exe" -ForegroundColor Green
}

Write-Host "`n✅ Build complete!" -ForegroundColor Green
