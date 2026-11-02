# 山体第 2 轮：几何保持，只修「黑色岩带 / 灰噪浆糊墙 / 硬剪影雪线」

第 1 轮几何过关了（尖峰、崖壁、沟壑都在），但**着色把画面做坏了**。本轮只改材质，不许动几何数字
（`cols/rows/terrace/crestRow/gully/seed` 全部保持 `[MOUNTAINS] ... meshHash=7448A17B`）。

## 必读
1. `spike/project/Assets/Art/Shaders/RidgeFog.shader`（改它）
2. `spike/project/Assets/Editor/SpikeScene.cs` 的 `BuildMountains()` 中 `mat.SetColor/SetFloat` 那段（只调材质参数）
3. 我量好的基线图：`spike/project/Shots/art_runtime.png`（现成，直接量）

## 我量到的「坏」在哪（数字 + 已看过的 1:1 裁剪）
```
region 1400,470,1800,560  mean=(86,95,101) p5=72.9 p50=95.9   ← 岩带偏暗，但真正的问题是下一行
macro  1300,140,1900,520  macroStd=64.4 dark=34.6% bright=13.7% strata=5.18
```
目视（crop=680:540:1240:120）：雪线下方有一条**近纯黑的剪影带**，再往下是一大面**均匀灰噪「浆糊墙」**，
雪片像**贴纸**盖在黑底上。根因在你写的三行里，逐个对应：

1. `rock *= (stratum < 1 ? .18 : (stratum < 2 ? .52 : 1.0));` → 最低层岩床是 **18%**，这就是黑带。
2. `rock *= lerp(1,.38,farRidge);` → 最远一层岩面被打到 **38%**，方向也反了：远层应该**更浅、更雾**。
3. `rockUV` 用 `/8.0` 且 `frac(y*0.35)` → 在 400 m 处每 texel < 1 px，退化成均匀灰色静态噪点（浆糊）。
4. 雪掩码 `snowWidth=.004` + 岩石过暗 → 交界成了**硬剪影**，不是雪线。

## 具体改法（数值按此实现，别自由发挥）
1. **岩层明暗差收到 ±15%**：三层系数改 `0.86 / 0.94 / 1.06`（多层叠加仍要有台阶感，
   但任何一层都不许低于相邻层的 80%）。
2. **删掉** `rock *= lerp(1,.38,farRidge)`。远层只靠已有的 `_Aerial`→`_AirColor` 变浅。
3. **岩石基础明度抬起来**：`rockFace = .38 + .42*lightFace`（原来 .2+.4），并让 `_ColorBottom`
   是「受光岩」颜色；要求最终受光岩面 luma 115–150、背光岩面 72–100、**任何岩面 luma ≥ 55**
   （只有 1–2 px 的雪檐投影允许到 45）。
4. **岩石纹理尺度**：`rockUV = float2(x/13.0, y/5.0 + 0.35*noise)`，纹理细节混合量 `0.20 → 0.10`，
   目标是 3–5 px 的块、且**沿坡（竖向）拉长**，读成岩理而不是苔藓/雪花噪点。
5. **雪线过渡**：雪掩码下沿做 3–4 px 宽的一条「雪檐投影」，压到相邻岩面的 **0.86**（不是 0.78），
   即 `rock *= lerp(1,.86,underEdge)`；`underEdge` 的宽度按 fwidth 换算成像素，不按世界 y。
6. **雪面亮度**：`_SnowColor*1.27` 会让雪顶接近纯白并过曝；改回 `_SnowColor`（不乘），
   靠 `snowLight` 控制在 luma 195–225，`white_clip ≤ 3%`。
7. **远层也要有岩石结构**：第 3 层（`_Aerial=.78`）的雪线目前几乎是整片白/整片无结构。
   让 `slopeGate` 与 `stratum` 在远层同样生效（只被 `_Aerial` 淡化），不要整层平涂。

## 硬门槛（我复跑；`SpikeScene.Run`→`SpikeBuild.Run`→`caffeinate -dimu -t 260 bash spike/tools/capture_runtime.sh`）
1. `macro.py 1300,140,1900,520` → `macroStd ≥ 35`、`strata ≥ 1.60`、**`dark% 12–26`**（不许 >26）
2. `region.py 1400,470,1800,560` → `mean luma 105–140`、`p5 ≥ 62`
3. `macro.py 100,260,500,560` → `macroStd ≥ 35`、`dark% 10–30`
4. `macro.py 620,120,1180,420`（远层）→ `macroStd ≥ 24`、`dark% ≥ 8`
5. `region.py` 远层矩形 p50 **比 R1 p50 高 ≥ 12 luma**（空气透视方向不能反）
6. `surface.py scan --rect 1400,380,1800,560` → `PATTERN=0`、`aniso ≤ 2.0`、`grain ≤ 25`
7. 无回归：`frame.py` `sky (155,170,182)` 每通道 ±3、`ground_near (203,205,200)` ±5、`clipped ≤ 1.5%`；
   `measure_shot.py` `snow_ratio 0.55–0.65`、`white_clip_ratio ≤ 0.03`；
   `[SPIKE] frame church heightPct=44.8 / centerX=922` ±1；双平台 `Succeeded / 0 errors`
8. `[MOUNTAINS]` 行必须与第 1 轮**完全一致**（只许材质参数变），`meshHash` 不变即几何没动

## 目视判据
不许再有**近纯黑带**与**均匀灰浆糊**；雪线要读成「崖壁上挂雪 + 岩壁上水平层理 + 雪檐下一条窄影」，
且峰顶雪不要过曝成白纸。
