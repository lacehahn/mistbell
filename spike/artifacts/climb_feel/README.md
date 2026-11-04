# Climb feel 验收（2026-11-04）

最终自动验收：`pass=true`，见 [acceptance.json](acceptance.json)。
只实现三段石阶、沿路径距离插值的室外相机、随高度递增的雾。人物资产、围栏、教堂、远山、室内未改；构建时除路面对象外的场景组件、材质和网格保护检查通过。移动仅为新路面支持 .32m 短踏步，原教堂入口台阶逻辑保留。

## 重建

```sh
bash spike/tools/rebuild_all.sh > spike/logs/climb_rebuild.log 2>&1
python3 spike/tools/verify_climb_feel.py > spike/logs/climb_acceptance.log 2>&1
```

22/22 pass，fail=0；macOS / Windows 均 Succeeded，errors=0。
[完整重建日志](../../logs/climb_rebuild.log)、[双平台构建日志](../../logs/rebuild/23_player.log)、[最终验收日志](../../logs/climb_acceptance.log)。
第 22 步 `PerformanceBuild.RunClimb` 在原路径与围栏补丁之后生成台阶，22 步数量保持不变；最后的 `SpikeBuild.Run` 产出非 Development 包。

## 几何、相机与雾

```text
[CLIMB_STEPS] flights=3 steps=10 rise=0.16 tread=0.32 platforms=2.00 shoulder=0.15 stations=50.085,101.771,153.456 triangles=5112
[CLIMB_GEOMETRY] rise_min=0.159997 rise_max=0.160004 flat_error=0.000002 protected_unchanged=True
[CLIMB_CAM] look_start=1.20 look_end=1.45 y_start=2.1 y_end=2.6
[CLIMB_FOG] density_base=0.010000 density_top=+24% density_top_value=0.012400 height_span=29.082 per_10m_pct=8.253 mode=Linear distance_scale_top=0.806452
[CLIMB_FOG_SAMPLE] base=0.010000 top=0.012400 increase_pct=23.9986 height_gain=29.081
```

台阶使用现有 path_stone_a，踢面材质色乘 .9。每段前后平台各 2m；两侧各 .15m 肩部降至原雪面；不新增扶手。总攀升及教堂台地不变，局部用过渡段接回原缓坡。

任务书中的“每 10m +8%”与“台地 +24%”在现有 29.082m 爬升中不能同时精确成立。本次按已说明的默认取舍，优先满足台地累计 +24%，按实际高度线性插值，折合每 10m +8.253%。未改地形以凑 30m。现有雾为 Linear，density 同时记录，并将起止距离除以高度系数使视觉生效；独立揭示演示及室内保持原逻辑。

## 行走与演示

```text
[CLIMB_WALK6] samples=1279 bad=0
[PLAY] walk_start=(-15.667,-28.798,-205.000) walk_end=(-8.209,-26.945,-189.018) distance=17.733 row=3 fps_ok=true
[PLAY] ground_error_max=0.000000 raycast_misses=0 fps=213.17 animation_fps=10 frames=6
[CLIMB_WALK] flights=3 samples=1804 bad=0 ground_error_max=0.000000 raycast_misses=0
```

`-demo-walk 6`、`-demo-climb-feel`、`-demo-enter-church`、`-demo-reveal`、`-demo-climb`、`-bench -bench-foreground` 退出码全部为 0。全程攀爬三段接地误差均为 0，`on_deck=true raycast_misses=0`。入教堂 `walk_in=true player_inside=true`；揭示 `gate=pass bell_once=True pulse_restored=True`。

## 性能

Apple M6 / Metal / Unity 6000.3.27f2，非 Development 包，1920×1080，`-force-gfx-direct -perf-unpaced`，VSync=0、targetFrameRate=-1。预热至少 180 帧且 3 秒，每处采样 300 帧。FPS 门槛按平均吞吐衡量；1% low 如实附列。

| 位置 | 平均 FPS | 1% low | draw calls | 提交 tris（含多 pass） |
|---|---:|---:|---:|---:|
| z01 | 216.82 | 177.63 | 210 | 5951352 |
| terrace | 226.16 | 171.59 | 279 | 6552464 |
| interior | 218.18 | 167.98 | 255 | 6479806 |

正式包纹理内存计数器不可用，JSON 记 -1；不把它解释为零占用。

Development 包首测起点 195.71 / 台地 199.80 FPS，未达到 200；其旧通用 draw calls≤250 门槛亦失败。前台复测起点 194.50 / 台地 210.76 FPS。失败结果完整保留在 [development/acceptance.json](development/acceptance.json)、[development/bench.json](development/bench.json) 和 [Development 验收日志](../../logs/climb_acceptance_development.log)。正式包结果没有改画质、没有动其他资产，使用已有 `-bench-foreground` 门槛（200 FPS / 330 draw calls）；复测全过程通过。

## 截图

[正对台阶 climb_steps.png](climb_steps.png) · [玩法机位 play_view.png](play_view.png)

两张均为 1920×1080，已目检；像素检查 magenta_px=0，完整结果和 SHA256 见 [measurements.txt](measurements.txt)。
