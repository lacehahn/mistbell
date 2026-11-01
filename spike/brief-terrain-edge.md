# 派遣任务（队列 12）：地形远端收口 —— 消掉「桌面模型」的直线天地交界

## 现状（已确诊）

- 地形是一块 `SpikeArt.Box` 平板：`new Vector3(200, 2, 260)`，中心 z=-30、绕 X 轴 -6°，所以地面覆盖
  z ∈ [-160, +100]、x ∈ [-100, +100]。远端 z=+100 处**直接断成一条边**，在 `art_runtime.png` 里是
  y≈715–760 px 的一条绿色接天空的硬边；三层远山（250/450/700 m）都浮在这条边**后面**，
  山脚被这条边齐刷刷切掉 → 整个画面像摆在一块绿板上的微缩模型。
- 雾已经是 `start 90 / end 1500 / #A7B7CB`（= 天空色），远山 `RidgeFog.shader` 已验收通过，**不要动它们**。

## 只允许改这些文件

- `spike/project/Assets/Editor/SpikeScene.cs`：把「坡地平板」换成程序化地形网格（见下），
  并相应改 `BuildStonePath()` 里对 `Collider` 的引用方式（地形换成 `MeshCollider` 后仍要能 Raycast 到）
- 产物：`spike/project/Shots/*.png`、`spike/logs/9*_*.log`

**不要**改：机位表与 `Fov`、教堂 FBX 与 `ImportChurch()`、角色、`RidgeFog.shader`、雾参数、
后期 `BuildRuntimeProfile()`、`Screenshotter.cs`、`make_textures.py`、`build_church.py`、`docs/`

## 地形要求

**网格**：X 方向 ±150 m（300 m 宽）；Z 方向 **-160 → +560 m**。分辨率分区（免得面数爆炸）：

- |z| ≤ 100：步长 **2 m**
- z > 100：步长 **10 m**（并在这段拉开，形成向下的谷）
- 三角面总数 **< 60000**；UV 用世界坐标（米）直接写（与 `SpikeArt` 的「1 UV = 1 m」约定一致），配 `M_Ground`

**高程剖面**（`h(z)`，必须与现有 `GroundY(z)=tan6°·z` 在 z ≤ 60 段**完全一致**，否则教堂与角色会浮空）：

- `z ≤ 60`：`h = tan(6°)·z`
- `z > 60`：先平缓再下沉成谷：`h = tan(6°)·60 - 0.0012·(z-60)^1.9`
  - 参考值：z=100 → 约 -2 m；z=200 → 约 -14 m；z=400 → 约 -76 m；z=560 → 约 -134 m
- 横向起伏（只在 z > 80 生效，按 `smoothstep(80,140,z)` 渐入）：
  `h += 2.5·sin(x/23)·sin(z/31) + 1.2·sin(x/7.3 + 1.7)`

**物理**：`MeshCollider`（convex=false）；`BuildStonePath()` 与角色的地形射线必须仍然命中
（现有日志 `[SPIKE] pilgrim y=... terrain y=...` 两值必须相等）。

**必须打印的自检日志**：

```
[SPIKE] terrain mesh verts=... tris=... extent=x150 z-160..560
[SPIKE] terrain edge sample z100=... z200=... z400=... z560=...   # 上面 h(z) 的实测值
```

## 额外的地平雾（让远边软下来）

- 在地形远边（z ≈ +420～+560、y ≈ 该处地面 + 6 m）放 **1 条水平雾带**，复用已有
  `Assets/Art/Textures/mist.png` 与 `BuildMist()` 里同样的透明材质做法（alpha ≈ 0.35、tiling 6×1），
  尺寸 700×300。目的是让地形远边**融进天空色**，不是再画一条边
- 这条雾带**不许**遮住教堂（z≈-11..13）与角色（z≈-80）

## 验收（分步跑，贴关键输出；回答前必须用 `--image=spike/project/Shots/art_runtime.png` 亲自看图）

```bash
cd /Users/lance/workspace/mistbell
pkill -f "Unity.app/Contents/MacOS/Unity" ; sleep 2
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/90_scene.log ; echo SCENE=$?
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/91_build.log ; echo BUILDAPP=$?
caffeinate -dimu -t 180 bash spike/tools/capture_runtime.sh
python3 spike/tools/frame.py spike/project/Shots/art_runtime.png
python3 spike/tools/horizon.py spike/project/Shots/art_runtime.png
```

必须同时满足：

1. `SCENE=0`、`BUILDAPP=0`、`error CS` 计数 0，三张 `art_*.png` 都重新落盘（看 mtime）
2. `frame church heightPct ∈ [45,55]`、`pitch ∈ [10,14]`、`frame porch centerX ∈ [760,1160]`、
   `pilgrim y=... terrain y=...` 两值相等（教堂没浮空、角色没浮空）
3. 三张图 `clipped>=250` 全部 < 3%；`sky` 均值仍 ≈ (167,183,203) ±10
4. `horizon.py` 报的 5 列地平线行号 **span ≥ 60 px**（现在 44，说明远边是一条等深的直边；
   下沉地形 + 雾后，各列「草地结束处」会明显不在同一高度）
5. 目视：**画面里不再有「草地一刀切到天空」的硬边**，远山山脚被地形/雾吃掉一部分而不是被一条直线齐切
