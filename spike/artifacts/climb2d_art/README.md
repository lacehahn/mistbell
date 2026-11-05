# 2D 攀爬真素材验收

任务：[climb2d_art.md](../../tasks/climb2d_art.md)。仅修改隔离的 2D 攀爬绘制、素材导入及本任务验收工具；3D 场景、地形、相机、围栏、教堂与室内保持原样。

复现命令（仓库根目录，使用已有 Pillow 和 Unity 6000.3.27f2）：

```sh
bash spike/tools/accept_climb2d_art.sh
```

命令从已入库 JPG 重建 `spike/project/Assets/Resources/Climb2DArt/` 下的 PNG 与 `route.json`，打包 Mac Player，走完全程并依次运行 `-demo-walk 6`、`-demo-walk-stairs`、`-demo-camera-distance`。生成资源沿用仓库现有忽略策略，可由脚本完整重建。

处理参数：黑底阈值 max(R,G,B) ≤ 22；3×3 MinFilter 向内去边 1 个源像素；雪面 RGB ×0.92 后调色；石材目标 Lab b*=+5；树、S 路和补充路面为 5 阶色板；首尾各 10% 交叉淡化。台阶前沿雪带压至 8 个源像素，绘制时每级高 16 个逻辑像素、级间升高 8 px，共 48 级。

五层顺序：上坡面（画面上部 55%）→ 下坡面（下部 45%）→ 树（视差 0.6）→ 原始 S 路带 → 台阶与两处平台。路线使用源图顶面上的 21 个中心点，实际贴地沿该曲线采样，两个弯点放置 `side_step_alt`。480×270、×4 放大保持不变；转场目标参数 0.613 s，实测包含帧调度与全白帧等待。

接缝定义为原始 PNG 或运行时导入贴图的末列与首列 RGBA 平均绝对差（0–255）。全部 7 张横向贴图分别测量；单级台阶和有限长度平台不做横向循环，处理记录中其首尾差不作为平铺判据。b* 离线使用 Pillow/LCMS，运行时使用 sRGB→XYZ→Lab，统计不透明石色中间调；两种测量均须落在 4–6。

验收数据见 [acceptance.log](acceptance.log)、[acceptance.json](acceptance.json) 与 [processing.json](processing.json)。构建及各运行原始日志位于 `spike/logs/climb2d_{build,run,walk,stairs,camera}.log`。

五张实际 Player 截图（均为 1920×1080，已人工检查素材、弯路、平台和台阶）：

- [起点](../climb2d/climb2d_start.png)
- [坡中](../climb2d/climb2d_ramp_mid.png)
- [折返](../climb2d/climb2d_turn.png)
- [台阶中](../climb2d/climb2d_stairs_mid.png)
- [转场前](../climb2d/climb2d_before_transition.png)

另有 [3D 落点截图](../climb2d/arrival_3d.png)。完整流程必须抵达 `TerraceLanding`、贴地误差为 0 且 `bad=0`。

本次自验（2026-11-05）：构建 Succeeded / errors=0；`bash spike/tools/accept_climb2d_art.sh` 退出码 0。

| 验收项 | 实测 |
|---|---:|
| 素材合成层数 | 5 |
| 7 张横向 PNG 的最大首尾差 | 0.0000 / 255 |
| 运行时导入后的最大首尾差 | 0.000 / 255 |
| 运行时石色 b*（90,047 个样本） | +4.775 |
| 树 / S 路 / 补充路面色阶数 | 各 5 |
| 曲线中心线最大贴地误差 | 0.000031 px |
| 透明区误踩 | 0 |
| 步行 / 48 级台阶 | 40.600 / 7.000 s |
| 完整流程（含截图、加载及转场） | 50.512 s |
| 转场目标 / 含全白帧等待实测 | 0.613 / 0.650 s |
| 3D 落点 / 误差 / bad | TerraceLanding / 0.000000 m / 0 |
| `-demo-walk 6` FPS / 样本 / bad | 59.83 / 359 / 0 |
| 3D 台阶 / 相机两阶段回归 | 均通过，bad=0 |
| 五张截图 | 各 1920×1080 |
