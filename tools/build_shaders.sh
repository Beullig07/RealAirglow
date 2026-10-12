#!/usr/bin/env bash
# Build the shader bundles (Windows, macOS, Linux) and run the shader-vs-reference comparison.
#
#   tools/build_shaders.sh            # bundles + harness renders (DirectX 11) + Python comparison
#   tools/build_shaders.sh --bundle   # bundles only
#   tools/build_shaders.sh --glcore   # harness + comparison only, rendered with OpenGL Core
#                                     # (the shader macOS and Linux use; Metal needs a Mac)
#
# Needs Unity 2019.4.18f1 (KSP 1.12's version) with an activated licence and the "Mac Build
# Support (Mono)" and "Linux Build Support (Mono)" modules. Set UNITY to the editor executable
# if it is not at the default location below.
set -euo pipefail
REPO="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="${UNITY:-/c/Users/charl/Unity/2019.4.18f1/Editor/Unity.exe}"
PROJECT="$REPO/unity/RealAirglowShaders"

# The Unity project compiles the Unity-free core from a synced copy (Unity only sees Assets/).
mkdir -p "$PROJECT/Assets/Core"
cp "$REPO"/src/Core/*.cs "$PROJECT/Assets/Core/"

run_unity() {
  local method="$1" log="$PROJECT/$2"; shift 2
  "$UNITY" -batchmode -quit -projectPath "$PROJECT" -executeMethod "$method" -logFile "$log" "$@" || {
    echo "Unity failed; see $log"; grep -E "error|Exception" "$log" | head -20; exit 1; }
}

harness_args=()
if [ "${1:-}" = "--glcore" ]; then
  harness_args=(-force-glcore)
else
  run_unity RealAirglow.Editor.BuildBundles.Build build_bundles.log
  for os in windows macos linux; do echo "bundle: $REPO/GameData/RealAirglow/Shaders/realairglow-$os.bundle"; done
  [ "${1:-}" = "--bundle" ] && exit 0
fi

run_unity RealAirglow.Editor.ComparisonHarness.Run harness.log ${harness_args[@]+"${harness_args[@]}"}
"$REPO/reference/.venv/Scripts/python.exe" "$REPO/reference/compare_shader.py"
