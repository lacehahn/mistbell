# 室内明暗均衡、穹顶与长椅接地验收

2026-11-04。仅局部更新既有 ArtSpike 场景；未运行会重写地板的 `build_interior.sh`。以本报告下方复现命令和 `measurements.json` 为验收依据。未修改 AGENTS.md，未提交。

## 光照最终数值

- 三盏 `Interior_WindowKey*`：45° → **39°**，强度2.5 → **2.125（-15%）**，range22、innerSpotAngle26、原颜色不变。
- `Interior_FloorBounce`：18 → **22 m**，0.75 → **0.95**，保持 **#8A6A4E**；位置改为 `(-2,FloorY+0.30,3)`，ForcePixel。
- 新增 `Interior_ChancelFill`：位置 `(-2,FloorY+4,7.5)`，Point / **range12 / intensity0.45 / #C9B79A / shadows=None**。
- 为达到中殿0.030下限，另增 `Interior_NaveFill_Balance`：位置 `(-2,FloorY+1,1.5)`，Point / **range10 / intensity1.20 / #C9B79A / shadows=None**。仅在室内启用，复用既有进出室内开关逻辑。指定反弹光的20组位置实测最高仅约0.0195，未通过，故补充此灯，而未改地板材质或放宽亮度门槛。
- 独立加法光柱原来不随真实聚光灯参数变化。三道光柱宽 **1.65 → 1.40 m**，材质HDR RGB由(2.8,2.35,1.85)降为 **(0.70,0.5875,0.4625)**，alpha仍0.08、加法仍保留。光柱材质降幅75%，与真实灯光降幅15%分别记录；地面光斑不改。
- 室内专用 `M_IndoorShell` / `M_InteriorWallFoot`：底色为 **#7A6B5C ×0.80**，自发光 **(0.320,0.272,0.224)**，关闭镜面高光，保留墙面纹理。仅影响室内切换用材质，室外材质不变。
- 进堂时附加灯上限 **4 → 8**，出堂恢复进入前的值；URP配置资产不改。八盏吊灯小烛光改为ForceVertex，强度0.35、range1.2不变，为主要补光保留像素灯优先级。总灯数18 → **20**，相应参数断言同步，原功能/亮度门槛不放宽。

## 穹顶最终数值

- `Interior_DomeInner`使用独立无贴图Lit抹灰：**#8A7A66，roughness0.85（smoothness0.15）**；暖色底光为底色×0.20，避免抬头时完全黑掉。鼓座石墙保持原样。
- 新增 **12根锥顶肋**，每根宽 **0.12 m**、内向出挑 **0.05 m**，每根拆成两个0.06m面；明面为抹灰色×**1.11**，暗面×**0.91**，与抹灰相同的粗糙度及底光倍率。原12根鼓座肋保留。
- 原0.80m顶孔孔径不改；天空片改为 **#D9EBF9、自发光×1.60**。孔边加 **0.06m**宽金环，内半径0.40、外半径0.46m，**#C9A227**。
- 测量使用480×270几何射线分类，包含深度遮挡；保存 `interior_dome_mask.png`，红=肋、绿=抹灰、蓝=顶孔。Python从本轮1920×1080截图复算显示亮度与肋/抹灰均值的相对差，结果见JSON。该指标为整组可见表面的均值对比，不宣称逐肋逐像素均达到相同比例。

## 长椅最终数值

- **12组阴影网格（6排×2侧）**，每组根据现有四个椅脚的世界包围盒自动生成四个 **0.24×0.18m**贴片，共48个脚底贴片，落在FloorY+0.008m。`[PEW_GROUND] shadows=12`计数为12组长椅，不将48个贴片伪称12个脚。
- **#000000 / alpha0.20**，不写深度，不投射额外实时阴影。任务“黑色加法混合”不能产生压暗效果，采用 **SrcAlpha / OneMinusSrcAlpha**透明混合；此为明确的规格取舍。原长椅模型、椅脚材质和碰撞不改。
- 记录48组椅脚底沿/紧邻地面投影像素对，以线性Rec.709亮度的**最大绝对差**验收≤0.02。投影采样包含原景深和遮挡效果，不用亮边补偿。

