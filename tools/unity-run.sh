#!/usr/bin/env bash
# Runs a static editor method in batch mode. Close the Unity editor first.
# Usage: tools/unity-run.sh TrashPandas.EditorTools.GreyboxSceneBuilder.Build
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/TrashPandas"
VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT/ProjectSettings/ProjectVersion.txt")"
UNITY="/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"
mkdir -p "$ROOT/test-results"
LOG="$ROOT/test-results/run.log"
set +e
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" -executeMethod "$1" -logFile "$LOG"
CODE=$?
set -e
if [ "$CODE" -ne 0 ]; then tail -n 40 "$LOG"; fi
exit "$CODE"
