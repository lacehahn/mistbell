# 祭坛第二轮验收（2026-11-04）

状态：曝光、布褶、附件、模型预算已实现；原机位十字顶端仍被高壁龛遮挡，**不判定整项任务通过**。完整数值以本文最终实测表和原始日志为准。

## 改了哪些数

- 几何：台布四褶，中心 x=-0.78/-0.26/0.26/0.78 m，名义起伏 0.035 m、宽 0.06 m、下垂 0.34 m，下沿距地 0.70 m。网格明确采样每个峰和两侧边界；保留 0.012 m 厚度、2 段倒角。窄褶采用风格化自定义法线，谷部倾斜系数 0.045，以免形状陡变造成黑条。
- 烛台：总高 0.16 m、盘径 0.10 m、杆径 0.035 m，三座 x=-0.44/0/0.44 m，局部 z=+0.15 m；蜡烛从台面上 0.16 m 起，高 0.12 m。圆柱由 16 边改为 12 边、台布横向基础分段由 128 改为 96，给新增附件腾出三角面预算。
- 每龛加入 0.30×0.46 m 板，内画 0.26×0.42 m，四周 0.02 m 金边；三块使用同一张格鲁吉亚风格圣像画，不视作考据复原。
- 图集：仍为单张 1024² `altar_stone.png`，imagegen 在原图上压暗框石、重绘暖白亚麻并加入图标。仍由 `ingest_handpaint.py --altar-only` 导入，不改室外图集。框石水平顶面从同一图集选取较亮石材区域；按每面 UV 包围盒 3×3 采样、按面面积加权，线性底色顶面/正立面为 **1.09983**，即 +9.98%，不依靠高光。
- 材质仍为 2 个：主材质 `Mistbell/AltarPainted`（URP Lit 变体），底色乘 (0.45,0.45,0.45)，亚麻区域额外乘 1.2，最终亚麻乘数 0.54；`_EmissionColor=(0,0,0)`、smoothness=0.08、metallic=0。龛板继续使用 URP Lit，共用图集，底色乘数由 0.82 改为 1.0，纹理自发光乘数 0.70，smoothness=0.08。自发光只用于龛板，不流入台布或框石。
- 祭坛主材质不接收阴影，也不使用屏幕 AO（保留普通直射、后期、雾及纹理）。现有 AO 半径 0.5 m / 强度 2.2，会把 0.06 m 褶压成黑条；此变体只影响祭坛主材质，未改变全局 AO、室外材质、室外灯光或相机。龛板材质仍接收原来的 AO/阴影。
- 将既有室内 `Interior_AltarWash` 从入口移到 (-2,FloorY+0.05,7.6)，朝向 (-2,FloorY+0.30,8.72)，外角/内角 100°/60°。保留强度 1.35、range=9、原暖色和无阴影；没有增加灯。原位置 z=-7.4 与祭坛相距超过 9 m，无法代替主材质自发光照亮祭坛。
- 按任务字面将模型根 z=8.6→8.72（+0.12 m），保留 `Interior_Altar` 和原 BoxCollider 尺寸。高壁龛只抬下沿 0.05 m，顶端不动；更新过程幂等，全场景生成也输出相同下沿。

## 最终实测

| 指标 | 数值 | 门槛 / 结果 |
|---|---:|---|
| tris | 8,880 | ≤9,000，通过 |
| 材质槽 | 2 | ≤2，通过 |
| 本体 bbox | 2.2400×1.0500×1.0251 m | 通过 |
| UV 面积密度 | 139.000 px/m | 110–150，通过 |
| 台布亮度 | 0.13257 | ≤0.22，通过 |
| 框条亮度 | 0.11417 | ≤0.42，通过 |
| 坛体亮度 | 0.20503 | 记录值，通过 |
| 龛内亮度 | 0.10027 | 0.09–0.13，通过 |
| 台布/坛体 | 0.64662 | ≤1.25，通过 |
| 褶峰/谷明暗差 | 11.05% | 10–15%，通过 |
| 室内 drawcalls | 236 | ≤260，通过 |
| 室内平均 FPS | 236.84 | ≥200，通过 |
| 室内 1% low | 176.60 FPS | 附录，不作为平均帧率承诺 |
| 进堂演示 | 全绿，obstacles=True | 通过 |
| 十字顶端可见 | False | 未通过 |

