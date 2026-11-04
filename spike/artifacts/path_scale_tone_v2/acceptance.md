# 路面尺度与明度 v2：未通过，所需场景基线缺失

本轮只尝试将既有 M_FT3_Paving 的 _BaseColor 从遗留的 1.08 改为任务要求的 1.06；其 _ScaleM 执行前已为 1.35。构建后自查截图发现该材质根本没有被当前场景引用，调整没有渲染效果，已恢复执行前文件。未修改着色器、相机、人物、围栏、雾、山、教堂、室内或任何几何。Assets 全部文件 SHA-256 与执行前一致，见 before_hashes.json / measurements.json。

## 阻塞证据

- 当前 HEAD：2cb9929；任务书所述回滚状态为 a28e637。Git 历史状态不能证明被忽略的生成场景也已回滚。
- `Assets/Scenes/ArtSpike.unity` 中，M_FT3_Paving 材质 GUID 引用数为 **0**，Paving_Surface 对象数为 **0**，旧 Paving_数字 路砖为 **270**。
- 自查 path_closeup.png：旧像素色块路面、中央和横向接缝，无法读取任务要求的石板贴图。不是合格成品。
- 既有像素掩码导出路面样本为 **0**。不能拿矩形框亮度冒充 PathTone 路面 L*；scale_m / luma / repeat_score 实际场景验收均为 N/A。
- diagnostic_luma.json 的固定矩形旧路面诊断 L*=94.0062（50000像素），只用于说明截图错误，不用于路面通过判定。
- repeat.json / repeat_weight_050.json 为未被场景引用的着色器 CPU 诊断：原 0.68/0.32 权重 0.832459；0.50/0.50 权重 0.541789。计算不含上一版验收脚本虚构的 scramble；不冒充实际场景结果。

## 本轮实测

Development / Metal，1920×1080，既有 PerformanceBuild.Run 和既有演示入口；未改验收门槛。

| 验收项 | 数字 | 结论 |
|---|---|---|
| 构建 | Succeeded / errors=0 | 通过 |
| 跟随相机 | 距离11.50 / 高2.10 / 视线1.20 / FOV48.00 | 参数一致 |
| 近景 | distance_m=3.000 | 原近景入口 |
| walk | bad=0 / distance=15.396m / ground_error_max=0 / raycast_misses=0 | 通过 |
| enter church | bad=0 / INT_PASS布尔全True | 通过 |
| reveal | bad=2 / gate=FAIL / ratio20_80=1.798609 | 失败 |
| z01 | 平均225.29 FPS / 1%low171.09 / 261 drawcall | 室外FPS通过 |
| terrace | 平均223.41 FPS / 1%low156.06 / 284 drawcall | FPS与drawcall通过 |
| interior | 平均228.71 FPS / 1%low166.06 / 258 drawcall | 记录 |
| 两张截图 | 均1920×1080 / 本次构建后生成 | 有效诊断图，非合格路面图 |
| 游戏资产改动 | 0 | 无效调整已撤回 |

`bad` 按既有脚本风格统计失败日志匹配数；reveal 的两处是 gate=FAIL 及其后错误日志，不表示运行了两次演示。

复验：`python3 spike/tools/verify_path_scale_tone_v2.py`，**exit=1 / pass=false**。完整重新构建和运行：`bash spike/tools/accept_path_scale_tone_v2.sh`。原始日志位于 `spike/logs/path_scale_tone_v2_*.log`，性能原始 JSON 为 `spike/logs/path_scale_tone_v2_bench.json`。

先恢复任务书要求的、确实引用 PathTone 的 a28e637 生成场景/资产基线，才能继续参数调整。恢复或重建场景会涉及本任务禁止修改的对象，因此本轮没有擅自执行。
