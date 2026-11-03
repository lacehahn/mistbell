# HD-2D 场景流程核对（对标《歧路旅人》）

> 目的：把"什么让画面像 HD-2D"拆成可执行的工程项，逐条核对本项目现状，指出缺口与下一步。
> 本文只描述**制作流程与技术配方**，不涉及玩法。

## 一、HD-2D 的五个支柱

| # | 支柱 | 具体含义 | 判断标准 |
|---|---|---|---|
| 1 | **低分辨率像素材质贴在 3D 几何上** | 表面贴图是手绘像素/笔触，不是 PBR 贴图；纹理的"像素颗粒"有统一的世界尺度 | 画面缩到 480×270 再放大，各元素颗粒大小一致 |
| 2 | **2D 像素角色站在 3D 场景里** | 角色是 billboard 精灵，低帧率动画（8–12 fps），受同一套光照/雾影响 | 角色与场景有共同的雾与色温 |
| 3 | **无高光的"画布"光照** | 表面几乎不反光；靠烘焙明暗、环境光与暖色点光塑造体积，而不是 PBR 高光 | 平面墙上没有随镜头移动的高光条 |
| 4 | **移轴式景深 + 分层纵深** | 中景清晰、前后虚化；远景是几张手绘层，逐层雾化 | 有 3 层以上纵深，层间有空气透视 |
| 5 | **统一的后期栈** | Bloom（暖）、低饱和、轻微暗角、颗粒、色调映射；控制白点避免雪过曝 | 整帧色板收敛、无硬切边 |

## 二、本项目现状核对

| 支柱 | 现状 | 实测/证据 | 结论 |
|---|---|---|---|
| 1 纹理尺度 | 手绘贴图 → 3 张 4096² UV 图集，1294 张大图表覆盖 98% 面积；`texel_per_m` 统一 | 八材质 `hf` spread 0.20 以内；砌块 0.22–0.85 m | ✅ 基本达标 |
| 2 角色 | `pilgrim_atlas.png` 384×320、24 帧、10 fps、Point 采样 | 近景 149.8 px / 远景 24.8 px | ✅ 达标 |
| 3 光照 | ⚠️ 仍是 URP **Lit + PBR**，靠环境光/点光；**场景没有环境光/间接光** | 台地墙背光面近黑；换材质亮度不变，证明是光照不是贴图 | ❌ 最大缺口 |
| 4 纵深 | 四层像素远山 + 地平线雾 + 雪粒子 + 前景雪脊 | `seam_max_grad 0.007–0.035`、`mountain_luma 0.48–0.57` | ✅ 达标（山略淡） |
| 5 后期 | ACES + Bloom(0.9/0.75/0.72) + Bokeh DoF(26m/2.8/50mm) + ColorAdj(-0.15EV/14/饱和-6) + Vignette 0.16 + FilmGrain 0.18 | 截图与 `measure_shot.py` 六项 | ✅ 基本达标 |

## 三、技术配方（当前实现）

### 1. 材质
- 手绘贴图 → UV 图集（每面墙一张图表，投影到主平面，UV 0–1）
- 着色：`Mistbell/TerrainHaze` 的 `_Style11UV=1` 分支（图集采样），`_Smoothness=0`，无高光
- texel 密度：石墙 112 px/m（图集通道）、雪 20 px/m、木 24 px/m
- **缺口**：仍走 Lit 的 BRDF，环境光为 0 时背光面直接压黑

### 2. 光照（需要重做）
目标：
- **环境光/半球光**：冷天光（上）`#9DB2C8` + 地面反射（下）`#8A8378`，`ambientIntensity 0.45–0.6`
- 主方向光：暖白 `#FFE6C4`，强度 1.1–1.3，仰角 28–35°（保持现有斜射）
- 背光面下限：任何朝下的面 `L* ≥ 18`（现在是 0–5）
- 暖点光：窗内/烛台/火盆，范围 3–6 m
- 顶缘 rim：材质级 +18% 暖边（已在贴图里烘焙，保留）

### 3. 后期栈（现有值，可作为基线）
```
Tonemapping: ACES
Bloom: threshold 0.90 / intensity 0.75 / scatter 0.72 / tint (1,.95,.88)
DepthOfField: Bokeh, focus 26 m, aperture 2.8, focal 50 mm, blades 6
ColorAdjustments: postExposure -0.15, contrast +14, saturation -6
Vignette: 0.16 / smoothness 0.45
FilmGrain: Thin2, 0.18, response 0.8
```
补充建议：加 `8×8 Bayer + 6bit` 量化在最终 pass（已有实现，值在天空 shader 里，需要提到全屏）

### 4. 相机
- 透视 FOV **35°**，跟随偏移 `(0, 4.4, -10.5)`，看向 `+3.2 m`
- 俯角约 8–12°（移轴景深的"微缩感"来源）
- 近景/室内自动切 50°

### 5. 分层
1. 天空穹顶（渐变 + 云，`rotation.y=37°` 避开 UV 接缝）
2. 远山 L1–L4：像素贴图 Quad（900/1300/1800/2200 m），`_Haze` 0.20/0.40/0.58/0.74
3. 地形（雪原 + 风蚀雪脊 + 裸岩簇 + 枯草）
4. 教堂（手绘图集）
5. 角色（billboard）
6. 天气：飘雪粒子（2600/s）、雾片

## 四、验收指标（已落地，可直接复用）

| 指标 | 门槛 | 工具 |
|---|---|---|
| `snow_ratio` | ≥0.55 | `measure_shot.py` |
| `white_clip_ratio` | ≤0.05 | 同上 |
| `magenta_px` | 0 | 同上 |
| `hf_energy_lower` | 0.012–0.045 | 同上 |
| 材质颗粒 spread | ≤0.20 | `measure_style*.py` |
| 墙面 `L*` / `b*` | 38–46 / +10~+18（暖土黄） | 自测脚本 |
| 屋顶比墙深 | ≥3.5 L* | 同上 |
| 非平铺自相关 | 二维 ≤0.5 | `measure_style11.py` |
| 性能 | ≥200 fps @1080p、drawcall ≤330 | `-bench` |

## 五、一键流程

```bash
# 1) 手绘图 → 图集
python3 spike/tools/forge_style11.py          # 生成 UV 图表（首次）
python3 spike/tools/ingest_handpaint.py       # 把手绘图贴进图集
# 2) Unity 侧
Unity -batchmode -quit -projectPath spike/project -executeMethod MaterialForge.Generate
Unity -batchmode -quit -projectPath spike/project -executeMethod SpikeScene.Run
Unity -batchmode -quit -projectPath spike/project -executeMethod SpikeBuild.Run
# 3) 验收（含截图/量测/演示/性能）
bash spike/tools/accept.sh
```

## 六、下一步优先级（按"提升画面像不像 HD-2D"排序）

1. **补环境光/间接光 + 背光面下限**（当前最破坏观感：台地墙、屋檐下、室内全黑）
2. **材质去 PBR 高光**：给石墙/屋顶一个 `PaintedSurface` 变体（Lambert + 半球光 + 烘焙 AO，`Specular=0`）
3. **像素化统一**：全屏 6bit + 8×8 Bayer（现在只在天空），并把 DoF 半径按 480×270 的像素栅格对齐
4. 远景山加存在感（雾回调到 0.16/0.34/0.52/0.70）、雪线提亮
5. 角色与场景的雾/色调统一（角色当前偏亮偏冷）

完成 1–2 之后，画面会从"手绘贴图的 3D 场景"变成真正的"HD-2D 画布"。
