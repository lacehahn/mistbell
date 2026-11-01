# 灌木 A/B 实测报告（2026-11-02）

1. **blob 提亮**：起点亮度比 **0.46** → 校正 1 `bumpCompensation=1.85`（叠乘原 `bushCorrection=0.5`）→ 实测 **0.83**（62.9 / 75.4）→ 校正 2 **未使用**，因为已落在 **0.75–1.15** → 最终 **0.83**。`M_BushLight` 保持最终 tint ×1.06。法线强度仍为 0.9，关闭镜面高光，原灌木颜色/法线贴图和几何不变。

2. **F1–F14**：门槛采用 brief 原值；`foliage.py` 自带提示仍显示旧的 0.90 下限，本报告按 brief 的 0.75 判断，没有修改测量脚本。

| # | 项与原门槛 | 实测值 | 过或不过 |
| --- | --- | --- | --- |
| F1 | 两路线日志；blob quads=0；card quads=66、clip=0.5、cullOff=true | blob：`bushVariant=blob quads=0 material=M_Bush`；card：`bushVariant=card quads=66 material=M_BushCard tex=128x128 clip=0.5 cullOff=true doubleSided=true` | 过 |
| F2 | alphaClip=true、cull=0、specularOff=true、normalMap=false | 卡片材质日志四项依次为 **true / 0 / true / false** | 过 |
| F3 | 128×128 RGBA；覆盖 55–78%；G∈[62,80] | PNG 色型 6，128×128 RGBA；不透明 **10612/16384=64.7705%**；不透明区 RGB=**(56.309,71.462,41.977)**；alpha 仅 **0、255** | 过 |
| F4 | blob ground 比 0.75–1.15；记录校正 | **0.83**；×1.85 一次；第二次未用 | 过 |
| F5 | card ground 比 0.75–1.15 | **0.71**（54.9/77.1） | **不过** |
| F6 | 四张 clipped 全部≤0.05% | 灌木采样区：blob ground **0.01%**、blob runtime **11.54%**、card ground **0.07%**、card runtime **3.07%**；草区四张均 **0.00%** | **不过** |
| F7 | blob ground middle 如实报，无硬门槛 | **13.81**；上一轮 **11.27**；top=2.72、bottom=2.73 | 过（已实测，无硬门槛） |
| F8 | card ground middle≥13.5 | **10.71**；top=2.72、bottom=2.73 | **不过** |
| F9 | 两套 runtime middle 变化≤12% | brief 基线 **4.14**；blob **4.14（0.00%）**、card **4.14（0.00%）** | 过 |
| F10 | sky±2、clipped≤0.1%、heightPct±1.5、pitch±0.3、porch±5 | 两套 sky 均 **(167,183,203)**，差 0；主视角全帧采样 clipped：blob **1.10%（911/82944）**、card **0.12%（96/82944）**；两套教堂 heightPct **49.8**、pitch **11.8**、porch centerX **924.6**，对 brief 基线差均 0 | **不过**（两套全帧 clipped 均超标） |
| F11 | 构建×2；双平台 Succeeded；error CS=0 | card：macOS **Succeeded/errors=0**、Windows **Succeeded/errors=0**；blob：macOS **Succeeded/errors=0**、Windows **Succeeded/errors=0**；两个构建日志 `error CS` 各 **0** | 过 |
| F12 | 同参数连续两次 props/bushVariant/heightPct 逐字一致 | card 186/187、blob 190/191 两对均 **逐字相同**；props 均 bushes=22、rocks=30、fencePosts=33、fenceRails=64、seed=20261102；教堂/门廊/角色 heightPct 均 **49.8/10.0/14.5** | 过 |
| F13 | 草/石/瓦六文件 MD5_OK | grass、grass_n、stone_ashlar、stone_ashlar_n、roof_slate、roof_slate_n **6/6 MD5_OK**；额外检查 bush、bush_n 也 **2/2 MD5_OK** | 过 |
| F14 | 两张地面图真读图并回答四问 | 已分别读取最终两图（`--image=` 路径记录及原分辨率看图）；人工目视四问见下文 | 过（完成读图；不代表画面合格） |

**F14：blob — [art_ground.png](project/Shots/art_ground.png)**

① 按大叶团之间的可见凹口数，约 **7 处**（上缘 3、左右合计 2、下缘 2；不把贴图暗线当轮廓凹口）。② 表面仍是体积明暗、硬亮斑与鳞片状纹理，尤其顶端像石壳，不是清楚的硬色块加像素毛边。③ 与像素草的细笔触不协调，灌木的大块法线明暗压过像素语汇；这张图未见角色，不能直接判定角色搭配。④ 更像**堆起来的花纹绿石或卷心菜石雕**。

