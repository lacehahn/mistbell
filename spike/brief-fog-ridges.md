# 派遣任务（队列 4）：远山不再是纸片 + 雾的分层纵深

## 现状（已确诊，不用重查）

- 远山在 `spike/project/Assets/Editor/SpikeScene.cs` 的 `BuildMountains()`：3 层三角带网格（距离 250/450/700 m，
  基底沉到 y=-275/-215/-90），材质是 `Universal Render Pipeline/Unlit` 纯色（#4E6478 / #6E8399 / #93A6B8），
  外加第 3 层前面贴一条 `Mountain_Snow` 纯白四边形。结果：**平涂纸片 + 一道白描边**，没有纵深、没有雪线过渡。
- 雾目前只有 `RenderSettings` 线性雾：start 80 / end 1800，等于几乎不生效；远山 250–700 m 之间没有任何空气透视。
- 参考目标（`art-ref/notes-winter-hd2d.md` 与 `Shots/art_runtime.png` 的差距）：远山要**三层深浅分明、山脚沉进雾里、
  雪线是渐变不是描边**；近景与中景之间要有一条可读的雾气带，才能撑出「微缩模型」的纵深。

## 只允许改这些文件

- `spike/project/Assets/Editor/SpikeScene.cs`（`BuildMountains()`、雾参数、新增 `BuildMist()`；机位表**不许动**）
- **新增** `spike/project/Assets/Art/Shaders/RidgeFog.shader`（远山专用）
- `spike/tools/make_textures.py`（只增加一类 `mist` 1024×256 可平铺软噪声 alpha 贴图，输出到
  `spike/project/Assets/Art/Textures/mist.png`；已有三类贴图的生成逻辑与参数**不许动**）
- 产物：`spike/project/Shots/*.png`、`spike/logs/8*_*.log`

**不要**改：`Fov`、机位表（`art_runtime` / `art_close` / `art_ground` 的位置与朝向）、`build_church.py`、
教堂几何、角色、后期 `BuildRuntimeProfile()`、`Screenshotter.cs`、`SpikeBuild.cs`、`docs/`、`AGENTS.md`。

## 要做的事

### 1. 远山着色器 `Mistbell/RidgeFog`

一个不透明、无光照、无阴影的 URP shader，输入世界坐标：

- **垂直渐变**：`_ColorBottom`（山脚，暗、偏冷灰蓝）→ `_ColorTop`（山脊，亮），按世界 Y 在
  `_GradLow`..`_GradHigh` 之间 lerp，并用 `pow(saturate(t), _GradPow)` 让亮部集中在上缘
- **雪线**：世界 Y 超过 `_SnowHeight` 时按 `_SnowSoftness` 做柔和过渡混入 `_SnowColor`，
  过渡带用一层极轻的噪声（可复用 `mist.png` 的一通道）打散成不规则雪线，**不要**再单独放白色四边形
  （删掉 `Mountain_Snow` 的生成）
- **空气透视**：shader 内用 URP 的 `MixFog()`（务必启用 `_FOG` 相关关键字 / `ApplyFog` 宏），
  另加 `_FogFloor`（0..1）：让山脚额外的雾化强于山脊，形成「山脚沉进雾里」
- 边缘不能出现纯白/纯黑描边（现有白描边的成因就是 snow 四边形；去掉后必须目视确认没有第二处）

三层山的参数建议（可直接调）：第 1 层（250 m）bottom #3C4A5C / top #6C7F92 / snow 不启用；
第 2 层（450 m）bottom #56697C / top #8CA0B2 / snow 高度 300 m；第 3 层（700 m）bottom #7C8FA2 /
top #B7C6D4 / snow 高度 250 m。三层雪线高度**必须不同**，否则又变成一条横线。

### 2. 雾的分层

- `RenderSettings` 线性雾：`fogStartDistance` 80 → **55**，`fogEndDistance` 1800 → **650**，
  `fogColor` 改成偏冷的 `#AFC2D6`
- **新增 `BuildMist()`：2 条水平雾带**，用 `mist.png` + Unlit 透明材质（alpha ≈ 0.22 / 0.30，
  `_Cull` 关、`ZWrite` off、透明队列），尺寸约 900×380：
  - 近带：中心 z = **-170 m**、y = 地面该处高度 + 6 m、朝向水平（平放）
  - 远带：中心 z = **-330 m**、y = 地面该处高度 + 14 m
  - 两层贴图 tiling 不同（例如 3×1 与 5×1）以免看出重复
- 雾带不能盖住教堂（最近处 z 必须 ≤ -150）也不能横切角色（角色在 z≈-80 附近）

### 3. 自检日志（我要用命令验收，必须打印）

```
[SPIKE] fog start=55 end=650 color=...
[SPIKE] mist near z=-170 y=... far z=-330 y=...
[SPIKE] ridge 1/2/3 shader=RidgeFog snowHeight=... 
```

## 验收（必须自己跑，把数字贴回回答；分步跑，不要串成一条长链）

```bash
cd /Users/lance/workspace/mistbell
python3 spike/tools/make_textures.py            # 应仍能产出 grass/stone/roof + 新增 mist.png
pkill -f "Unity.app/Contents/MacOS/Unity" ; sleep 2
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/80_scene.log ; echo SCENE=$?
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/81_build.log ; echo BUILDAPP=$?
caffeinate -dimu -t 180 bash spike/tools/capture_runtime.sh
for f in art_runtime art_close art_ground; do python3 spike/tools/frame.py spike/project/Shots/$f.png; done
```

必须同时满足：

1. `SCENE=0`、`BUILDAPP=0`，`grep -c "error CS"` 为 0，`Shots/art_*.png` 三张都重新落盘
2. `[SPIKE] frame church heightPct ∈ [45,55]`、`pitch ∈ [10,14]`、`frame porch centerX ∈ [760,1160]`
   （证明机位与几何没被动）
3. 三张图 `clipped>=250` 全部 **< 3%**（当前 0.00 / 0.23 / 0.10，不许因为雾/雪回涨）
4. `art_runtime.png` 的 `sky` 区域均值仍 ≈ **(167,183,203)**（±10），雾不许把天空染色
5. `grep -E "fog start|mist near|ridge [123] shader" spike/logs/8*_scene.log` 三条日志都在
6. 目视：`art_runtime.png` 里三层山**能数出三条不同深浅的轮廓**、雪线是软过渡、山脚融进雾；
   `art_ground.png` 不要被雾带糊住（草地仍要看得清像素颗粒）

**跑不动就贴卡住那一步的日志**，不要为了过数字删掉雾或把山调回纯色。
