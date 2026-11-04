# far_mist_v2 验收

**未全通过。**

阻塞项：80m原机位实际几何视高0.411735，要求≤0.30。任务同时禁止改变距离/FOV/高度；本轮保留原机位，不把测量改成可见像素或放宽阈值。reveal因此gate=FAIL、bad=1，不能宣称三项演示全绿。

```text
[REVEAL] far_h_pct=0.411735 far_contrast=0.042502 near_h_pct=0.620000 gate=FAIL
[ROCK_SHAPE] aspect=0.420000 buried=0.399999..0.400001 dark_face_L=23.971725 count=88 layout_unchanged=true
[PATH] view=reveal_80 luma=54.765097 b_star=-0.192417 edge_L=65.722796 snow_edge=true
[PATH] view=art_runtime luma=52.289078 b_star=-1.011916 edge_L=61.183276 snow_edge=true
[BENCH] pos=z01 fps=228.15 drawcall=285
[BENCH] pos=terrace fps=225.52 drawcall=355
[BENCH] pos=interior fps=216.15 drawcall=250
[WALK] distance=15.395 ground_error_max=0.000000 raycast_misses=0
bad={"interior": 0, "walk": 0, "reveal": 1, "capture": 0, "bench": 0}
pass=False
```

| 检查 | 结果 |
| --- | --- |
| build | 通过 |
| protected | 通过 |
| rock_count_layout | 通过 |
| rock_aspect | 通过 |
| rock_burial | 通过 |
| rock_dark_face | 通过 |
| path_color | 通过 |
| path_snow_edge | 通过 |
| far_mist_preserved | 通过 |
| far_height | 失败 |
| far_contrast | 通过 |
| near_frame | 通过 |
| reveal_demo | 失败 |
| interior | 通过 |
| walk | 通过 |
| outdoor_performance | 通过 |
| bad_zero | 失败 |
| reveal_80_fresh | 通过 |
| reveal_80_material_ok | 通过 |
| reveal_28_fresh | 通过 |
| reveal_28_material_ok | 通过 |
| art_runtime_fresh | 通过 |
| art_runtime_material_ok | 通过 |

局部修改入口 `FarMistV2Build.Run`，不重建场景。88块石头保留物体变换矩阵，仅改网格世界Y方向，最高宽高比0.42，按实际雪地/台地碰撞表面埋入40%。背光面使用独立材质的环境补光下限。路面和雪边使用独立材质；雪边网格批为一个renderer，不增加碰撞。

保护摘要涵盖非石头/路面的渲染器、网格、材质、灯光、全部碰撞体、保存的相机、人物控制器与ShotRig。前后SHA一致；未重建或覆盖原有工作区修改。

测量口径：石头形态来自世界空间网格包围盒；埋雪比例=(雪面Y-网格底Y)/总高。暗面最低L*、路面平均L*/b*、雪边平均L*均从交付1080p PNG的960×540首命中几何掩码独立复算，sRGB解码、D65 CIELAB。L*不是0–1的Rec.709亮度。雪边宽0.1m，线性反照率比路面高15%；雾与景深后实际L*另行报告，不混淆反照率和显示亮度。

教堂视高来自实际顶点投影的上下界；对比度为真实三角面投影的逐行左右轮廓，内外各4px的PNG Rec.709亮度差绝对值均值，每2px取样，独立复算全部`Shots/reveal_contrast.csv`采样。ratio20_80保留作诊断，注释说明已与远山可见性冲突，不再作门槛。近景额外检查完整入画与基座无遮挡。

室外性能按原PlayerBenchmark的180帧且≥3秒预热、300帧采样，在1920×1080/Metal下测量；本任务用室外≥200FPS/≤400drawcalls验收，不使用通用bench的250或旧foreground的330上限，未修改基准测试脚本；因此原BENCH_GATE仍可能False，本任务性能结论由此脚本按任务书独立判定。

复现：`bash spike/tools/accept_far_mist_v2.sh`；仅复算：`python3 spike/tools/verify_far_mist_v2.py`，任何未过项返回1。原始日志：`spike/logs/far_mist_v2_{build,interior,walk,reveal,capture,bench}.log`。

截图：[reveal_80.png](../../project/Shots/reveal_80.png)、[reveal_28.png](../../project/Shots/reveal_28.png)、[art_runtime.png](../../project/Shots/art_runtime.png)。全部数字与摘要见[measurements.json](measurements.json)。
