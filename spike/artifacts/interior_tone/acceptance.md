# interior_tone 验收记录

仅执行 `spike/tasks/interior_tone.md`。命令：

```sh
bash spike/tools/accept_interior_tone.sh
```

最终 exit=0，全部 19 项检查通过，Unity 构建 Succeeded / errors=0。

测量：1920×1080 新 PNG，sRGB → 线性 RGB → CIELAB D65；480×270 射线查询首个可见不透明表面作为掩码。墙面使用原机位；原机位看不到天花板，使用原有抬头机位测量天花板，排除穹顶、窗与肋条；祭坛区使用王门开口内的背墙像素（归一化屏幕 x=0.48–0.52，y=0.47–0.59，自上向下），排除模型、圣像及门框。

| 区域 | L* | a* | b* | 采样点 |
|---|---:|---:|---:|---:|
| 受光墙 | 37.807 | 3.427 | 10.982 | 26561 |
| 背光墙 | 22.719 | 2.243 | 8.098 | 26561 |
| 天花板 | 16.168 | 0.474 | 6.477 | 43390 |
| 王门内侧背墙 | 29.016 | 3.485 | 11.916 | 570 |

墙面明暗差 15.088 L* ≥12；受光墙与天花板差 21.639 L* ≥14；祭坛背墙比天花板亮 12.849 L* ≥6。

砌块使用连续 ±12% 明度散布；每行随机 0.1–0.3 块宽偏移，保留隔行半块错缝；每 6 行一块 1.5 倍条石。灰缝宽 0.04 m、暗 20%。墙面局部补光在材质中按法线方向分层，未调整场景灯光。

5 根 `Interior_CeilingBeam_*` 渲染器禁用；可见横梁宽 0 m，保留原 0.030 m 细杆与 3 节链。`Interior_WallPainting` 禁用，采用任务允许的移除方案。

`-demo-enter-church` 全绿：light / domeLight / ceilingSeen / domeSeen / visible / obstacles / walked 均 True；lamps=20，shafts=3。保留会话开始时已有的 lamps=20 断言，未改亮度或碰撞门槛。

室内性能：244 drawcalls ≤300，平均 218.609 FPS ≥200，1% low=160.093 FPS（参考值）。

地板、室外、相机、祭坛模型与圣像、长椅几何与材质、王门、全部碰撞体及灯光序列化摘要前后相同，见 [protected_digests.json](protected_digests.json)。保留工作区原有未提交改动，未修改 AGENTS.md。

交付：

- [完整测量与逐项结果](measurements.json)
- [构建日志](../../logs/interior_tone_build.log)
- [入堂日志](../../logs/interior_tone_interior.log)
- [性能日志](../../logs/interior_tone_bench.log)
- [四项门槛日志与最终验收](../../logs/interior_tone_acceptance.log)
- [interior_inside.png](../../project/Shots/interior_inside.png)
- [windows_closeup.png](../../project/Shots/windows_closeup.png)
- [interior_lookup.png](../../project/Shots/interior_lookup.png)

三张交付图均为本次构建后的 1920×1080 新图，SHA-256 存于 measurements.json。脚本源码和材质补丁可复现，未安装依赖。
