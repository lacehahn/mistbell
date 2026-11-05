# 任务 B：3D 教堂细活验收

已完成任务书 `spike/tasks/church3d_detail.md`。只覆盖教堂外观；没有运行全量重建。

## 复跑命令

```sh
bash spike/tools/accept_church3d.sh
# 已构建播放器：
bash spike/tools/accept_church3d.sh --player-only
# 复核已有资产、日志与截图：
python3 spike/tools/verify_church3d.py
```

前提为现有 `checkpoint-cliff` 工程及 Style11 图集/UV 资源。生成器复制图集到 `Church3D_Atlas0/1/2.png`，仅由教堂材质引用；原图集 SHA256 未变。编辑器入口 `Church3DDetailBuild.Run` 在既有场景局部打补丁，保留 ArtSpike 与 Climb2D 两个构建场景。重复运行仍从原材质派生，不叠乘暗化。

## 任务书逐项结果

| 项 | 实测 | 判据 |
|---|---|---|
| 砌块档位 | 4；实际像素抽检 8886 块 | 4 |
| 灰缝宽度 | 3 / 5 / 7 px，基准 5 px，±40% | ±40% |
| 灰缝暗度 | 19.6644%–20.0720% | 18%–22% |
| 条石间隔 | 6–9 块 | 6–9 块 |
| 条石长度 | 1.5833–1.6250 倍 | 1.4–1.8 倍 |
| 屋顶行距错位 | ±2%；5× 采样后 BOX 还原保留亚像素偏移 | ±2% |
| 檐口阴影间隔 | 5–7 行 | 5–7 行 |
| 檐口阴影暗度 | 11.9074%–12.1207% | 12%（RGB8 量化） |
| 屋脊 / 山墙边缘投影 | 153 条；alpha 0.35；8 px 等效（112 texel/m，实际宽 0.071429 m） | alpha 0.35，6–10 px |
| 窗棂 | 7 扇，每扇竖 1 / 横 2；内侧投影偏移 0.150 m | 0.15 m |
| 玻璃梯形亮斑 | 每扇 4 顶点；自发光 ×1.200 | +20% |
| 窗洞内壁 | 原乘色 ×0.920 | 暗 8% |
| 正面近景 | 距正面窗洞平面 12.000 m | 12 m、正面 |
| 构建 | Succeeded；errors=0 | 成功 |
| 步行 6 秒 | 17.747 m；接地误差 0；raycast_misses=0；bad=0 | bad=0 |
| 进堂 | INT_PASS 各布尔项 True；lamps=20；bad=0 | bad=0 |
| 揭示 | gate=pass；bell_once=True；pulse_restored=True；bad=0 | bad=0 |

四档色板由同一 Lab a*/b* 目标值反算 RGB；实际 8-bit 色板对基准 b* 的最大量化误差 **0.180364**。颜色从 (97,85,70) 的原石材色相派生。暗度按源 sRGB 亮度比例测量；室内亮斑的 +20% 指材质自发光值，后期曝光 / Bloom 后的屏幕亮度不保持线性。

## 性能

Apple M6 / Metal，Unity 6000.3.27f2 Development，1920×1080；关闭帧率上限和 vSync，预热至少 180 帧 / 3 秒，取 300 帧。任务书 FPS 按平均吞吐量验收，1% low 同时列出。

| 室外位置 | 平均 FPS | 1% low FPS | drawcall |
|---|---:|---:|---:|
| z01 | 213.04 | 166.50 | 242 |
| terrace | 223.68 | 160.19 | 277 |

任务判据为两处均平均 FPS ≥200 且 drawcall ≤400，均通过。使用现有 `-bench -bench-baseline` 采样，由本任务校验器执行 400 drawcall 门槛；已有通用 benchmark 的 250 drawcall 门槛与本任务不同，没有修改它。

## 截图与范围保护

- [art_runtime.png](art_runtime.png)：原全景机位，1920×1080。
- [church_closeup.png](church_closeup.png)：12 m 正面，1920×1080。
- 两图均已实际打开检查：窗棂与偏移投影、上部梯形亮斑可见；屋顶仍可读成瓦面。
- 独立 `-shot <路径> -church3d-shots` 入口只在截图会话套用机位。既有 `-shots` 会因当前角色高度 71.8 px 不满足其 120 px 下限而提前结束；本任务没有修改角色、相机或该断言。
- 相机、室内、路面、围栏、碰撞体和其材质的保护摘要前后一致：`20E8A2ECA848DECD5CC139C628CA81CB0E56F54C40161541722D9FCFB3C48943`。2D 攀爬源码与资源未改。
- [measurements.json](measurements.json)：结构化数字及所有判据；[assets.json](assets.json)：生成几何/材质与保护摘要；[bench.json](bench.json)：原始性能计数。
- 原始日志：`spike/logs/church3d_{build,walk,interior,reveal,capture,bench,accept}.log`；按仓库策略不入库，可由上述命令重现。
