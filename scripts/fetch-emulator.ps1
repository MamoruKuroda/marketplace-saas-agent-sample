#!/usr/bin/env pwsh
# azd prepackage hook (Windows / pwsh).
# Fetches the Microsoft Commercial Marketplace SaaS API Emulator source (pinned to a reviewed
# commit) into ./.emulator-src so azd can build & deploy it as the "emulator" App Service.
# Kept out of git (see .gitignore) — it's upstream source, pulled on demand.
$ErrorActionPreference = 'Stop'

$repo   = 'https://github.com/microsoft/Commercial-Marketplace-SaaS-API-Emulator.git'
$commit = 'bb7bc6317128605b2f777ebe1c9969198733ae85'
$dir    = Join-Path $PSScriptRoot '..' | Join-Path -ChildPath '.emulator-src'

if (-not (Test-Path (Join-Path $dir '.git'))) {
  Write-Host "Fetching emulator source into .emulator-src ..."
  git clone --quiet $repo $dir
}
git -C $dir fetch --quiet origin $commit 2>$null
git -C $dir checkout --quiet $commit
Write-Host "Emulator source ready at commit $commit."
