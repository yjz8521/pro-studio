# Build Pro Studio into a single self-contained exe.
# Requirements: Windows + .NET Framework 4.8 (csc.exe), internet for the WebView2 SDK.
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sdkVersion = '1.0.4370.26'
$build = Join-Path $root '.build'
$refs = Join-Path $build 'refs'
$dist = Join-Path $root 'dist'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

New-Item -ItemType Directory -Force $refs, $dist | Out-Null

$core = Join-Path $refs 'Microsoft.Web.WebView2.Core.dll'
if (-not (Test-Path $core)) {
    Write-Host 'Downloading WebView2 SDK...'
    $nupkg = Join-Path $build 'webview2.zip'
    Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2/$sdkVersion" -OutFile $nupkg
    $pkg = Join-Path $build 'webview2'
    if (Test-Path $pkg) { Remove-Item $pkg -Recurse -Force }
    Expand-Archive -Path $nupkg -DestinationPath $pkg -Force
    Copy-Item (Join-Path $pkg 'lib\net462\Microsoft.Web.WebView2.Core.dll') $refs -Force
    Copy-Item (Join-Path $pkg 'lib\net462\Microsoft.Web.WebView2.WinForms.dll') $refs -Force
    Copy-Item (Join-Path $pkg 'runtimes\win-x64\native\WebView2Loader.dll') $refs -Force
}

$out = Join-Path $dist 'Pro Studio.exe'
$icon = Join-Path $root 'assets\app.ico'
$loader = Join-Path $refs 'WebView2Loader.dll'

& $csc /nologo /target:winexe /platform:x64 /codepage:65001 /warn:4 `
    "/win32icon:$icon" "/out:$out" `
    "/r:$refs\Microsoft.Web.WebView2.Core.dll" `
    "/r:$refs\Microsoft.Web.WebView2.WinForms.dll" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.dll `
    "/resource:$icon,app.ico" `
    "/resource:$refs\Microsoft.Web.WebView2.Core.dll,Microsoft.Web.WebView2.Core.dll" `
    "/resource:$refs\Microsoft.Web.WebView2.WinForms.dll,Microsoft.Web.WebView2.WinForms.dll" `
    "/resource:$loader,WebView2Loader.dll" `
    (Join-Path $root 'src\Program.cs')

if ($LASTEXITCODE -ne 0) { throw "compile failed ($LASTEXITCODE)" }
Write-Host "Built: $out"
