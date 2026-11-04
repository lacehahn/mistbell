# far_mist_rocks 验收

整体结论：**未全通过，保留失败门槛**。

- 80 m：visible_ridges=3，mountain_luma=0.472523，sky_luma=0.434141，seam=0.003036。
- 雪盖面积 42.441280%，单块 41.833252–43.188221%；88 块石头数量、顶点与世界变换 SHA256 相等。
- 两张 PNG 暗面最低采样 L*=16.340896；立柱最大平均 b*=3.225766。
- 立柱材质 L*：50.592014 → 45.533375（×0.900011）。
- 室外 1920×1080/Metal：z01 214.69 FPS / 281 drawcalls; terrace 229.09 FPS / 306 drawcalls。
- 原揭示门槛：[REVEAL] gate=FAIL ratio=1.311026 near_dist=28 church_height_pct=0.620000 ratio20_80=1.311026 bell_once=True pulse_restored=True。没有修改判据或相机；所有试验性揭示时序调整已撤回。
- bad：{"interior": 0, "walk": 0, "reveal": 1, "capture": 0, "bench": 0}。

| 检查 | 结果 |
| --- | --- |
| build | 通过 |
| protected | 通过 |
| rock_geometry | 通过 |
| snow_caps | 通过 |
| fence_albedo_drop_10pct | 通过 |
| far_mist | 通过 |
| snow_rock_pixels | 通过 |
| fence_pixels | 通过 |
| outdoor_performance | 通过 |
| interior | 通过 |
| walk | 通过 |
| reveal | **失败** |
| bad_zero | **失败** |
| reveal_80_fresh | 通过 |
| art_runtime_fresh | 通过 |

## 变化与保护范围

局部入口 `FarMistRocksBuild.Run`，不重建场景。MistHaze 改为 0.04/0.18/0.34/0.50，山体垂直雾 0.65、亮度 0.78，天空/雾底色 #DCE4EA；山脊对局部天空的亮度比例为 0.72/0.82/0.90/0.97。修正运行时雾幕重复覆盖远山：雾幕采样场景深度，仅覆盖 250–500 m 内的近处场景，山体自行处理分层雾。该深度采样只在运行时申请，不改变保存的机位参数。

石头沿既有三角面按朝上/迎风评分覆盖约42.5%表面积，雪色 #EDF3F8；岩体/暗面独立材质补偿最低亮度。围栏只替换立柱独立冷灰材质，原雪盖与横栏未改。室内、地面、祭坛、王门、人物、全部碰撞、灯光和原机位的保护快照一致；石头位置/尺寸/顶点一致。详见 assets.json 的前后 SHA256。

## 测量口径与复现

- mountain_luma：原有屏幕 ROI x=5–95%、y=10–45% 的 PNG 编码 RGB Rec.709 均值；seam 为该 ROI 相邻列均值梯度的真正最大值。sky_luma 取顶部10%、同一横向范围。
- 山脊：从原山体 PNG alpha=0.5 导出真实世界轮廓，在原80m机位投影；排除被近层覆盖的轮廓及中央32–68%教堂区域。每层至少20个样本，沿轮廓上下3px、横向5px均值差的中位数≥0.008才记可见。全部样本保存在 measurements.json。
- 雪盖百分比为三维三角面表面积；暗面与立柱用960×540首命中网格射线掩码，采样交付1920×1080 PNG。L*/b*按sRGB解码、D65 CIELAB独立复算；暗面报告采样最小L*，立柱报告采样平均b*。掩码位于 Shots/far_mist_*_mask.png。
- 性能为180帧且≥3秒预热后300帧统计，使用原PlayerBenchmark；与普通60FPS演示分开测吞吐，未改性能阈值。

完整运行：`bash spike/tools/accept_far_mist_rocks.sh`。仅复算：`python3 spike/tools/verify_far_mist_rocks.py`；任何失败返回1。

截图：[reveal_80.png](../../project/Shots/reveal_80.png)、[art_runtime.png](../../project/Shots/art_runtime.png)。原始日志为 `spike/logs/far_mist_rocks_{build,interior,walk,reveal,capture,bench}.log`，结构化结果见 [measurements.json](measurements.json)。
