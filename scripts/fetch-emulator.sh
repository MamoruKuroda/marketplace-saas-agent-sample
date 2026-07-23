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

# Patch two upstream-at-this-commit issues so the ACR image build succeeds:
# 1) The Dockerfile's `npm install -g npm` now pulls an npm that requires Node >=22, but the
#    base image is Node 18 -> EBADENGINE. Node 18's bundled npm is fine, so drop that line.
# 2) Add a .dockerignore so node_modules/.git aren't uploaded as build context.
sed -i.bak 's#^[[:space:]]*RUN npm install -g npm.*## (patched) keep Node 18 bundled npm; latest npm requires Node >=22#' "$dir/docker/Dockerfile" && rm -f "$dir/docker/Dockerfile.bak"
printf 'node_modules\n.git\ndist\n' > "$dir/.dockerignore"
echo "Emulator source ready at commit $commit."
