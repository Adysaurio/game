#!/usr/bin/env bash
# Runs Unity EditMode tests in batch mode. Close the Unity editor first.
# Usage: tools/unity-test.sh [testFilter]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/TrashPandas"
VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT/ProjectSettings/ProjectVersion.txt")"
UNITY="/Applications/Unity/Hub/Editor/$VERSION/Unity.app/Contents/MacOS/Unity"
OUT="$ROOT/test-results"
RESULTS="$OUT/editmode.xml"
LOG="$OUT/editmode.log"
mkdir -p "$OUT"
rm -f "$RESULTS"

ARGS=(-batchmode -nographics -projectPath "$PROJECT" -runTests -testPlatform EditMode -testResults "$RESULTS" -logFile "$LOG")
if [ $# -gt 0 ]; then ARGS+=(-testFilter "$1"); fi

set +e
"$UNITY" "${ARGS[@]}"
CODE=$?
set -e

if [ ! -f "$RESULTS" ]; then
  echo "No test results produced (compile error or editor open?). Last log lines:"
  tail -n 40 "$LOG"
  exit 1
fi
grep -o '<test-run [^>]*>' "$RESULTS" | grep -oE '(total|passed|failed)="[0-9]+"' | tr '\n' ' '
echo
if [ "$CODE" -ne 0 ]; then
  grep -B1 -A6 'result="Failed"' "$RESULTS" | grep -E 'fullname=|<message>' | sed 's/^ *//' || true
fi
exit "$CODE"
