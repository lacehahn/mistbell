#!/bin/bash
set -eu
cd "$(dirname "$0")/../.."
ROOT="$PWD"
UNITY=/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity
"$UNITY" -batchmode -quit -projectPath "$ROOT/spike/project" -executeMethod Style11Atlas.VerifyAndBuild -logFile "$ROOT/spike/logs/style11_perf_build.log" > /dev/null 2>&1
caffeinate -dimu -t 600 sleep 600 &
CAFF=$!
trap 'kill "$CAFF" 2>/dev/null || true' EXIT
pkill -x Mistbell 2>/dev/null || true
sleep 1
open -n -a "$ROOT/spike/project/Builds/Mac/Mistbell.app" --args -bench -bench-foreground -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -bench-output "$ROOT/spike/logs/style11_bench.json" -logFile "$ROOT/spike/logs/style11_bench.log"
for _ in $(seq 1 180); do pgrep -x Mistbell >/dev/null || break; sleep 1; done
if pgrep -x Mistbell >/dev/null; then pkill -x Mistbell; exit 124; fi
grep -aE '\[BENCH(_INFO|_GATE)?\]' "$ROOT/spike/logs/style11_bench.log"
grep -aq 'BENCH_GATE] pass=True' "$ROOT/spike/logs/style11_bench.log"
python3 spike/tools/measure_style11.py --bench spike/logs/style11_bench.json
