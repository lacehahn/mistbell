# brief-stone6 —— 屋顶 tint（队列 18-A）与石材单 texel 砂粒（队列 18-B）

## 边界

1. 只允许改两个文件：`spike/project/Assets/Editor/SpikeScene.cs`（只改屋顶那一处材质）与 `spike/tools/make_textures.py`（只改 `make_stone`）。
2. **禁止通读全仓**。必读文件只有：`spike/tools/make_textures.py`、`spike/project/Assets/Editor/SpikeScene.cs` 的 L28–60（材质区）。不要打开 `docs/`、`SpikeURP.cs`、shader。
3. 不许改：相机、机位、光照、雾、SSAO、移轴、地形、grass 分支、随机种子（stone 23 / roof 37 / grass 11）、`stone` 那三行 tiling 与 `_BumpScale`。
4. 不许 `git commit` / `git push`。

---

## A. 屋顶渲染出来还是暖棕 —— 真因在材质 tint，不在贴图

上一轮已经把贴图改成冷灰：`roof_slate.png` 现在是 `meanRGB=(75,81,89) B>R`。但主视角渲染出来仍是 **`(108,80,63) sat 0.422 R-B +45.2`**。

原因（已定位，别再浪费时间找别的）：`SpikeScene.cs` L41 `var roofMat = ChurchMaterial("roof", "#A06B4A");` —— `#A06B4A` 只是「`.mat` 不存在时的兜底」，而 `spike/project/Assets/Settings/M_Roof.mat` **真实存在**且里面存着一个很暖的 `_BaseColor`。`_BaseColor` 在 URP Lit 里是**乘性**的，把贴图的冷色乘掉了。而 `.mat` 被 `.gitignore` 忽略（`spike/project/*`），所以这是**不可复现的隐藏状态**——和当初石材 tiling 踩的是同一个坑。

`stone` 那边已经有正确的写法（L39–40）：
```csharp
// 固定曝光补偿：原石材 tint × (目标反照率 0.30 / 原目标 0.36)，重复生成不累乘。
stone.SetColor("_BaseColor", new Color(0.675f, 0.63f, 0.575f, 1f));
```

### 要做的

在 `var roofMat = ChurchMaterial("roof", "#A06B4A");` **紧后面**加一行显式 tint（写法照 stone，带注释说明为什么必须写在代码里）：

```csharp
// 屋顶：贴图本身已是深石板冷灰（meanRGB 75,81,89）；_BaseColor 是乘性归一系数，
// 必须写死在代码里——Assets/Settings/M_Roof.mat 被 .gitignore 忽略，写进 .mat 等于不可复现。
roofMat.SetColor("_BaseColor", new Color(0.50f, 0.47f, 0.43f, 1f));
```

**这三个数的推导**（已算过，别重算，若首次测量偏了就按下面的规则改）：

- 用石材当标定：石材贴图 `R-B=+14`、tint `0.675/0.575`、渲染出来 `R-B=+40.6`。反推「太阳 + tonemap」把 R/B 比值放大约 **1.07 倍**。
- 屋顶贴图 `R/B = 75/89 = 0.843`；当前渲染 `R/B = 108/63 = 1.714` ⇒ 当前 tint 的 `R/B ≈ 1.90`（很暖）。
- 要让渲染接近中性（`R/B ≈ 1.05`，即 `R-B ≈ +5`）⇒ `tint R/B ≈ 1.05 / (0.843 × 1.07) ≈ 1.16`。`(0.50, 0.47, 0.43)` 的 `R/B = 1.163`，正好。

### 唯一一次调参许可

如果实测 `R-B` 落在允许带之外，**只允许改这三个数一次**（乘性修正：把 R/B 按实测比例调），然后重跑一遍管线再量。第二次还不中就直接报告真实数字，不要继续磨。

---

## B. 石墙块面还是「平滑石膏」—— 缺单 texel 砂粒

现在的石材最小噪声 cell 是 `size // 32` = 16 texel（512 下 = 15 cm），也就是说**整张贴图里没有任何一个像素尺度的随机**。所以近读（`art_close`，143 px/m，1 texel ≈ 1.34 px）看到的是一张平滑面 + 几团大云斑，不像砂岩。

