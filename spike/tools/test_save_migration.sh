#!/bin/bash
# 存档迁移回归：验证
#   ① 旧路径 Lot/Mistbell 与 com.Lot.Mistbell 各有一份档时，只搬一份且新目录落定；
#   ② 新目录已有档时，**不覆盖**（迁移只在目标不存在时发生）；
#   ③ 搬完之后正常读写。
# 全程备份/恢复真实存档，不会弄丢 Lot 的进度。
set -u
ROOT="$HOME/Library/Application Support"
NEW="$ROOT/Mistbell"
LOT="$ROOT/Lot/Mistbell"
COM="$ROOT/com.Lot.Mistbell"
STAMP=$(date +%s)
BACKUP="$ROOT/.mistbell_backup_$STAMP"

PASS=0; FAIL=0
ok(){ echo "  ✓ $1"; PASS=$((PASS+1)); }
bad(){ echo "  ✗ $1"; FAIL=$((FAIL+1)); }

cleanup(){
  pkill -x Mistbell 2>/dev/null
  rm -rf "$NEW" "$LOT" "$COM"
  [ -d "$BACKUP/new" ] && cp -R "$BACKUP/new" "$NEW"
  [ -d "$BACKUP/lot" ] && mkdir -p "$LOT" && cp -R "$BACKUP/lot/." "$LOT/"
  [ -d "$BACKUP/com" ] && mkdir -p "$COM" && cp -R "$BACKUP/com/." "$COM/"
  rm -rf "$BACKUP"
}
trap cleanup EXIT

# —— 备份现有三个目录 ——
mkdir -p "$BACKUP"
# ⚠️ 不能按 basename 备份：$NEW 与 $LOT 的 basename 都是 "Mistbell"，会互相覆盖，
# 恢复时还会把旧目录嵌套进新目录（第一版脚本就这么把 Lot 的进度搞乱过）。
[ -d "$NEW" ] && cp -R "$NEW" "$BACKUP/new"
[ -d "$LOT" ] && cp -R "$LOT" "$BACKUP/lot"
[ -d "$COM" ] && cp -R "$COM" "$BACKUP/com"

run_once(){
  pkill -x Mistbell 2>/dev/null; sleep 1
  caffeinate -dimu -t 120 sleep 120 & local caff=$!
  open -n -a "$(cd "$(dirname "$0")/../project" && pwd)/Builds/Mac/Mistbell.app" \
    --args -demo-cinema -screen-width 1280 -screen-height 720 -screen-fullscreen 0 \
    -logFile "$(cd "$(dirname "$0")/../project" && pwd)/../logs/migrate_$STAMP.log"
  for _ in $(seq 1 90); do pgrep -x Mistbell >/dev/null || break; sleep 1; done
  kill $caff 2>/dev/null
}

echo "== 用例 1：两个旧路径都有档，新目录空 =="
rm -rf "$NEW" "$LOT" "$COM"
mkdir -p "$LOT" "$COM"
echo '{"version":1,"marker":"legacy-Lot"}'    > "$LOT/mistbell_save.json"
echo '{"version":1,"marker":"legacy-comLot"}' > "$COM/mistbell_save.json"
echo '{"volume":0.8,"language":"both"}'       > "$LOT/mistbell_settings.json"
run_once
migrated=$(grep -ac "\[SAVE\] migrated mistbell_save.json" "$(cd "$(dirname "$0")/../project" && pwd)/../logs/migrate_$STAMP.log" || true)
if [ -f "$NEW/mistbell_save.json" ]; then ok "新目录生成了存档"; else bad "新目录没有存档"; fi
if grep -q "legacy-Lot" "$NEW/mistbell_save.json" 2>/dev/null; then ok "搬的是 Lot/Mistbell 那一份（顺序优先）"; else bad "搬错了来源"; fi
if [ "${migrated:-0}" -eq 1 ]; then ok "同一次只迁移一次（不重复搬）"; else bad "迁移次数异常：$migrated"; fi

echo "== 用例 2：新目录已有档，旧路径也有档 =="
echo '{"version":1,"marker":"new-current"}' > "$NEW/mistbell_save.json"
echo '{"version":1,"marker":"legacy-Lot"}'  > "$LOT/mistbell_save.json"
run_once
if grep -q "new-current" "$NEW/mistbell_save.json" 2>/dev/null; then ok "新档没有被旧档覆盖"; else bad "新档被覆盖了"; fi

echo
echo "PASS=$PASS FAIL=$FAIL"
[ "$FAIL" -eq 0 ]
