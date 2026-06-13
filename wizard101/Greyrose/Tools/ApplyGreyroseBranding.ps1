# Applies Greyrose303 icon to Greyrose, WizardLauncher, WGC, and wizard101.skf (Bank A/B).
param(
    [string]$ClientRoot = "d:\Wizard101_client_04_2019\Wizard101 April of 2019"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\Greyrose.csproj"

dotnet build $project -c Debug -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project $project -c Debug --no-build -- --apply-branding --client-root $ClientRoot
exit $LASTEXITCODE
