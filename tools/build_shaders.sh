#!/usr/bin/env bash
# Build the DX11 shader bundle and run the shader-vs-reference comparison harness.
#
#   tools/build_shaders.sh            # bundle + harness renders + Python comparison
#   tools/build_shaders.sh --bundle   # bundle only
#
# Needs Unity 2019.4.18f1 (KSP 1.12's version) with an activated licence. Set UNITY to the
# editor executable if it is not at the default location below.
set -euo pipefail
REPO="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY:-/c/Users/charl/Unity/2019.4.18f1/Editor/Unity.exe}"
PROJECT="$REPO/unity/RealAirglowShaders"

# The Unity project compiles the Unity-free core from a synced copy (Unity only sees Assets/).
mkdir -p "$PROJECT/Assets/Core"
cp "$REPO"/src/Core/*.cs "$PROJECT/Assets/Core/"

run_unity() {
  local method="$1" log="$PROJECT/$2"
  "$UNITY" -batchmode -quit -projectPath "$PROJECT" -executeMethod "$method" -logFile "$log" || {
    echo "Unity failed; see $log"; grep -E "error|Exception" "$log" | head -20; exit 1; }
}

run_unity RealAirglow.Editor.BuildBundles.Build build_bundles.log
echo "bundle: $REPO/GameData/RealAirglow/Shaders/realairglow-windows.bundle"
[ "${1:-}" = "--bundle" ] && exit 0

run_unity RealAirglow.Editor.ComparisonHarness.Run harness.log
"$REPO/reference/.venv/Scripts/python.exe" "$REPO/reference/compare_shader.py"
