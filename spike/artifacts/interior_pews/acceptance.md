# 室内长椅、吊链、侧龛验收

2026-11-04。通过 `InteriorPewsBuild.Run` 在现有场景局部更新；不调用 `SpikeScene.Run` 或 `TweakInteriorMats.Run`。未修改 AGENTS.md，未提交。完整任务严格验收状态以 `measurements.json` 的 `accepted` 和逐项 checks 为准。

## 规格与实际改动

- 长椅每侧6排、合计12排；外宽2.35m；座面深0.42m、厚0.05m，顶面距地0.48m；靠背高0.42m、后倾6°，背板厚0.05m。两端端板厚0.06m，自座面上方起，避免端板落地形成矮墙；每排4条0.06×0.06m方腿，腿高0.43m。
- 上沿收边截面0.02m高、0.055m深，底色和自发光均相对原椅背×1.11，复用木纹。结合后倾椅背与收边条的最远伸出量，行距0.80230m、真实外轮廓净空0.28000m。两侧排中心x=-4.15/0.15m，首排z=0.55m，其后递增行距。座面宽度包含两端端板，不额外加宽。
- 保留12组接地阴影，并跟随48条新椅脚重建；每片0.24×0.18m、黑色alpha0.20、距地0.008m。旧导入长椅及其碰撞保持，继续沿用既有室内隐藏/室外显示逻辑；本轮装饰新几何不增加碰撞体。碰撞与新外形并非逐面贴合，维持既有障碍测试约定。
- 吊灯环从x=0移至中殿轴x=-2m，蜡烛、小烛灯和室内吊灯主灯位置同步。真实环高为世界y=3.400m，任务中提及的7.2m来自旧灯环，未据此抬高目前可见吊灯。天花吊杆顶y=8.600m；下端3节交错椭圆铁链，每节外长0.16m、铁丝直径0.03m、外宽0.10m，节中心间距0.13m（留交锁重叠），再由3条斜撑连接环缘。材质沿用 `M_InteriorIron`。三节链无法单独跨过5.2m高差，所以明确采用“长吊杆＋短链＋斜撑”结构。
- 两侧拱背板前各安装0.34×0.52m画面，四周0.02m金框，外框0.38×0.56m。左用 `icon_left`、右用 `icon_mid`，原贴图Point/no-mipmap不变；两个独立Lit材质底色×0.5，图像自发光倍率分别0.96/1.23，供昏暗环境中的可读性补偿。两张原图的平均色与明度不同，因此使用不同倍率。祭坛材质、模型、UV和贴图均不改。

## 验收口径

- 同一局部入口连续应用两次。保护快照逐项对比所有非本轮家具渲染器、网格、材质、相机、非吊灯灯光及全部碰撞；地板（包括场景内嵌材质）、室外、祭坛、王门、穹顶与上一轮光柱/明暗材质均纳入。
- 原机位在实际可渲染网格上建立临时查询碰撞，逐排沿收边采17点，命中自身收边才计为可见，查询对象在同一帧移除；不修改游戏碰撞。Python再从最终PNG复算亮边相对其下方3px椅背的对比度，至少3个有效点且平均对比≥3%才计一排；两侧均须≥5排。此图像对比受景深、光照、材质影响，不等同于材质色倍率。
- 侧龛亮度使用画面内部11×15网格的实际可见投影点，PNG采样后先平均RGB再转线性Rec.709，**分别**要求0.05–0.09。此指标是两块圣像画面的亮度，不是含金拱和黑暗边缘的整个壁龛平均值；不混用显示空间亮度。
- 上一轮明暗均衡、穹顶肋/顶孔及接地指标由最终截图独立复算，原演示光照/构图/障碍门槛保持。4m近景用临时检查相机拍摄，原相机参数与原机位未改。

## 已确认的规格冲突

上一轮 `interior_balance.md` 的实际成品是20盏灯，包含新增 `Interior_ChancelFill` 与 `Interior_NaveFill_Balance` 两盏补光。当前任务同时要求保留该轮效果与“lamps=18不变”。本轮保留20盏和原20盏演示断言，未删补光、未更改计数、未放宽任务18盏门槛。因此严格验收脚本的 `lamps_18=false`，退出码为1；其他项须分别查看，不能把完整任务称为全通过。

## 复现命令

仓库根目录串行执行；播放器保持前台，性能测试时不要并行启动其他播放器或Unity构建。

