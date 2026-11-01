# 第 18 轮：SSAO + 石材块面起伏验收报告

日期：2026-11-02。按 `spike/brief-ssao.md` 的 A/B/C 执行；源码阅读限于 brief 指定文件/片段，另核对本轮验收产物与日志。没有修改 AGENTS.md。

## 1. 修改文件与关键行

- `/Users/lance/workspace/mistbell/spike/project/Assets/Editor/SpikeURP.cs:76`：新增 `SSAO()`；第 91 行起通过 `SerializedObject` 配置 `m_Settings.*`。独立创建 `Assets/Settings/SSAOFeature.asset`，保留原 TiltShiftFeature 对象及参数；移除列表中的 SSAO 引用后仅加入一次，重复运行仍为两个 feature。第 118 行起保存、强制重新导入并 Load，断言两项顺序/路径、SSAO 为主资产及 Source=Depth。
- `/Users/lance/workspace/mistbell/spike/project/Assets/Editor/SpikeScene.cs:34`：tiling 后固定设置 `stone.SetFloat("_BumpScale", 1.8f)` 并输出 `stone bump=1.80`；第 36 行起仅修正 tiling 日志的小数格式，不改变 tiling 数值。
- `/Users/lance/workspace/mistbell/spike/tools/make_textures.py:172`：仅改 `make_stone()`。第 197/224 行：中性灰缝和中性流痕混色，15% 浅缝保留；第 260 行：三像素倒角高度 0.30 → 0.50 → 0.70；第 261 行：块心浅凹与连续低频高度变化（合计控制在 ±0.05 内，原颗粒另计）；倒角压暗 8–12、块心提亮 0–4。第 255 行：连续低频斑驳幅度由 40 调到 88，块面亮度统一偏移 -7；第 285 行：灰缝三通道共用 ±2 抖动。块间 light 的原 ±0.025–0.035 分布、size=256、courses=4、块宽分布规则不变。

生成/更新的关键资产：

- `/Users/lance/workspace/mistbell/spike/project/Assets/Settings/SSAOFeature.asset`
- `/Users/lance/workspace/mistbell/spike/project/Assets/Settings/MistbellRenderer.asset`
- `/Users/lance/workspace/mistbell/spike/project/Assets/Settings/M_Stone.mat`
- `/Users/lance/workspace/mistbell/spike/project/Assets/Art/Textures/stone_ashlar.png`
- `/Users/lance/workspace/mistbell/spike/project/Assets/Art/Textures/stone_ashlar_n.png`

场景、双平台包及运行时截图也由 brief 中的命令重新生成。其他材质生成函数未改。

## 2. 最终 8 条验收实测

最终截图：`spike/project/Shots/art_close.png`（1920×1080）；运行日志：`spike/logs/20_runtime_035603.log`。以下命令按 brief 顺序实际执行。

| # | 检查 | 最终实测数字 | 判定 |
| --- | --- | --- | --- |
| 1 | region：墙 `1400,200,1750,420` | p5=71.4，p50=145.9，tail=0.489（基线 0.529）；p50 相对 145.9 为 0.00%；clipped=0.00% | 达标 |
| 1 | region：门廊 `700,600,1100,760` | p5=65.0，p50=141.7，tail=0.459；p50 相对 145.7 为 -2.75%；clipped=0.07% | 达标 |
| 1 | region：墙脚 `1300,760,1750,900` | p5=93.3，p50=160.0，tail=0.583；p50 相对 154.2 为 +3.76%；clipped=0.00% | 达标 |
| 2 | patchwork，tile=64 | tiles=15，meanL=139.9，blockSpread=21.4% ≤24%，neighborDiff=7.3% ≤10%，fineGrain=23.22 | 达标 |
| 3 | texstat | 256×256，meanRGB=(125,121,112)，coarseStd(32px)=16.39 ≥16，saturation=0.104 ≤0.16，edge%=4.9（要求 4–8） | 达标 |
| 4 | seam | inner(col,row)=7.38/7.10，wrap=4.74/9.21，ratio=0.64/1.30（均 ≤1.60） | 达标 |
| 5 | sharp | top=5.05，middle=10.85 ≥10，bottom=4.88 | 达标 |
| 6 | scene 日志 | church heightPct=49.8，pitch=11.8；stone tiling=(0.20833333, 0.20833333)，stone bump=1.80 | 达标 |
| 7 | build 日志 | macOS Succeeded，errors=0，size=120773800 B；Windows Succeeded，errors=0，size=102794820 B；error CS=0 条 | 达标 |
| 8 | SSAO 日志 | intensity=0.75，radius=0.6，source=Depth；features=2，entry0=TiltShiftFeature，entry1=SSAOFeature | 达标 |

