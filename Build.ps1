<#
.SYNOPSIS
    Baut BGInfo-Reloaded.exe und den Editor BGInfo-Reloaded-Editor.exe aus dem Quellcode.

.DESCRIPTION
    Ergebnis sind zwei eigenstaendige Dateien (self-contained, win-x64, single file) im Ordner
    .\publish_tmp. Der Ordner wird nicht geleert - eine dort liegende BGInfo-Reloaded.json samt
    Hintergrundbildern bleibt erhalten.

    Mit -SignCertThumbprint werden beide exe nach dem Bauen per Authenticode signiert (SHA-256).
    Das Zertifikat muss als Codesignatur-Zertifikat mit privatem Schluessel in CurrentUser\My
    oder LocalMachine\My liegen. Ohne den Parameter wird unsigniert gebaut.

.PARAMETER SignCertThumbprint
    Fingerabdruck des Codesignatur-Zertifikats (Leerzeichen sind erlaubt). Wird es nicht
    gefunden, bricht der Build ab.

.PARAMETER TimestampServer
    Optionaler Zeitstempeldienst (RFC 3161), z. B. http://timestamp.digicert.com. Mit Zeitstempel
    bleibt die Signatur auch nach Ablauf des Zertifikats gueltig; ohne endet sie mit ihm.

.NOTES
    Braucht das .NET SDK (8 oder neuer): https://dotnet.microsoft.com/download
    Ausfuehren mit: powershell.exe -ExecutionPolicy Bypass -File .\Build.ps1
    Signiert:       .\Build.ps1 -SignCertThumbprint <Fingerabdruck> [-TimestampServer <URL>]
#>

[CmdletBinding()]
param(
    # Standard .\publish_tmp. Nicht hier mit $PSScriptRoot vorbelegen: Windows PowerShell 5.1 laesst
    # die Variable im param-Block bei "powershell.exe -File" leer.
    [string] $OutputDirectory,
    # Nur BGInfo-Reloaded.exe bauen, den grafischen Editor weglassen.
    [switch] $SkipEditor,
    [string] $SignCertThumbprint,
    [string] $TimestampServer
)

$ErrorActionPreference = 'Stop'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot 'publish_tmp' }

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK nicht gefunden. Installieren: https://dotnet.microsoft.com/download (mindestens .NET 8)"
}

# Zertifikat vor dem Bauen suchen, damit ein Tippfehler nicht erst nach Minuten auffaellt.
$signCert = $null
if ($SignCertThumbprint) {
    $wanted = ($SignCertThumbprint -replace '[^0-9A-Fa-f]').ToUpperInvariant()
    $signCert = Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\My -CodeSigningCert |
        Where-Object { $_.Thumbprint -eq $wanted -and $_.HasPrivateKey } | Select-Object -First 1
    if (-not $signCert) {
        throw "Kein Codesignatur-Zertifikat mit privatem Schluessel und Fingerabdruck $wanted in CurrentUser\My oder LocalMachine\My gefunden."
    }
    if ($signCert.NotAfter -lt (Get-Date)) { throw "Das Zertifikat $wanted ist am $($signCert.NotAfter) abgelaufen." }
    Write-Host "Signiere mit: $($signCert.Subject) (gueltig bis $($signCert.NotAfter.ToString('yyyy-MM-dd')))" -ForegroundColor Cyan
}

function Invoke-Sign([string] $File) {
    if (-not $signCert) { return }
    $arguments = @{ FilePath = $File; Certificate = $signCert; HashAlgorithm = 'SHA256'; IncludeChain = 'NotRoot' }
    if ($TimestampServer) { $arguments.TimestampServer = $TimestampServer }
    $result = Set-AuthenticodeSignature @arguments
    if (-not $result.SignerCertificate -or $result.SignerCertificate.Thumbprint -ne $signCert.Thumbprint) {
        throw "Signieren fehlgeschlagen: $File ($($result.StatusMessage))"
    }
    # Status ist "Valid", wenn dieser Rechner der ausstellenden CA vertraut; sonst trotzdem signiert.
    Write-Host "Signiert: $(Split-Path $File -Leaf) ($($result.Status))" -ForegroundColor Green
}

Write-Host "Baue BGInfo-Reloaded.exe (self-contained, win-x64, single file)..." -ForegroundColor Cyan
dotnet publish (Join-Path $PSScriptRoot 'src\BGInfo-Reloaded.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw "Build von BGInfo-Reloaded.exe fehlgeschlagen." }
Invoke-Sign (Join-Path $OutputDirectory 'BGInfo-Reloaded.exe')
Write-Host "Fertig: $(Join-Path $OutputDirectory 'BGInfo-Reloaded.exe')" -ForegroundColor Green

if (-not $SkipEditor) {
    # Der Editor wird nur zum Bearbeiten der BGInfo-Reloaded.json gebraucht und nicht auf die Zielrechner verteilt.
    Write-Host "Baue BGInfo-Reloaded-Editor.exe (grafischer Editor fuer BGInfo-Reloaded.json)..." -ForegroundColor Cyan
    dotnet publish (Join-Path $PSScriptRoot 'editor\BGInfo-Reloaded-Editor.csproj') -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -o $OutputDirectory
    if ($LASTEXITCODE -ne 0) { throw "Build von BGInfo-Reloaded-Editor.exe fehlgeschlagen." }
    Invoke-Sign (Join-Path $OutputDirectory 'BGInfo-Reloaded-Editor.exe')
    Write-Host "Fertig: $(Join-Path $OutputDirectory 'BGInfo-Reloaded-Editor.exe')" -ForegroundColor Green
}
