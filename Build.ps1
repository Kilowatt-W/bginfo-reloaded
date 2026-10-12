<#
.SYNOPSIS
    Baut BGInfo-Reloaded.exe und den Editor BGInfo-Reloaded-Editor.exe aus dem Quellcode.

.DESCRIPTION
    Ergebnis sind zwei eigenstaendige Dateien (self-contained, win-x64, single file) im Ordner
    .\publish_tmp. Der Ordner wird nicht geleert - eine dort liegende BGInfo-Reloaded.json samt
    Hintergrundbildern bleibt erhalten.

.NOTES
    Braucht das .NET SDK (8 oder neuer): https://dotnet.microsoft.com/download
    Ausfuehren mit: powershell.exe -ExecutionPolicy Bypass -File .\Build.ps1
#>

[CmdletBinding()]
param(
    [string] $OutputDirectory = (Join-Path $PSScriptRoot 'publish_tmp'),
    # Nur BGInfo-Reloaded.exe bauen, den grafischen Editor weglassen.
    [switch] $SkipEditor
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK nicht gefunden. Installieren: https://dotnet.microsoft.com/download (mindestens .NET 8)"
}

Write-Host "Baue BGInfo-Reloaded.exe (self-contained, win-x64, single file)..." -ForegroundColor Cyan
dotnet publish (Join-Path $PSScriptRoot 'src\BGInfo-Reloaded.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw "Build von BGInfo-Reloaded.exe fehlgeschlagen." }
Write-Host "Fertig: $(Join-Path $OutputDirectory 'BGInfo-Reloaded.exe')" -ForegroundColor Green

if (-not $SkipEditor) {
    # Der Editor wird nur zum Bearbeiten der BGInfo-Reloaded.json gebraucht und nicht auf die Zielrechner verteilt.
    Write-Host "Baue BGInfo-Reloaded-Editor.exe (grafischer Editor fuer BGInfo-Reloaded.json)..." -ForegroundColor Cyan
    dotnet publish (Join-Path $PSScriptRoot 'editor\BGInfo-Reloaded-Editor.csproj') -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -o $OutputDirectory
    if ($LASTEXITCODE -ne 0) { throw "Build von BGInfo-Reloaded-Editor.exe fehlgeschlagen." }
    Write-Host "Fertig: $(Join-Path $OutputDirectory 'BGInfo-Reloaded-Editor.exe')" -ForegroundColor Green
}