第 1 项仅墙区要求 tail≤0.500；另外两区按 brief 检查 p50 和 clipped。p50 变化百分比用命令显示的一位小数计算。

第 5 项工具还显示 top/mid=0.47、bottom/mid=0.45；工具提示的 `<0.45` 不满足严格小于，但这不是 C 节第 5 项硬门槛（硬门槛为 middle≥10）。本轮没有调整移轴参数。

实际执行的 8 条命令：

```sh
python3 spike/tools/region.py spike/project/Shots/art_close.png 1400,200,1750,420 700,600,1100,760 1300,760,1750,900
python3 spike/tools/patchwork.py spike/project/Shots/art_close.png 1400,200,1750,420 --tile 64
python3 spike/tools/texstat.py spike/project/Assets/Art/Textures/stone_ashlar.png
python3 spike/tools/seam.py spike/project/Assets/Art/Textures/stone_ashlar.png
python3 spike/tools/sharp.py spike/project/Shots/art_close.png
grep -E '\[SPIKE\] frame|\[SPIKE\] stone' spike/logs/122_scene.log
grep -E 'Succeeded|error CS' spike/logs/124_build.log
grep -E '\[POST\] ssao|\[POST\] renderer' spike/logs/120_ssao.log
```

最终第 8 项原始日志：

```text
[POST] ssao feature=Assets/Settings/SSAOFeature.asset intensity=0.75 radius=0.6 source=Depth
[POST] renderer features=2 entry0=TiltShiftFeature entry1=SSAOFeature
```

## 3. 调整过程与踩坑

1. 初版只有倒角/块心变化时，coarseStd=9.76，仍低于 16。将连续低频斑驳幅度从 40 调至 88，并让同一低频场参与高度起伏；没有扩大每块随机 light 的范围。该轮 coarseStd=16.41、saturation=0.099、edge%=5.1，seam=0.74/1.35。
2. 首轮运行时 tail=0.477、blockSpread=21.9%、neighborDiff=7.1%、middle=11.60，但三块 p50=152.3/147.9/166.2，第一、三区超出 ±4%。保持 SSAO Radius=0.6、Intensity=0.75，先调整直接影响中位亮度的块面统一偏移。
3. 偏移 -6 并统一流痕混色为中性后，p50=147.3/141.9/160.4；第三块仍略高于精确上限 154.2×1.04=160.368，不能把四舍五入后的“约 4%”当作通过。最终改为 -7，灰缝像素抖动收窄至共享 ±2，再从贴图生成起完整重跑，得到本报告的最终数字。
4. 为防止修灰缝时意外重排块宽，保留原灰缝生成的随机数消耗次数；倒角边缘也保留原有随机采样次数。初次草稿曾改变随机数消耗并出现较差接缝，已修正后再进行上述验收。
5. URP 的 settings 类为 internal；只通过 `m_Settings.Intensity` 等完整 SerializedProperty 路径配置。Source 用 enumValueIndex=0；没有访问 internal 类型，也没有依赖 TerrainHaze 缺失的 DepthNormals pass。重复调用 SSAO() 后磁盘重导入断言仍为两个 feature。
6. Unity 的 Vector2.ToString("F8") 在该环境输出 `(0.20833330, 0.20833330)`。将已有 float 分量转 double 再按 F8 输出，得到实际 float 值对应的 `(0.20833333, 0.20833333)`；没有改 tiling。
7. SSAO 入包不是仅凭编辑器日志判断：最终构建日志两次列出独立 `Assets/Settings/SSAOFeature.asset`（0.1 kb），以及 `ScreenSpaceAmbientOcclusion.shader`（macOS 37.0 kb、Windows 33.6 kb）。AOMethod=1，避免依赖 BlueNoise 动态纹理。最终 macOS 运行日志的 SSAO 资源缺失/BlueNoise 加载失败/Missing material 检查为 0 条。

完整生成链按 brief 执行：`python3 spike/tools/make_textures.py` → Unity `SpikeURP.SSAO`（120_ssao.log）→ `SpikeScene.Run`（122_scene.log）→ `SpikeBuild.Run`（124_build.log）→ `caffeinate -dimu -t 150 bash spike/tools/capture_runtime.sh`。Unity 路径为 `/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity`，各次均使用 `-batchmode -quit -projectPath spike/project`。

## 4. 完成范围与限制

A/B 已实现，C 的生成链与 8 条硬门槛全部实跑达标。SSAO 的 11 个配置参数均采用 brief 建议值；没有修改机位、相机、地形几何、雾、远山或移轴参数。

运行截图验证来自 macOS 播放器；Windows 完成构建和入包检查，本机没有执行 Windows 播放器。没有把 Windows 运行时效果声称为已验证。
