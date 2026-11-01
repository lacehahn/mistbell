# 第 26 轮：blob 回修与 Y 轴像素 Billboard 灌木实测

日期：2026-11-02。依据 [brief-bushsprite.md](brief-bushsprite.md)，验收数字未修改。结论：**F1–F15 共 12 项过、3 项不过（F3、F9、F15）**。blob 两张图的 clipped 均满足 ≤0.05%，但没有同时做到 ground 的亮度与锐度目标；不能把任务 A 写成全部达标。

源码只读取 brief 指定的四个文件。本轮编辑 `SpikeScene.cs`、`PixelBillboard.shader`、`make_textures.py`；`SpikeArt.cs` 原有工作区改动未动。未编辑 `foliage.py`、相机、ShotRig、光照、Volume、SSAO/移轴、教堂、栅栏、石块、blob 几何或叶簇。`-bushCard` 分支代码与卡片贴图未改、未重新出卡片图。默认仍为 blob，最后建场、构建和运行截图均为 blob。未改 AGENTS.md。

## 1. blob 回修与校正记录

灌木 `bumpScale=0.5`；保留 `_SPECULARHIGHLIGHTS_OFF`、原有 `bushCorrection=0.5` 和亮叶 ×1.06。只调整 `bumpCompensation`，起点 1.39，之后校正一次到 1.00，无后续试参。

| 轮次 | bump | compensation | ground：bush / grass、比、clipped | runtime：bush / grass、比、clipped | 主视角 frame clipped |
| --- | --- | --- | --- | --- | --- |
| brief 上轮基线（非本轮重跑） | 0.9 | 1.85 | 62.9 / 75.4＝0.83；0.01% | 123.7 / 81.2＝1.52；11.54% | 1.10% |
| 本轮起点 | 0.5 | 1.39 | 47.3 / 75.4＝0.63；0.00% | 99.0 / 81.1＝1.22；3.94% | 0.32% |
| 一次校正，最终保留 | 0.5 | 1.00 | 34.4 / 75.4＝0.46；0.00% | 74.3 / 81.1＝0.92；**0.047222%** | **0.004823%** |

1.39 起点已经出现 ground 偏暗、runtime 削顶的相反约束。因此本次向下校正，优先满足两张图的削顶门槛；没有再靠提高反照率把 ground 拉亮。实测不能支持“降低 bump 后已经同时解决两个视角”的结论。最终 runtime 的 `foliage.py` 显示值四舍五入为 0.05%，另逐像素核对为 **34 / 72000＝0.047222%**，按未舍入值判断通过。ground 为 0 / 33600。

原始结果：[起点 foliage](logs/198_foliage_blob_139.txt)、[起点 frame](logs/198_frame_blob_139.txt)、[最终 foliage](logs/198_foliage_blob_final.txt)、[最终 sharp](logs/198_sharp_blob_final.txt)、[最终 frame](logs/198_frame_blob_final.txt)、[逐像素 clipping](logs/199_exact_clipping.json)。

## 2. F1–F15 验收

`foliage.py` 输出中旧的通用亮度提示仍为 0.90–1.15；**本表按 brief 的逐项门槛判定**，没有修改工具提示或门槛。frame 的“全帧”值使用既有 `frame.py` 的全画面抽样网格（82944 个样本），没有冒充逐像素全帧计数。