## 测量方法

`PlayerAltar.cs` 的固定世界空间矩形投影到截图，区域覆盖台布正面、两块裸露坛体、三条龛框竖边、三块图板及四组褶峰/谷；没有根据结果移动取样框。日志 `[ALTAR_ROI]` 给出以左下角为原点的像素坐标。

截图为 1920×1080 PNG，先将每个 sRGB 通道解码为线性值，再用 Rec.709 权重 0.2126/0.7152/0.0722 求均值。褶对比取四组峰/谷均值的相对差绝对值。C# 实测与独立 Python 重算使用同一张原始截图；完整 ROI、像素数和门槛保存在 [altar_v2_luma.json](altar_v2_luma.json)。这些定义明确为线性亮度；任务首轮未附色彩空间/ROI，因此不能把本次和首轮数字当作相同采样条件直接相减。

近景相机距正面石材平面 1.2 m，位置 (-2,FloorY+1.05,7.095)，FOV=88，仅近景检查关闭景深/移轴模糊；保持原场景家具、墙面、灯光与背景可见。没有隔离图层或黑底。原机位截图沿用 `-demo-enter-church`，不改玩法相机和演示判定。

性能采用开发播放器、1920×1080、Metal / Apple M6，vSync=0；各机位至少 180 帧且至少 3 秒预热，采样 300 帧。原始结果 [altar_model2_bench.json](../../logs/altar_model2_bench.json)，`[BENCH_GATE] pass=True`。入堂原始结果 [altar_model2_interior.log](../../logs/altar_model2_interior.log)：`light=True domeLight=True ceilingSeen=True domeSeen=True visible=True obstacles=True walked=True`；演示代码和门限未改。Python 编译、Blender 导出断言、Unity 构建（0 errors）、独立 PNG 亮度重算、`git diff --check` 均通过。

## 十字遮挡：未通过

编辑器 `[ALTAR_CROSS]` 使用实际进堂机位 (-2,FloorY+1.75,-4.78) 朝十字顶部内部点 (-2,FloorY+2.132,9.01) 投射射线，命中 **Interior_V2NicheBack_1**，`visible=False`。`interior_inside.png` 同样不能证明十字顶端可见。

会众实际在 **−Z**，所以任务的“+Z（朝会众方向）”与当前坐标系不一致。+Z 0.12 m 会将祭坛移远；高壁龛位于 z≈7.55/7.61，仍在玩家与祭坛之间。射线穿过壁龛时高度约为 FloorY+2.09 m，而抬高后的板下沿只有 FloorY+1.32 m。继续仅抬 0.05 m 无法解决。要完整露出十字，需要重新安排高壁龛到祭坛后方（例如基准 z=9.35），或另行批准明显更大的下沿裁切；这超出了任务给定的小幅位置修正，本轮未擅自执行。

## 复现

```sh
/opt/homebrew/bin/blender -b --python-exit-code 1 -P spike/tools/build_altar.py
python3 spike/tools/ingest_handpaint.py --altar-only
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath spike/project -executeMethod AltarBuild.Run -logFile spike/logs/altar_model2_build.log
open -n -a "$PWD/spike/project/Builds/Mac/Mistbell.app" --args -demo-altar -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$PWD/spike/logs/altar_model2_closeup.log"
# 等该播放器退出，再执行：
python3 spike/tools/verify_altar_v2.py
open -n -a "$PWD/spike/project/Builds/Mac/Mistbell.app" --args -demo-enter-church -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$PWD/spike/logs/altar_model2_interior.log"
# 等该播放器退出，再执行：
open -n -a "$PWD/spike/project/Builds/Mac/Mistbell.app" --args -bench -bench-foreground -bench-output "$PWD/spike/logs/altar_model2_bench.json" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$PWD/spike/logs/altar_model2_bench.log"
```

模型 [FBX](../../export/gergeti_altar.fbx)、[Blender 源文件](../../export/gergeti_altar.blend)；截图 [场景近景](../../project/Shots/altar_closeup.png)、[原机位](../../project/Shots/interior_inside.png)。模型、Unity 生成资产、构建、日志沿用仓库忽略规则留在本地；生成图集和实现源文件保留在工作区，未提交。未修改 AGENTS.md。
