#!/usr/bin/env bash
# Runs EditMode tests in the OPEN Unity editor via the Unity CLI (Pipeline package).
# Exits non-zero when any test fails or the run errors. Usage: tools/unity-test-live.sh [filter]
set -uo pipefail
cd "$(dirname "$0")/../TrashPandas"
ARGS=(--mode EditMode)
if [ $# -gt 0 ]; then ARGS+=(--filter "$1"); fi
OUT="$(~/.unity/bin/unity command --timeout 300 run_tests -- "${ARGS[@]}" 2>&1 | tail -1)"
SUMMARY="$(printf '%s' "$OUT" | cut -f3)"
echo "$SUMMARY"
case "$SUMMARY" in
  *" passed "*) n="${SUMMARY%% passed*}"; [ "${n%%/*}" = "${n##*/}" ] && exit 0 ;;
esac
printf '%s\n' "$OUT" | head -c 2000
exit 1