| # | 项 / 原门槛 | 实测值 | 过或不过 |
| --- | --- | --- | --- |
| F1 | 一行 bushSprite；quads=22、variants=8、castShadow=true、depthOnly=true、tex=112x80、pivotY=0 | 全部相符；color=(1.000,1.000,1.000)，shadowPass=1、depthPass=2；完整日志见下 | 过 |
| F2 | 112×80 RGBA；alpha 只有 0/255；不透明占比45–62%；G∈[58,86] | 8/8 均112×80 RGBA，alpha={0,255}；占比52.433–55.859%；G均值72.256–74.623；8个文件哈希各异 | 过 |
| F3 | blob ground 比0.80–1.10；clipped≤0.05% | **比0.46**；clipped=0.000000%（0/33600） | **不过：比值不足** |
| F4 | blob runtime 比0.75–1.15；clipped≤0.05% | 比0.92；clipped=0.047222%（34/72000） | 过 |
| F5 | blob runtime frame clipped≥250≤0.10% | 4/82944＝0.004823%（工具显示0.00%） | 过 |
| F6 | sprite ground 比0.80–1.10；clipped≤0.05% | 比1.03；clipped=0.032738%（11/33600） | 过 |
| F7 | sprite runtime 比0.75–1.15；clipped≤0.05% | 比0.94；clipped=0.000000%（0/72000） | 过 |
| F8 | sprite ground sharp middle≥11.0 | 11.10 | 过 |
| F9 | blob ground sharp middle≥12.0 | **11.16** | **不过：不足12.0** |
| F10 | 两套 runtime sharp middle 相对基线4.14变化≤12% | blob=4.14，sprite=4.14；按工具显示精度变化均0.00% | 过 |
| F11 | 两套主视角 sky±2、heightPct±1.5、pitch±0.3、porch±5 | 两套sky均(167,183,203)，与brief基线差(0,0,0)；教堂heightPct均49.8、pitch均11.8°、porch heightPct均10.0、centerX均924.6；与首次建场诊断差均0。相机和构图代码未改 | 过 |
| F12 | 构建×2；双平台Succeeded，error CS=0 | sprite：macOS/Windows均Succeeded、errors=0；blob同样；两份最终build日志error CS=0、Shader error=0 | 过 |
| F13 | 同参数连跑两次；props/bushVariant/bushSprite/heightPct逐字相同 | sprite提取6行、blob提取5行，各自两次逐字相同；blob无bushSprite行，两次均无 | 过 |
| F14 | 草/石/瓦6个+bush.png/bush_card.png前后MD5相同 | 所需8/8一致；额外bush_n.png也一致，共9/9 | 过 |
| F15 | 两张ground图逐项目视：凹口、表面明暗、与草的笔触、读成什么 | 两图均已直接打开检查，具体见第4节；sprite有像素硬边，但圆叶团排列感、局部空隙与地面草的笔触差异仍明显 | **不过：美术读取未完全达到灌木目标** |

F11 的构图对照来自本轮首次与最终建场诊断；brief 未列历史 heightPct/pitch/porch 绝对数值，因此不伪造历史构图测量。两路线相机诊断完全一致，sky 同时对照了 brief 给出的历史值。

```text
[SPIKE] bushSprite quads=22 variants=8 castShadow=true depthOnly=true tex=112x80 pivotY=0 color=(1.000,1.000,1.000) shadowPass=1 depthPass=2
```

构建与确定性证据：[sprite建场](logs/196_scene_sprite.log)、[sprite重复建场](logs/196_scene_sprite_repeat.log)、[sprite构建](logs/197_build_sprite.log)、[blob建场](logs/198_scene_blob.log)、[blob重复建场](logs/198_scene_blob_repeat.log)、[blob构建](logs/199_build_blob.log)、[验收审计汇总](logs/199_acceptance_audit.json)。保护贴图哈希：[前](logs/196_texture_before.json) / [后](logs/199_texture_after.json)。

## 3. sprite 路线实现与亮度记录

### 贴图

`make_bushsprite(seed)` 用独立种子2600–2607，输出 `bush_sprite_0..7.png`；`bush_sprite.png` 为0号的相同内容。只运行 `make_textures.py --bush-sprite-only`，未重写任何旧贴图。

每个变体8个错落叶团，团块主体约28–30px，每团6–10个长轴随机取向的3–5px叶形色块；多边形以整数像素光栅化，无抗锯齿、无叶片暗描边。每团按左上到右下分成4/3/2/1档硬斜面，附2–3px阶梯；叶形亮块补足局部亮档，整丛底部14px压到0档；顶缘1px用第6档钩光。只使用brief的6种RGB。

第一次目视发现大封闭孔洞，随后填补封闭洞及叶团间中央连接区；没有改色板或材质亮度。最终仍残留局部三角空隙，且叶团偏圆，不能宣称B1的全部美术要求都已令人满意。

| 变体 | 不透明占比 | 不透明RGB均值 | 顶边(104,120,74)占不透明像素 |
| --- | --- | --- | --- |
| 0 | 55.859% | (57.905, 72.508, 43.468) | 1.359% |
| 1 | 54.297% | (58.644, 73.298, 43.987) | 1.459% |
| 2 | 55.000% | (58.875, 73.537, 44.154) | 1.481% |
| 3 | 54.721% | (59.105, 73.775, 44.319) | 1.468% |
| 4 | 54.007% | (58.379, 72.920, 43.871) | 1.467% |
| 5 | 52.433% | (59.276, 73.988, 44.420) | 1.447% |
| 6 | 53.181% | (59.888, 74.623, 44.860) | 1.448% |
| 7 | 54.955% | (57.753, 72.256, 43.431) | 1.442% |

8张均为112×80 RGBA，alpha精确为{0,255}，亮边均低于6%。[逐文件统计与哈希](logs/196_sprite_texture_stats.json)。

### `_Color` 实测

