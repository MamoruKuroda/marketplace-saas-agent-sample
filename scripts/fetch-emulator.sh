#!/bin/sh
# azd prepackage hook (Linux / macOS).
# Fetches the Microsoft Commercial Marketplace SaaS API Emulator source (pinned to a reviewed
# commit) into ./.emulator-src so azd can build & deploy it as the "emulator" App Service.
# Kept out of git (see .gitignore) — it's upstream source, pulled on demand.
set -e

repo='https://github.com/microsoft/Commercial-Marketplace-SaaS-API-Emulator.git'
commit='bb7bc6317128605b2f777ebe1c9969198733ae85'
dir="$(dirname "$0")/../.emulator-src"

if [ ! -d "$dir/.git" ]; then
  echo "Fetching emulator source into .emulator-src ..."
  git clone --quiet "$repo" "$dir"
fi
git -C "$dir" fetch --quiet origin "$commit" 2>/dev/null || true
git -C "$dir" checkout --quiet "$commit"
echo "Emulator source ready at commit $commit."
