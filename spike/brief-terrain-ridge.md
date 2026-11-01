# 修正（队列 12 第 2 轮）：地形剖面改成「教堂站在山脊上、背后是山谷」

## 上一轮为什么没效果

你改了网格、也照我给的剖面下沉了，但实测 `horizon.py` 跨度 38 px、画面还是硬直边 —— **是我给的剖面错了**：
`0.0012·(z-60)^1.9` 太缓，z=60 那道坡脊比后面所有点都高，等于在相机视线里立了一道**新的直边**，
后面的谷地和雾带全被它挡住。地形换了、画面没变，就是这个原因。

真正要的画面关系是：**教堂坐在山脊顶端，脊线之后的地面立刻掉下去**（这也正是 Gergeti 的实际地形，
`art-ref/concept-gergeti-sunny.jpg` 里教堂背后就是断崖式的山谷）。这样「草地断在天空里」就不再是bug，
而是有意义的脊线；谷底与远山之间再由雾接上。

## 只允许改 `spike/project/Assets/Editor/SpikeScene.cs`（地形高程函数与雾带 y 值）

**高程剖面改成**（`z` 为世界 z，`g(z)=tan6°·z`）：

- `z ≤ 25`：`h = g(z)` —— 与旧地面**完全一致**，教堂（z≈-11..13）与角色（z≈-80）都不会浮空
- `z > 25`：`h = g(25) − 0.32·(z−25)` —— 32% 的坡直接下坠（z=100 → 约 −24 m；z=200 → 约 −56 m；z=560 → 约 −171 m）
- 横向起伏只作用于脊线之后，振幅要**大到能在轮廓上读出来**：
  `h += 5.0·sin(x/29 + 0.7)·smoothstep(25, 90, z) + 2.5·sin(x/11 − 1.3)·smoothstep(25, 60, z)`
- 网格范围与分辨率沿用你现在的（X ±150、Z -160..+560、近 2 m 远 10 m）不必改

**雾带**：`BuildMist()` 里新增/调整那条地理带，让它落在**谷底**而不是脊后第一段：
- z ≈ **+260**，`y = h(260) + 12`（h 用上面的新剖面算），尺寸 800×320、alpha 0.35、tiling 6×1

**不许动**：机位表、`Fov`、教堂、角色、`RidgeFog.shader`、线性雾参数、后期、`make_textures.py`。

## 验收（分步跑；回答前必须 `--image=spike/project/Shots/art_runtime.png` 亲自看图）

```bash
cd /Users/lance/workspace/mistbell
pkill -f "Unity.app/Contents/MacOS/Unity" ; sleep 2
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/92_scene.log ; echo SCENE=$?
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/93_build.log ; echo BUILDAPP=$?
caffeinate -dimu -t 180 bash spike/tools/capture_runtime.sh
python3 spike/tools/frame.py spike/project/Shots/art_runtime.png
python3 spike/tools/horizon.py spike/project/Shots/art_runtime.png
```

1. `SCENE=0`、`BUILDAPP=0`、`error CS` 0，三张图重新落盘
2. `frame church heightPct ∈ [45,55]`、`pitch ∈ [10,14]`、`porch centerX ∈ [760,1160]`、
   `pilgrim y == terrain y`
3. `art_runtime` 削顶 < 3%、`sky` ≈ (167,183,203) ±10
4. `horizon.py` 五列行号 **span ≥ 60 px**
5. 目视：教堂脚下是地面、**教堂背后能看到地面下坠/被雾接住**，山脚不再被一条直线齐切
