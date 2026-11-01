# 派遣任务：雪山氛围（队列 27a / 用户六条批评里的 P0）

## 背景（只读这几处，禁止通读全仓）

Mistbell = HD-2D 探索 RPG 的技术 spike，Unity 6.3 LTS + URP 17，工程在 `spike/project`。
教堂刚按用户设定画重建完（十字主堂 + 独立钟楼），**几何这轮不许动**。

本轮唯一目标：**远山从「纯雾色剪影」变成有体积、有雪的高加索雪山**。

现状（我实测的基线，必须拿它对比）：

- `art_runtime.png` 主视角。三层山是**竖直帘幕网格**（80 段 × 2 顶点），着色器 `Mistbell/RidgeFog` 只做**竖直渐变 + 噪声 + 雪线高度阈值**，因此山体内部**几乎没有明暗**：
  - 左山矩形 `60,470,420,640`：mean(156,173,192) `Lstd 10.1`、**p5/p50 = 0.973**（内部几乎平涂，那 10.1 主要来自矩形里的天空锯齿）
  - 右山矩形 `1450,400,1820,560`：mean(152,169,187) `Lstd 9.7`、p5/p50 = 0.984
  - `sharp art_runtime` 的 `hf top = 0.00`（上三分之一完全没有高频细节）
- 用户批评原文：「雪山氛围不足」→ 验收要求：迎光面比背光面亮 15–25%、最远层有雪线（破碎边缘）、山脚 2 层横向云雾带。

## 只允许改这些文件

- `spike/project/Assets/Art/Shaders/RidgeFog.shader`（主战场）
- `spike/project/Assets/Editor/SpikeScene.cs` 里的 **`BuildMountains()` 与 `BuildMistbands()` 两个方法**（参数、网格细分、云带位置）；**不要动** `BuildChurch`/`BuildProps`/`BuildTerrain`/机位/光照/后期
- 允许新增一张贴图：`spike/tools/make_textures.py` 里加 `make_snow_noise()` → `Assets/Art/Textures/snow.png`（128×128 可平铺灰度，纯标准库写 PNG，风格照该文件已有函数）
- **不要**改：`spike/export/*.fbx`、`Assets/Art/Church/*`、`PixelBillboard.shader`、灌木/石块、任何 docs

## 实现要求（按顺序做，每条都要能验收）

1. **山体方向光（核心）**：现在是平涂渐变，没有法线。给帘幕网格**沿 x 的高度斜率**推一个近似水平法线，在着色器里算一次 `NdotL`：
   - 太阳方位取场景主光的水平朝向（`SpikeScene` 里 `sun yaw +25`，即光从右前方来 → 画面里**右坡受光、左坡背光**，具体以实际渲染为准，可先渲染一次确认方向再定符号）
   - 受光面与背光面相对亮度差 **15–25%**（不要超过 30%，否则山会从「远景」变成「近景道具」）
   - 明暗过渡不要是硬边：斜率用 2–3 段平滑（`smoothstep`），保证山体内部是**块面**而不是条纹
2. **雪线**：只在**最远层（layer 3）与中层（layer 2）**出现，且：
   - 只在受光面 + 山脊上方出现，背光面雪压暗到 `#B9C8D4` 一带
   - 边缘用 `snow.png` 噪声**破碎**（不要一条水平直线）
   - 近层（layer 1）**没有雪**（它是画面下缘的近山，有雪会读成「白色石头」）
3. **层间色温分离**：近层偏冷青（`#44546A` 一带）、中层中性灰蓝、远层偏紫灰；三层矩形均值差 **≥ 6**（当前三层几乎同色）
4. **山脚两条横向云雾带**：现有 `BuildMistbands()` 的雾带在主视角里读不出来（之前记过）。改成 2 条水平拉伸的雾带，压在山腰下缘、宽度覆盖整幅画面，用 `mist.png` + 加色混合；**不许**在山与天空交界处制造新的硬边（`edge.py` 会量）
5. **不要洗白**：远山**任何位置都不得比天空更亮**（天空 `(167,183,203)`），也不能变成纯白雪山剪影

## 验收（必须自己跑，把输出原样贴回回答）

在仓库根 `/Users/lance/workspace/mistbell`：

```bash
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath spike/project -executeMethod SpikeScene.Run \
  -logFile spike/logs/220_scene_mtn.log ; echo EXIT=$?
grep -E '\[SPIKE\] (ridge|frame church|frame pitch|mist)|error CS' spike/logs/220_scene_mtn.log

/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath spike/project -executeMethod SpikeBuild.Run \
  -logFile spike/logs/221_build_mtn.log ; echo EXIT=$?
grep -E 'result=|error CS' spike/logs/221_build_mtn.log

caffeinate -dimu -t 260 bash spike/tools/capture_runtime.sh

python3 spike/tools/region.py spike/project/Shots/art_runtime.png 60,470,420,640 1450,400,1820,560
python3 spike/tools/sharp.py  spike/project/Shots/art_runtime.png
python3 spike/tools/frame.py  spike/project/Shots/art_runtime.png
python3 spike/tools/edge.py   spike/project/Shots/art_runtime.png
```

必须同时满足：

1. 两个 Unity 命令 `EXIT=0`，日志无 `error CS`，`result=Succeeded errors=0`（macOS + Windows）
2. 山体矩形 `Lstd ≥ 8` **且 `p5/p50 ≤ 0.90`**（证明山体内部真的有 15–25% 的明暗，而不是被天空锯齿凑出来的 std）
3. 远山任何采样点不比天空亮：`frame.py` 的 `sky` 保持 `(167,183,203)` ±3；`clipped ≤ 0.05%`
4. `sharp art_runtime` 的 `top ≥ 3.0`（当前 0.00）
5. `edge.py` `mean jump ≤ 5.0`（当前 4.0，不许造新硬边）
6. `frame church heightPct = 44.7±0.5`、`centerX = 917.9±1`、`pitch = 11.8±0.2`——**几何与机位不许被带偏**
7. 三层山矩形均值互相差 ≥ 6（贴出三块数字）
8. 雪只在 layer 2/3、且必须可目视（正视图/主视角截图里能看到破碎雪线）

不达标就自己迭代参数再跑，**不要**改机位或几何去凑数字。

## 纪律

- 中文回答；把上面每条命令的**实际输出**贴回来，不要只写「已达标」
- 不许新增依赖，不许 `git commit`，不许改 docs 与本 brief
