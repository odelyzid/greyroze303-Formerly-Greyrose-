# wizCrawl303 Submodule Setup Script
# Run this after creating the wizCrawl303 repo on GitHub.
#
# Prerequisites:
#   1. Create a GitHub repo for wizCrawl303 (e.g., anomalyco/wizCrawl303)
#   2. You have push access to both repos
#
# Usage:
#   .\scripts\setup-submodules.ps1 -WizCrawlRepo "git@github.com:anomalyco/wizCrawl303.git"

param(
    [Parameter(Mandatory = $true)]
    [string]$WizCrawlRepo
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

Write-Host "=== wizCrawl303 Submodule Setup ===" -ForegroundColor Magenta

# Step 1: Push the wizCrawl303 submodule repo
Write-Host "`n[1/3] Pushing wizCrawl303 to $WizCrawlRepo ..." -ForegroundColor Cyan
Push-Location "$root\WizCrawl303"
try {
    git remote add origin $WizCrawlRepo
    git push -u origin master
    Write-Host "  ✓ wizCrawl303 pushed" -ForegroundColor Green
} catch {
    Write-Host "  ! Remote may already exist: $($_.Exception.Message)" -ForegroundColor Yellow
    git remote set-url origin $WizCrawlRepo
    git push -u origin master
}
finally { Pop-Location }

# Step 2: Update .gitmodules URL
Write-Host "`n[2/3] Updating .gitmodules URL ..." -ForegroundColor Cyan
Push-Location $root
try {
    git submodule set-url WizCrawl303 $WizCrawlRepo
    Write-Host "  ✓ .gitmodules updated" -ForegroundColor Green
} finally { Pop-Location }

# Step 3: Commit the .gitmodules change and push main repo
Write-Host "`n[3/3] Committing .gitmodules update and pushing main repo ..." -ForegroundColor Cyan
Push-Location $root
try {
    git add .gitmodules
    git commit -m "Update wizCrawl303 submodule URL to $WizCrawlRepo"
    git push
    Write-Host "  ✓ Main repo pushed" -ForegroundColor Green
} finally { Pop-Location }

Write-Host "`n=== Setup complete! ===" -ForegroundColor Magenta
Write-Host "Next steps for other developers cloning the repo:" -ForegroundColor Cyan
Write-Host "  git clone --recursive <main-repo-url>" -ForegroundColor Gray
Write-Host "  # or after a normal clone:" -ForegroundColor Gray
Write-Host "  git submodule update --init --recursive" -ForegroundColor Gray
