# DesktopTodo build script - compiles the app with the .NET Framework in-box C# compiler (no external deps)
# Usage: powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$fw   = Join-Path $env:windir 'Microsoft.NET\Framework64\v4.0.30319'
$csc  = Join-Path $fw 'csc.exe'

if (-not (Test-Path $csc)) {
    $fw  = Join-Path $env:windir 'Microsoft.NET\Framework\v4.0.30319'
    $csc = Join-Path $fw 'csc.exe'
}

# Resolve a framework DLL: prefer the framework folder, then search the GAC
function Resolve-FxDll([string]$name) {
    $p = Join-Path $fw ($name + '.dll')
    if (Test-Path $p) { return $p }
    $gac = Get-ChildItem (Join-Path $env:windir 'Microsoft.NET\assembly') -Recurse -Filter ($name + '.dll') -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\.resources' } |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $gac) { throw ("Assembly not found: " + $name) }
    return $gac
}

$refs = @(
    (Resolve-FxDll 'PresentationCore'),
    (Resolve-FxDll 'PresentationFramework'),
    (Resolve-FxDll 'WindowsBase'),
    (Resolve-FxDll 'System.Xaml'),
    (Resolve-FxDll 'System.Windows.Forms'),
    (Resolve-FxDll 'System.Drawing'),
    (Resolve-FxDll 'System.Web.Extensions')
)
$refArgs = $refs | ForEach-Object { ('/r:"' + $_ + '"') }

$sources = Get-ChildItem (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object { ('"{0}"' -f $_.FullName) }
if (-not $sources -or @($sources).Count -eq 0) { throw 'No source files found under src\' }

$outExe = Join-Path $root 'DesktopTodo.exe'

# Embed the app icon (assets\app.ico) as the Win32 icon when present
$ico = Join-Path $root 'assets\app.ico'
$icoArgs = @()
if (Test-Path $ico) { $icoArgs = @('/win32icon:"' + $ico + '"') }

& $csc /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /out:"$outExe" @refArgs @icoArgs @sources

if ($LASTEXITCODE -ne 0) { throw ("csc.exe failed with exit code " + $LASTEXITCODE) }
Write-Host ("Build OK -> " + $outExe)