```sh
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath spike/project -executeMethod InteriorPewsBuild.Run -logFile spike/logs/interior_pews_build.log
caffeinate -dimu -t 240 open -W -n -a "$PWD/spike/project/Builds/Mac/Mistbell.app" --args -demo-enter-church -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$PWD/spike/logs/interior_pews_interior.log"
caffeinate -dimu -t 240 open -W -n -a "$PWD/spike/project/Builds/Mac/Mistbell.app" --args -bench -bench-foreground -bench-output "$PWD/spike/logs/interior_pews_bench.json" -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$PWD/spike/logs/interior_pews_bench.log"
python3 spike/tools/verify_interior_pews.py
```

新建完整场景也在上一轮 `InteriorBalanceBuild.Apply` 后调用同一局部Apply；本次没有运行全场景生成。清理旧吊杆时同步移除其穹顶切换表条目，防止残留空引用；其余鼓座/外墙切换表逐项保留。

[原机位截图](../../project/Shots/interior_inside.png) / [4m长椅近景](../../project/Shots/pews_closeup.png) / [全部测量](measurements.json)。截图、场景、生成材质/网格和Mac构建沿用仓库忽略规则保留本地。

## 最终实测

| 项目 | 实测 | 门槛 / 结果 |
|---|---:|---|
| 左/右可见亮边排数 | 6 / 6 | 各≥5，通过 |
| 最小排间净空 | 0.28000m | 0.28m，通过 |
| 左/右圣像画面线性亮度 | 0.07208 / 0.08277 | 各0.05–0.09，通过 |
| 左墙 / 右墙 / 左右比 | 0.06964 / 0.05291 / 1.31619 | 上轮门槛通过 |
| 中殿线性亮度 | 0.03400 | 0.030–0.050，通过 |
| 肋对比 / 顶孔显示亮度 | 0.24033 / 0.96016 | ≥0.10 / ≥0.55，通过 |
| 接地阴影组 / 脚底最大亮度差 | 12 / 0.00751 | 12 / ≤0.02，通过 |
| 室内drawcalls | 258 | ≤290，通过 |
| 室内平均FPS / 1% low | 230.40 / 168.10 | 平均≥200，通过；1% low只记录 |
| INT_PASS | 所有布尔True，obstacles=True | 原演示通过，播放器退出0 |
| 灯数 | 20 | 要求18，**未通过** |

环境：Apple M6、Metal、Unity6000.3.27f2、1920×1080开发播放器、vSync0。性能沿用现有独立bench，每机位至少3秒与180帧预热、300帧采样。构建Succeeded/errors=0，最终进堂运行无NullReference/MissingReference异常，`git diff --check`通过。两张最终交付截图已目视检查。

`python3 spike/tools/verify_interior_pews.py` **exit=1**：17项检查中16项true，仅`lamps_18=false`。未将该退出码包装成完整通过。首轮过暗、调节中过亮的侧龛帧已被最终截图替换，验收只使用最终PNG及其SHA-256；原始运行日志的截图采样与PNG独立复算一致。

```text
[PEW_V2] rows=6 width=2.35 gap=0.28 legs=4 rail=true visible_rows=6 left=6 right=6 measurement=mesh_raycast
[CHANDELIER] chain_segments=3 top_y=8.600 ring_y=3.400
[SIDE_NICHE] panels=2 luma=0.07743 left=0.07208 right=0.08277 space=linear_mean_rgb
[INT_BALANCE] left=0.06972 right=0.05295 nave=0.03387 ratio=1.31684
[DOME_SKIN] plaster=true ribs_visible=true rib_contrast=0.22211 oculus_luma=0.96086 plaster_luma=0.12802 rib_luma=0.15646 rib_pixels=1968 oculus_pixels=214
[PEW_GROUND] shadows=12 edge_delta=0.00751 samples=48 blend=alpha alpha=0.20
[INT_PASS] light=True domeLight=True ceilingSeen=True domeSeen=True fx=0.793 visible=True obstacles=True walked=True z=0.43 dy=0.000 fov=50.00 lamps=20 shafts=3
```

构建日志 `logs/interior_pews_build.log`，进堂日志 `logs/interior_pews_interior.log`，性能 `logs/interior_pews_bench.{log,json}`；采样坐标 `project/Shots/interior_pews_samples.json`，测量报告保存最终图片SHA-256。