### 要做的

在 `make_stone` 里给**块面**（`edge >= 2`、非灰缝）叠加真·单 texel 砂粒：

- 颜色：`img[y][x] += rnd.uniform(-7, 7)` —— **三通道共用同一个偏移**（保持中性；上一轮好不容易才把粉/绿彩线去掉，别再逐通道加随机）。
- 高度场：`height[y][x] += rnd.uniform(-0.04, 0.04)` 同频微扰，让法线也有砂粒。
- 灰缝里**不加**（`height <= 0.1` 的位置跳过），否则缝会重新变毛。
- 不要成团、不要沿着块的边界、不要引入任何周期性。用 `rnd` 直接取就好。

同时**保留**已有的低频/中频噪声与倒角，别为了加砂粒把 `blotch` 或 `grain` 再调大。

### 不许破坏的（同一条门槛，原样复测）

| 命令 | 门槛 |
| --- | --- |
| `python3 tools/texstat.py project/Assets/Art/Textures/stone_ashlar.png` | `512x512`；`saturation ≤ 0.13`；`coarseStd(32px) ≥ 14`；**`edge% ≥ 12.0`**（现在 9.1，砂粒要把它顶上去） |
| `python3 tools/seam.py project/Assets/Art/Textures/stone_ashlar.png` | 两个 ratio 都在 `[0.55, 1.80]` |
| `python3 tools/texstat.py project/Assets/Art/Textures/roof_slate.png` | `saturation ≤ 0.16`（屋顶贴图这次不许动） |
| `python3 tools/texstat.py project/Assets/Art/Textures/grass.png` | 仍是 `128x128 meanRGB=( 79, 90, 58) saturation=0.349` |

---

## 渲染验收（只允许跑 **一轮** 管线，命令照抄）

```bash
cd /Users/lance/workspace/mistbell
U=/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity
"$U" -batchmode -projectPath spike/project -executeMethod SpikeScene.Run  -logFile "$PWD/spike/logs/144_scene.log" -quit;  echo "SCENE=$?"
"$U" -batchmode -projectPath spike/project -executeMethod SpikeBuild.Run  -logFile "$PWD/spike/logs/146_build.log" -quit;  echo "BUILD=$?"
caffeinate -dimu -t 200 bash spike/tools/capture_runtime.sh
```

**`caffeinate` 不能省**：显示器休眠时 Unity 播放器主循环不推进，截图会「静默失败」而不是报错（这是我们踩过的坑，不是保险起见）。

然后逐条量（`cd spike/project`）：

| # | 命令 | 门槛 |
| --- | --- | --- |
| R1 | `python3 ../tools/region.py Shots/art_runtime.png 920,430,965,462` | `sat ≤ 0.22`、`R-B ∈ [-6, +14]`、`p50 ∈ [70, 112]`（现在 0.422 / +45.2 / 83.9） |
| R2 | `python3 ../tools/sharp.py Shots/art_close.png` | `middle ≥ 13.0`（现在 11.73） |
| R3 | `python3 ../tools/region.py Shots/art_close.png 1400,200,1750,420` | `sat ∈ [0.26, 0.33]`、`clipped ≤ 0.15%`（现在 0.289 / 0.06%） |
| R4 | `python3 ../tools/frame.py Shots/art_runtime.png` | `clipped 0.00%`、`sky=(167,183,203)`、`ground_near=(67,90,28)`，三通道差都 ≤ 3 |
| R5 | 构建日志 `grep -c "error CS" spike/logs/146_build.log` | `0`，且 `[SPIKE] macOS/windows: result=Succeeded` |

R4 是防你手滑动了光照/曝光的守卫：**天空与近景草地这两个数一个字都不许变**。

---

## 交付时请在回复里列出

1. 两处 diff 的原文（`SpikeScene.cs` 那几行 + `make_stone` 加砂粒那几行）。
2. 上面 B 表 4 条 + R 表 5 条的**真实输出数字**，照抄终端。
3. 做不到的说做不到 + 原因。**不要**因为跑不完就编数字。
