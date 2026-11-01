# 修正（队列 12 第 3 轮）：脊线要起伏 + 雾带必须压在脊线上

## 为什么前两轮画面没变

1. 起伏项的渐入是 `smoothstep(25, 90, z)` —— **在脊线处（z=25）振幅为 0**，所以可见轮廓仍是一条等深的直边。
2. 雾带被我指到 z=260（脊线后面），而脊线本身挡住了它 → 一点也看不见。

两个都是我的参数错，不是你的实现问题。这轮把这两点改掉即可，**不要再动其它东西**。

## 只允许改 `spike/project/Assets/Editor/SpikeScene.cs`

**A. 脊线起伏**（关键：轮廓本身要歪）

- 起伏渐入改为 `smoothstep(8, 50, z)`，并在脊线处就有明显振幅：
  `h += 7.0·sin(x/37 + 0.9)·smoothstep(8, 50, z) + 3.0·sin(x/13 − 1.1)·smoothstep(8, 40, z)`
- 脊线位置本身随横向摆动：把「z ≤ crestZ 段沿用 g(z)」的 `crestZ` 从常量 25 改成
  `crestZ(x) = 25 + 18·sin(x/45)`，`z > crestZ(x)` 起以下坠 `h = g(crestZ) − 0.32·(z − crestZ)`
  （注意：教堂占 z≈-11..13、x≈-17..17，`crestZ(x)` 在该区间的**最小值必须 ≥ 22**，别让台地悬空；
  取 `25 + 18·sin(x/45)` 在 |x|≤17 时约为 25±6.8，最小 18.2 —— 不够，改成 `28 + 10·sin(x/38)`）

**B. 雾带压到脊线上**

- `BuildMist()` 里那条地理带的 z 从 260 改到 **+95**，`y = TerrainHeight(0f, 95f) + 5f`，
  尺寸 900×**220**、alpha **0.45**、tiling **6×1**
- 它必须横过整幅画面宽度（x 方向），并且**盖住「草地结束」那一条** —— 这是本轮唯一目的

**不许动**：机位、`Fov`、教堂、角色、`RidgeFog.shader`、线性雾参数、后期、`make_textures.py`

## 验收

```bash
cd /Users/lance/workspace/mistbell
pkill -f "Unity.app/Contents/MacOS/Unity" ; sleep 2
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeScene.Run -logFile spike/logs/94_scene.log ; echo SCENE=$?
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit \
  -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/95_build.log ; echo BUILDAPP=$?
caffeinate -dimu -t 180 bash spike/tools/capture_runtime.sh
python3 spike/tools/frame.py spike/project/Shots/art_runtime.png
python3 spike/tools/horizon.py spike/project/Shots/art_runtime.png
```

1. `SCENE=0`、`BUILDAPP=0`、`error CS` 0；三张图重新落盘
2. `frame church heightPct ∈ [45,55]`、`pitch ∈ [10,14]`、`porch centerX ∈ [760,1160]`、
   `pilgrim y == terrain y`、教堂没有悬空（台地底部仍贴地）
3. `art_runtime` 削顶 < 3%、`sky` ≈ (167,183,203) ±10
4. `horizon.py` 五列行号 **span ≥ 50 px**（这一轮起绿地的判定会被雾带部分遮住，行号变化越大越好）
5. 目视（必须 `--image=` 读图后再说结论）：**草地与天空之间有一条可见的雾带过渡**，
   草地不再一刀切；教堂与角色不被雾遮住
