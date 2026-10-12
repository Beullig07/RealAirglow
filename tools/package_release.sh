#!/usr/bin/env bash
# Package GameData/RealAirglow plus the licence files into release/RealAirglow-<version>.zip.
# Build the plugin (dotnet build -c Release) and the three shader bundles first.
set -euo pipefail
cd "$(dirname "$0")/.."
version="${1:?usage: tools/package_release.sh <version>}"
for f in GameData/RealAirglow/Plugins/RealAirglow.dll GameData/RealAirglow/Shaders/realairglow-{windows,macos,linux}.bundle \
         GameData/RealAirglow/Config/Earth.cfg GameData/RealAirglow/Icons/toolbar.png; do
  [ -f "$f" ] || { echo "missing $f" >&2; exit 1; }
done
stage="release/stage"
rm -rf "$stage" && mkdir -p "$stage/GameData/RealAirglow"
cp -r GameData/RealAirglow/{Config,Icons,Plugins,Shaders} "$stage/GameData/RealAirglow/"
cp README.md CHANGELOG.md LICENSE LICENSE-DATA.md "$stage/GameData/RealAirglow/"
out="release/RealAirglow-$version.zip"
rm -f "$out"
# Python's zipfile writes forward-slash entry names (PowerShell 5.1 Compress-Archive writes backslashes).
python -I -c "import shutil, sys; shutil.make_archive(sys.argv[1], 'zip', sys.argv[2], 'GameData')" "${out%.zip}" "$stage"
rm -rf "$stage"
echo "$out"
