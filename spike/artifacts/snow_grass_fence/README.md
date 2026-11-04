# snow_grass_fence 验收记录

本次仅处理 `spike/tasks/snow_grass_fence.md`。最终完整验收命令退出码 0，`pass=true`。

复跑（仓库根目录）：

```sh
bash spike/tools/accept_snow_grass_fence.sh
# 对现有构建复跑：
bash spike/tools/accept_snow_grass_fence.sh --player-only
# 仅重算现有证据：
python3 spike/tools/verify_snow_grass_fence.py
```

## 改动与数字

- `[GRASS_PROBE]` 识别到 72 个 `DryGrass_*`、96 个 `TrailShrub_*` 和 2 个 `FGD_Grass*` 渲染对象。前两类为不透明实心草茎，`M_STYLE6_Stems` / `Mistbell/TerrainHaze`，alpha=1；前景草也使用不透明 shader。按任务书允许的删除方案清除，最终 `[GRASS] count=0`。完整首次探针在 [initial_build.log](initial_build.log)，最终摘要在 [acceptance.log](acceptance.log)。
- 木围栏 `#4A3B2E`，185 根柱、366 根横杆，每段双杆；立柱 0.10×0.10×1.00 m，横杆截面 0.06×0.08 m，实测上下间距 0.450001 m。柱间水平距离实测 2.199987–2.200088 m，弯角也按 2.2 m 弦长采样。
- `#EDF3F8` 顶部积雪厚 0.040 m，柱顶及横杆顶都有雪。8 个材质/路段批次。独立围栏 ShadowCaster 采用 Bayer 10/16 覆盖率，即阴影采样覆盖强度 0.625，由现有软阴影滤波整合；这是围栏投影覆盖率，不是修改全局灯光的 shadowStrength。未改灯光和室内阴影。
- 雪面只在 `M_Snow2Terrain` 上启用非重复投影：保留原有像素雪色板，用缓慢变化的大尺度随机旋转/偏移打散平铺，细节/大层权重 0.35/0.65；5 次纹理采样。其余使用 TerrainHaze 的材质默认关闭此分支。
- 室内、祭坛、人物、玩法相机和其余场景资产：构建前后保护哈希一致 `D4C8B4398C4B458FAAF52AA374E057F2F466F574696B744ACE622D3E2F269FB9`。玩法镜头实测仍为 11.5 / 2.1 / 1.2 / 48；两段相机碰撞检测 `bad=0`。

## 最终验收

| 项目 | 实测 | 门槛 |
|---|---|---|
| Unity batchmode / Development 构建 | Succeeded，errors=0 | 成功 |
| 草簇 | count=0 | 0 或透明正确 |
| 围栏 | #4A3B2E、双杆、雪帽、shadow_strength=0.625 | ≤0.65 |
| 玩法机位雪面 grid_score | 0.034356 | ≤0.12 |
| 参考机位雪面 grid_score | 0.085198 | ≤0.12 |
| 山脚 z01，1920×1080 / Metal | 213.28 FPS，285 drawcall | 室外 ≥200 FPS |
| 台地 terrace，1920×1080 / Metal | 225.88 FPS，328 drawcall | ≥200 FPS、≤330 drawcall |
| -demo-walk 6 | pass，bad=0 | 全绿 |
| -demo-enter-church | pass，bad=0 | 全绿 |
| -demo-reveal | pass，bad=0 | 全绿 |

性能沿用原 PlayerBenchmark：至少 180 帧且 3 秒预热，300 帧计数；只在基准会话关闭 VSync/帧率限制。原基准阈值未改。台地相对任务书的 355 drawcall 减少 27（7.61%）。

`grid_score` 定义：实际 PNG 缩至 480×270，固定纯雪 ROI：玩法 (5,108,93,245)，参考 (260,211,340,263)。灰度减 Gaussian radius=3 局部均值，分别计算 X/Y 方向 1–36 px 的归一化自相关，取 4–32 px 周期峰相对 ±1、±2 相邻滞后的最大正突出量；跨轴、跨机位取最大值。算法在任务书未给公式时明确约定，不能解释为米制测量或通用感知指标；ROI 没有随结果挑选。调整前同一算法对应 0.075261 / 0.092707。完整逐轴数据与截图哈希见 [measurements.json](measurements.json)。已人工查看两张 480×270 整帧缩图，雪面无黑梳子，未见规则 4–6 m 网格。

## 揭示回归所需的兼容修正

旧日志 `spike/logs/far_mist_v2_reveal.log` 已有 `far_h_pct=0.411735 gate=FAIL`。仅修正 DemoReveal 自己持有相机时的远景取景，使其实际占高 0.28，保留原 ≤0.30 门槛；近景仍 0.62。另将合批后柱脚验收改读保留的 BoxCollider，并相对实际雪肩支撑面测间隙，保留 ≤0.02 m 门槛，最终柱脚误差 0.000002 m。未放宽门槛；正常玩法镜头、雾/钟声演出曲线没有改动。

## 交付物

- [cam_play_1080.png](cam_play_1080.png) / [art_runtime.png](art_runtime.png)：1920×1080 原始运行时截图。
- [玩法缩图](cam_play_1080_480.png) / [参考缩图](art_runtime_480.png)，以及对应 `_roi.png` 标注。
- [assets.json](assets.json)、[measurements.json](measurements.json)、[bench.json](bench.json)。
- [build.log](build.log)、[walk.log](walk.log)、[interior.log](interior.log)、[reveal.log](reveal.log)、[camera.log](camera.log)、[capture.log](capture.log)、[bench.log](bench.log)、[acceptance.log](acceptance.log)。

AGENTS.md 未修改。未安装依赖，未执行其他任务。