| 阶段 | Color RGB | ground 比、clipped | runtime 比、clipped | 处理 |
| --- | --- | --- | --- | --- |
| 初版叶团、修补前 | (1,1,1) | 1.07；0.03% | 0.94；0.00% | 发现大孔洞，回修轮廓连接；不调Color |
| 最终叶团 | (1,1,1) | **1.03；0.032738%** | **0.94；0.000000%** | 已在B2目标0.85–1.05内，亮度校正次数0，保持白色 |
| 最终shader深度输出复验 | (1,1,1) | 1.03；0.032738% | 0.94；0.000000% | 复测一致，无新增校正 |

没有使用超过±25%的材质补偿，也没有改调色板。最终ground luma=79.3、grass=77.1，runtime luma=76.0、grass=81.1。[最终foliage](logs/196_foliage_sprite_final.txt)、[最终sharp](logs/196_sharp_sprite_final.txt)、[最终frame](logs/196_frame_sprite_final.txt)。

### 材质、几何、阴影与深度

- 8个 `Mistbell/PixelBillboard` 材质，0号名为 `M_BushSprite.mat`，其余 `_1..7`。Point、无压缩、sRGB、alphaIsTransparency、mipmap关、Clamp、NPOT缩放关；保持112×80。
- `_Cutoff=.5`、`_Cull=0`、`_PivotY=0`，基准宽1.35m、高0.96m。沿用每丛原有scale，再以独立shape序列作横向[0.9,1.1]、高度[0.92,1.08]微扰，写入MaterialPropertyBlock；不消耗布局rnd。
- 每丛一个 `SpikeArt.Quad("Billboard",1,1)`，底边中点原点、UV0..1。新增隐藏属性 `_BottomOrigin=1` 适配底边原点；默认0保留既有角色居中网格的坐标约定。CPU包围盒扩大到4×3×4m覆盖旋转和尺寸；网格顶点与UV不改。
- 保留 `Snap()`、原有10次rnd抽样、22丛、bushIndex顺序。`index % 8` 分配全部8个变体：0–5各3丛，6–7各2丛。
- Forward仍为纹理×Color的unlit。所有pass共用Y轴朝向函数和同一alpha阈值。
- ShadowCaster带URP的ApplyShadowBias、平行/点光分支、reversed-Z近裁面处理；renderer明确设ShadowCastingMode.On。DepthOnly写Z并输出片元深度。额外DepthNormalsOnly覆盖SSAO使用法线预通道的情形。
- 两个必需pass实际被材质找到（索引1、2），两平台shader编译无错误，运行截图无粉色shader错误。此证据验证资源与渲染管线的接入；没有抓GPU Frame Debugger，不把“找到pass”冒称为逐draw-call验证。图中可见丛下暗部，但不把它单独当作阴影pass执行的独立证明。

## 4. F15 目视与图像交付

已分别直接打开两张最终PNG逐项观察（使用当前会话的本地图像查看工具，等同把文件作为 `--image=` 图像输入；未伪称另开CLI图像审查）。

| 观察项 | [blob ground](project/Shots/art_ground.png) | [sprite ground](project/Shots/sprite_art_ground.png) |
| --- | --- | --- |
| ① 外轮廓凹口 | 上缘能辨认3处主要大凹口，侧边另有折角；来自凸起体块，缺口偏硬 | 上缘3处主要V形凹口，下缘另有约3处分团凹口；中央仍能看到三角空隙 |
| ② 表面明暗 | 实时受光的连续明暗叠像素纹理，侧面有大块深黑与条带 | 明暗是画进去的硬像素色块，没有实时连续受光；叶团边界仍显得过于完整圆润 |
| ③ 与右边草的笔触 | 不像。blob的大面折线、纹理拉伸与细碎草笔触差异很大 | 比blob接近：都保留像素台阶；但sprite是大团面与少量小叶斑，草是密集斜向短笔触，仍不统一 |
| ④ 一句话读取 | 更像贴了迷彩叶纹的岩石/紧实卷心菜团块 | 更像圆叶团绿篱/多肉群，接近灌木，但还不够自然 |

**sprite比blob更像HD-2D植被，但本次不能把F15判为通过。**

最终运行截图（两套均1920×1080）：

- sprite：[ground](project/Shots/sprite_art_ground.png)、[runtime](project/Shots/sprite_art_runtime.png)、[close](project/Shots/sprite_art_close.png)。
- blob（默认，最后生成）：[ground](project/Shots/art_ground.png)、[runtime](project/Shots/art_runtime.png)、[close](project/Shots/art_close.png)。

按brief复制了全部 `art_*.png` 为 `sprite_art_*.png`；其中approach/wide沿用已有截图，本轮capture脚本实际重拍的是runtime/close/ground，不能把旧图标成本轮新拍。

本轮保留的限制与失败：blob削顶已压到门槛内，但ground亮度、锐度不过；sprite的圆叶团和局部空隙仍需美术迭代。没有为追求全绿改门槛，也没有改受保护项目来补这三项。