**F14：card — [card_art_ground.png](project/Shots/card_art_ground.png)**

① 不计单像素台阶，人工辨认约 **12 处**明显凹口（左缘约 5、上缘约 3、右缘约 2、下缘约 2；卡片交叠会影响分组，非自动精确计数）。② 纹理能读出硬色块和锯齿边，没有每片叶子的暗描边；但大面积连续明暗来自 Lit 光照，中央交叉处很黑，三张平面的折线仍然抢眼。③ 叶片色块和毛边比 blob 接近像素草，但叶块密度偏稀、折面明显；同样没有角色可供直接比较。④ 更像**三片相交的深绿色纸板盆景**，尚未读成自然灌木。

3. **卡片实现要点**：底边中点为原点的 quad 为 **1.25m×1.05m**，UV 0..1，正面法线 -Z；缓存为 `Assets/Settings/Quad_BushCard_1.25_1.05.asset`。每丛三张，偏航为独立本丛基座偏航 + **0°/60°/120°** + 各自 **±12°**；相对缩放 **1.00/0.94/0.88**，再乘原丛 scale，三张底边都从同一吸附地面点起。沿用原 22 丛落点、`Snap()`、bushIndex 和每丛 10 次共享随机抽样；新偏航只消耗独立 shape 序列。

`Assets/Settings/M_BushCard.mat` 手工使用 URP/Lit；`_AlphaClip=1`、`_AlphaClipThreshold=0.5`，并设置 URP 实际使用的 `_Cutoff=0.5`；`_Cull=0`、doubleSidedGI=true、RenderType=TransparentCutout、queue=2450、ShadowCastingMode.On。Smoothness/Metallic/SpecularHighlights 均 0，无 normal map。只用 alpha>128 的 texel 进入 TintFor 均值及 p99 计算；目标取 **blob 最终基础材质的线性平均反照率**，约 **(0.0925,0.1295,0.0555)**，没有为卡片单独调亮以凑 F5。ImportTex 后设置 alphaIsTransparency、mipmap、Point、Uncompressed、Clamp。

贴图独立于 RGB/法线 jobs 写出：**34 个半径 3–6px 的圆弧凸起**、三块硬明暗、**50** 个 4–7px 无描边叶形、底部 y≥105 压一档、**12** 处顶部亮边。实际轮廓包围盒 **(7,7)–(121,121)**，115×115px（画布的 89.84%×89.84%，与 brief 约 88%×92% 的意图接近，但并非精确相等）；实际覆盖率见 F3。先前试跑发现旧 PNG writer 丢弃 alpha，已修正 RGBA 写出；最终表格只采用修正后、宽底圆顶版本的截图，不采用中间版。

4. **一句话结论**：卡片比 blob 的绿石鳞片更接近 HD-2D 植被的像素边缘语汇，但当前仍读成相交纸板盆景；本轮 **F5、F6、F8、F10 不过**，不能据此认定卡片路线已达标。

两套本轮新出机位为 runtime / close / ground，共 **6 张**，均 1920×1080。最终场景和两个平台构建停在默认 **blob**；卡片图保存在 `card_art_*.png`。按 brief 批量复制也保留了旧的其它 `art_*` 图的 card 前缀副本，它们没有作为本轮新截图或验收证据。

证据：场景日志 [card](logs/186_scene_card.log)、[card repeat](logs/187_scene_card_repeat.log)、[blob](logs/190_scene_blob.log)、[blob repeat](logs/191_scene_blob_repeat.log)；构建日志 [card](logs/188_build_card.log)、[blob](logs/192_build_blob.log)。测量保存于 `logs/189_{foliage,sharp,frame}_card.txt`、`logs/193_{foliage,sharp,frame}_blob.txt`，四张 region 各有独立日志。几何占屏/俯仰/门廊数据引用场景的确定性投影诊断，天空及全帧 clipped 引用 frame.py，没有冒充从像素图重新推算教堂几何。另见 [贴图统计](logs/185_card_texture_stats.json)、[MD5](logs/194_texture_md5_check.txt)、[确定性与构建](logs/194_determinism_build.txt)、[图像读取记录](logs/194_image_reads.txt)。

只读取 brief 及其指定的四个代码文件，未通读仓库；Screenshotter.cs 只确认 `-shots`，未修改；未修改 AGENTS.md、验收脚本或门槛数字。
