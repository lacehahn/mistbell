#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
mkdir -p spike/artifacts/climb2d_art
if [[ "${1:-}" != --player-only ]]; then
 python3 spike/tools/prepare_climb2d_art.py | tee spike/artifacts/climb2d_art/processing.txt
 bash spike/tools/accept_climb2d.sh
else
 bash spike/tools/accept_climb2d.sh --player-only
fi
python3 spike/tools/verify_climb2d_art.py | tee spike/artifacts/climb2d_art/acceptance.log
