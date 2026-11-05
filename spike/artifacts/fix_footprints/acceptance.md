# fix_footprints 验收记录

任务：[`spike/tasks/fix_footprints.md`](../../tasks/fix_footprints.md)。仅修脚印与其验收漏报；未修改场景、地形、材质、相机、教堂、室内、2D 实现或 AGENTS.md。

## 根因与修复

- [修复前实跑](before.log)：39 次 `[TRAIL_DIAG] ray_hit=true layer=0 skip_reason=paving_or_interior surface=Paving_Surface`，`spawns=0`。射线命中正常；旧演示已随默认出生点和 S 形路带改为全程走石板，触发合理的铺装跳过规则。
- 移除 `FootprintTrail` 将接触位置吸回路带中心的逻辑；脚印按实际位移与左右脚偏移落地。仅 `-demo-trail` 走现有雪肩（中心线 x 偏移 +2.1 m），普通行走演示不变；石板和室内仍不生成脚印。
- 雪肩验证发现全场景射线会击中 `FM4_Batch` 护栏，误报地面高差。脚印改用角色移动已有的 `SurfaceAt` 可行走表面射线查询，不把护栏等装饰碰撞体当成地面。顶点射线缺失直接跳过，不以中心高度伪造贴地。
- `ground_err_max` 记录生成顶点与采样地面高度之差，包含防止 Z-fighting 的 0.012 m 抬升；地面高差单独保留为 `ground_span_max`。不是把坡面高差当成贴地误差。
- 玩家终止 gate 与脚本默认数量门槛均为 20，未降低。截图后统一记录终止汇总，避免 LateUpdate 和减速尾程导致两条终止日志数量不同。
- `run_demos.sh` 校验数量、贴地误差、行走通过、终止 gate 和两条日志数量一致；缺日志/字段也计入 `bad`。`TRAIL_MIN_SPAWNS` 仅用于脚本门槛注入，不能绕过播放器自身的 20 个门槛。

## 实跑结果

| 验收 | 数字与判据 | 证据 |
|---|---|---|
| 脚印 10 秒 | spawns=38；despawns=0；ground_err_max=0.0120 m；ground_span_max=0.0700 m；uneven_skipped=0；static_kept=64；gate=pass；rc=0 bad=0；脚本 exit=0 | [日志](trail.log)、[汇总](trail_summary.txt)、[俯视截图](footprints_trail_top.png) |
| 故意提高脚本门槛至 1000 | 实际 spawns=38；bad=1；FAIL=1；脚本 exit=1 | [日志](high_threshold.log)、[汇总](high_threshold_summary.txt) |
| 3D 行走 6 秒 | distance=17.747 m；ground_error_max=0.000000 m；raycast_misses=0；samples=360 bad=0；fps_ok=true；脚本 rc=0 bad=0 | [日志](walk.log)、[行走脚本汇总](regression_900_summary.txt) |
| 2D 攀爬全程（1920×1080） | total_s=51.011；stairs_s=7.000；fade_s=0.646；support_error_px=0.000031；5 张 1920×1080 截图；arrival valid=True bad=0；transition=ok | [日志](climb2d_1080.log)、[截图目录](climb2d/) |
| 构建和静态检查 | Unity result=Succeeded errors=0；bash -n 通过；git diff --check 通过 | [构建日志](../../logs/fix_footprints_build.log) |

10 秒雪肩累计位移 28.76 m，按 0.75 m 间距生成 38 个，符合距离计算；保留任务要求 ≥20 的门槛。普通 6 秒行走仍在石板上，spawns=0、paving_skipped=23 是预期规则，不以此冒充雪面验收。

补充检查直接执行脚本中的日志解析器，正常、数量不足、终止日志缺失、walk_pass=false、误差超限、零脚印 6 项均符合预期：[结果](gate_tests.txt)。

2D 首次由通用脚本以 1600×900 启动，5 次截图尺寸断言令内部 bad=5；该通用脚本未识别 2D 的失败终止标记。已保留[首跑日志](climb2d_900.log)，并按 2D 规定分辨率独立重跑、断言真实终止判据及 5 张 PNG 尺寸通过。此任务仅补脚印验收，未扩展修改 2D 验收脚本。

## 复跑命令

在仓库根目录执行。专用构建入口仅构建已有两个场景，不重建或保存场景。

```bash
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "$PWD/spike/project" -executeMethod FootprintAcceptanceBuild.Run -logFile "$PWD/spike/logs/fix_footprints_build.log"
bash spike/tools/run_demos.sh "-demo-trail 10"
TRAIL_MIN_SPAWNS=1000 bash spike/tools/run_demos.sh "-demo-trail 10" # 预期 exit=1、bad=1
bash spike/tools/run_demos.sh "-demo-walk 6"
```

2D 使用 `open -n -a` 前台启动，等待播放器完成后检查日志中的 `arrival ... bad=0` 与 `transition=ok`，不以 `open` 的退出码作为游戏验收结果：

```bash
caffeinate -dimu -t 180 &
open -n -a "$PWD/spike/project/Builds/Mac/Mistbell.app" --args -demo-climb2d -climb2d-auto -climb2d-output "$PWD/spike/artifacts/fix_footprints/climb2d" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$PWD/spike/artifacts/fix_footprints/climb2d_1080.log"
```