## 保护与验收

`InteriorBalanceBuild.Run`连续应用两次，确认幂等。更新前后逐项比对：地板材质资产文本、全部室内碰撞序列化及变换、室外渲染器/网格/材质、相机、室外灯、祭坛/王门/两侧拱与背板。保护断言均通过；原地板内嵌材质也不经任何赋值或重建。祭坛模型、圣像贴图及王门源文件不改。

构建Succeeded / 0 errors；最终 `-demo-enter-church` 的 `[INT_PASS]`各布尔项True，包括obstacles=True。Python验收脚本同时检查原功能日志、截图尺寸/新鲜度、保护断言和独立性能数据；未改原亮度/功能门槛制造通过。

截图：[室内](../../project/Shots/interior_inside.png)、[抬头](../../project/Shots/interior_lookup.png)。已目视检查墙面石纹、收窄光柱、抹灰锥顶、放射肋与长椅底部。

## 复现命令

在仓库根目录串行运行。显示器保持唤醒，播放器用open保持前台；不要同时启动多个播放器。生成场景/材质/网格/构建和截图沿用仓库忽略规则保存在本地。

```sh
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath spike/project -executeMethod InteriorBalanceBuild.Run -logFile spike/logs/interior_balance_build.log
caffeinate -dimu -t 240 open -W -n -a "$PWD/spike/project/Builds/Mac/Mistbell.app" --args -demo-enter-church -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$PWD/spike/logs/interior_balance_interior.log"
caffeinate -dimu -t 240 open -W -n -a "$PWD/spike/project/Builds/Mac/Mistbell.app" --args -bench -bench-foreground -bench-output "$PWD/spike/logs/interior_balance_bench.json" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$PWD/spike/logs/interior_balance_bench.log"
python3 spike/tools/verify_interior_balance.py
```

新增入口位于 `Assets/Editor/InteriorBalanceBuild.cs`；全场景首次生成也在原有ApplyLighting完成后调用同一Apply。仅需重现本轮时使用局部入口，不要调用旧的全场景+地板改色脚本。

## 最终实测（Python从最终截图复算）

| 指标 | 实测 | 门槛 |
|---|---:|---:|
| wall_left | 0.06934 | ≤0.09 |
| wall_right | 0.05364 | ≥0.045 |
| left/right | 1.29258 | ≤2.0 |
| nave | 0.03306 | 0.030–0.050 |
| rib_contrast | 0.24122 | ≥0.10 |
| oculus_luma（显示空间） | 0.96006 | ≥0.55 |
| pew_edge_delta（线性空间） | 0.00473 | ≤0.02 |
| 室内drawcalls | 254 | ≤280 |
| 室内FPS | 229.88 | ≥200 |
| 室内1% low | 165.93 | 记录 |

运行时日志采用独立CaptureScreenshotAsTexture帧，颗粒/景深采样与PNG复算存在小差异，二者均通过；不混用线性空间和显示空间。原始运行日志：

```text
[INT_BALANCE] left=0.06941 right=0.05368 nave=0.03290 ratio=1.29309
[DOME_SKIN] plaster=true ribs_visible=true rib_contrast=0.22120 oculus_luma=0.96043 plaster_luma=0.12805 rib_luma=0.15637 rib_pixels=1970 oculus_pixels=214
[PEW_GROUND] shadows=12 edge_delta=0.00473 samples=48 blend=alpha alpha=0.20
[INT_PASS] light=True domeLight=True ceilingSeen=True domeSeen=True fx=0.793 visible=True obstacles=True walked=True z=0.43 dy=0.000 fov=50.00 lamps=20 shafts=3
```

环境：Apple M6 / Metal / Unity6000.3.27f2，1920×1080开发播放器，vSync0。每机位至少180帧且3秒预热、300帧采样。全部验收为true；`verify_interior_balance.py` exit=0，`git diff --check`通过。

[完整测量JSON](measurements.json) / [构建日志](../../logs/interior_balance_build.log) / [进堂日志](../../logs/interior_balance_interior.log) / [性能JSON](../../logs/interior_balance_bench.json)。截图SHA-256和48个采样坐标对见测量JSON。
