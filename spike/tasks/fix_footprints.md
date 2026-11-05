# 任务：修 P1——脚印不再生成，且验收漏报

## 现象（全流程跑出来的唯一 P1）
`-demo-trail 10` 日志：`spawns=0`，`walk_pass=*** **1**`（失败），但流程脚本仍报 `rc=0 bad=0`。
也就是：**脚印系统完全不出脚印**，而且这个失败没有被计入 bad，属于静默失败。

## 要做的
1. **查脚印为什么是 0**：
   - `FootprintTrail` 依赖贴地射线与"Paving/路面"层判定；最近路面改成了 2D 攀爬段 + S 形路带，
     3D 段的路面也换过材质与层；
   - 日志里补 `[TRAIL_DIAG] ray_hit= layer= skip_reason=`，确认是射线没命中、还是被 skip 规则全跳过。
2. **修到至少 15 个脚印**（原门槛 20–26，先看能不能回到 20 以上；若结构变了就按新的合理值设门槛并说明）。
3. **把失败计入 bad**：`-demo-trail` 的 `spawns<门槛` 必须让脚本报 `bad>0`，不能再静默通过。

## 验收
- `[TRAIL] spawns=≥20 ground_err_max≤0.02`
- `bash spike/tools/run_demos.sh "-demo-trail 10"` 输出 `bad=0`；故意把门槛调高时应能看到 `bad>0`（证明计数生效）
- 3D 行走 6 秒仍 ≈17.7 m、`bad=0`；2D 攀爬不受影响

## 硬约束
- 不要求 rebuild_all 全绿；不改 3D 地形/相机/教堂/室内/2D 场景外观。
