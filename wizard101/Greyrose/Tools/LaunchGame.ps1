# Launch WizardGraphicalClient against Greyrose login (patch bypass).
# Must run from the client Bin folder so PatchConfig.xml and asset paths resolve.

$clientRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..\Wizard101 April of 2019")
$binDir = Join-Path $clientRoot "Bin"
$exe = Join-Path $binDir "WizardGraphicalClient.exe"
$rootWad = Join-Path $clientRoot "Data\GameData\Root.wad"

if (-not (Test-Path $exe)) {
    Write-Error "Client not found: $exe"
    exit 1
}

if (-not (Test-Path (Join-Path $binDir "PatchConfig.xml"))) {
    Write-Error "PatchConfig.xml not found in $binDir. Do not run WizardGraphicalClient.exe from Explorer without setting the working directory to Bin."
    exit 1
}

if (-not (Test-Path $rootWad)) {
    Write-Error @"
Missing game data: $rootWad

WizardGraphicalClient loads WizardMessages.xml (and GameMessages.xml, etc.) from the WAD
packages under Data\GameData. This install currently has no .wad files there, so a direct
launch will always fail with "Could not load WizardMessages.xml".

Restore Root.wad and the other packages listed in LocalPackagesList.txt, or use the
normal launcher and complete patching first.
"@
    exit 1
}

Push-Location $binDir
try {
    Write-Host "Working directory: $binDir"
    Write-Host "Launching WizardGraphicalClient -> login.us.wizard101.com:12000"
    & $exe "-L" "login.us.wizard101.com" "12000"
}
finally {
    Pop-Location
}
