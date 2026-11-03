只做一件事：让**脚印跟着玩家走**（现在雪地上是 64 个写死的装饰脚印，玩家走过不留痕）。不改美术、不改地形、不改玩法数值。

必读：`SpikeScene.cs` 的 `BuildFootprints()`（现有 64 个静态脚印）、`PlayerController.cs`（移动/动画时钟）。

【要改】
1) 新增 `FootprintTrail.cs`：
   - 玩家每移动 **0.75 m**（按累计位移，不按时间）在脚下生成 1 个脚印贴片（0.3×0.5 m 椭圆，左右脚交替偏移 ±0.18 m）。
   - 贴地：从脚下向上 2 m 射线向下取地面高度 +0.012 m；射线失败则跳过（不报错）。
   - 上限 **120 个**，超出后回收最早的（环形缓冲，复用贴片，避免 GC）。
   - 踩在石板上（材质名含 `Paving`）时**不生成**脚印。
2) 现有 64 个静态脚印：**保留**（作为"来过的人"的叙事痕迹），但降低不透明度到 0.55，与玩家新脚印区分。
3) 音效联动：每一步脚印触发一次 `PlayFootstep()`（现在按动画帧触发，改为按位移触发更自然，保留动画帧调用但去重）。

【自检打印】
"[TRAIL] spacing=0.75m max=120 spawns=<n> despawns=<n> ground_err_max=<f> paving_skipped=<n> static_kept=64"
硬门槛：走 18 m 后 `spawns ∈ [20, 26]`、`ground_err_max ≤ 0.02`、`despawns = 0`（未超上限）、`paving_skipped ≥ 1`（路线经过石板）。

【必须自己跑】
`-demo-walk 6`（distance ≥ 15 m，且 TRAIL 指标达标）、`-demo-walk-stairs`、`-demo-enter-church`。
截图：`Shots/footprints_trail.png`（走完后回看脚印串，用 `-demo-trail` 生成）。
回归：`-demo-cinema`、`-demo-reveal`、`capture_runtime.sh`、`measure_shot.py`（magenta_px=0）。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改美术/地形/玩法数值/相机/measure_shot.py。
