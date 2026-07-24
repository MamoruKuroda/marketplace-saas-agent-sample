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

# Patch two upstream-at-this-commit issues so the ACR image build succeeds:
# 1) The Dockerfile's `npm install -g npm` now pulls an npm that requires Node >=22, but the
#    base image is Node 18 -> EBADENGINE. Node 18's bundled npm is fine, so drop that line.
# 2) Add a .dockerignore so node_modules/.git aren't uploaded as build context.
$df = Join-Path $dir 'docker/Dockerfile'
(Get-Content $df) -replace '^\s*RUN npm install -g npm.*$', '# (patched) keep Node 18 bundled npm; latest npm requires Node >=22' | Set-Content $df -Encoding ascii
Set-Content -Path (Join-Path $dir '.dockerignore') -Value "node_modules`n.git`ndist`n" -Encoding ascii
Write-Host "Emulator source ready at commit $commit."
