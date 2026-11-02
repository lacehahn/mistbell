# 山体形态（队列 ⑬）— 从「毛绒棉花糖」改成「有崖壁、岩层、雪线的雪山」

## 必读（只读这三个，禁止通读全仓）
1. `spike/project/Assets/Editor/SpikeScene.cs` 的 `BuildMountains()` / `SaveMountainMesh()`（约 548–645 行）
2. `spike/project/Assets/Art/Shaders/RidgeFog.shader`
3. `spike/tools/macro.py`（本轮新增的验收尺子，`selftest` 5/5 PASS）

## 现状（我刚量的基线，`spike/project/Shots/art_runtime.png`）
```
python3 spike/tools/macro.py scan project/Shots/art_runtime.png 1300,140,1900,520 100,260,500,560 620,120,1180,420
  1300, 140,1900, 520  macroStd=23.8 dark= 4.3% bright= 4.5% strata=0.93  PLUSH
  100, 260, 500, 560  macroStd=15.1 dark= 3.5% bright= 0.6% strata=1.34  PLUSH
  620, 120,1180, 420  macroStd=16.4 dark= 3.5% bright= 0.8% strata=1.58  PLUSH
```
四块全 PLUSH。目视（我已看过 1:1 裁剪）：山是**光滑穹顶**，表面是一层**均匀 5 px 方块马赛克**，
雪只是一顶**软帽**盖在顶上，往下没有任何岩壁/岩层/沟壑。根因两条，都必须改：

- **几何**：`n=120, rows=14`，`h = -10 + (peak+10) * pow(1-|2t-1|, .85)` 是单一对称拱形，
  `peak` 只是 4 条正弦叠加 → 光滑穹顶；`t` 方向只有 14 行，做不出崖壁台阶。
- **着色**：`snow` 只由高度分数 `ridge.x` 决定（`_SnowHeight=.72`），岩面颜色与雪面颜色
  都乘了同一套 `lightFace` 平滑梯度；`pixelUV = positionWS.xy / 16.0` 一张 16 m 的贴图在
  190–700 m 距离上被压成均匀马赛克。**坡度完全不参与雪线**，所以陡壁也盖满雪。

## 要做的事（不改其它任何东西）

### A. 几何（`BuildMountains()`）
1. 分辨率：`n=240`（沿 x）、`rows=40`（沿 t）；顶点数 9600，可接受。
2. 山脊轮廓（沿 x）：把 4 条正弦换成**确定性的 ridged noise**（自己写 hash，不要用 `Random.Range`
   以免不可复现）：`peak = base + Σ_i amp_i * (1 - |2*frac(x*f_i + phase_i) - 1|)`，
   3–4 个八度、`f` 递减、`amp` 递减；结果要**尖顶 + V 形鞍部**，相邻峰高差 ≥ 25 m。
3. 山体剖面（沿 t）：改成**不对称 + 台阶化**：
   `base = pow(1 - |2t-1|, 0.55)`（坡更陡）；再 `h_ter = floor(h/step)*step + remap(frac)`
   与 `h` 按 0.55/0.45 混合，`step = 14 m` → 形成**崖壁段 + 平台段**交替（这是「崖壁」的来源）。
4. **沟壑/支脊**：按列加确定性的 `gully = rig(x*0.02~0.08)` 作用到 `z`（振幅 12–22 m）与
   `h`（振幅 4–8 m，沿 t 衰减）→ 正立面出现**竖向的沟与脊**，法线左右摆动，光照下读出纵纹。
5. 法线：保留 `Slerp(normal, smooth, .88)` 的平滑策略，但崖壁台阶处允许更硬（`.92`）。
6. 现有 `uv.y = 高度分数`、`_SnowHeight` 等接口保持不变（shader 还要用）。

### B. 着色（`RidgeFog.shader`）
1. **坡度参与雪线**（关键）：`slopeGate = smoothstep(0.30, 0.62, n.y)`，
   `snow = heightGate * slopeGate`。陡壁（`n.y` 小）露岩石，平台/山脊留雪 → 雪线自然成锯齿。
2. **雪线压边**：雪掩码的 `smoothstep` 宽度收到 ≤ 0.008（1–2 px 硬边），并在掩码下方
   （`snow` 刚从 0 抬起的那一带）把岩石压暗 ×0.78，读成「雪檐投影」。
3. **岩层（stratification）**：岩石反照率按世界高度做**3 档水平条带**
   （`y*0.9` 为相位，加沿 x 的低频扰动让条带不水平僵硬），档间明度差 ≥ 12%，
   条带交界保留 1 px 硬边（按屏幕像素量化，不要用平滑 lerp）。
4. **贴图尺度**：`pixelUV` 的 16 m 太大。雪 `positionWS.xy / 6.0`、岩 `/ 8.0`，
   并给岩贴图乘一个沿 `(y*0.35)` 的竖向拉伸，让纹理读成「顺坡的岩理」而不是方块马赛克。
5. 不得改动：`_Aerial` 三层次序、`_AirColor`、`Stencil` 逻辑、以及 `frame sky` 的取值
   （雾只在山体片元上，不许抬亮天空）。

### C. 不许碰
地形 `TerrainHaze`/雪面、教堂、植被、机位 `ShotRig`、曝光（sun/ambient/postExposure）、
任何 `tools/*.py` 尺子。

## 硬门槛（我会独立复跑，全部要过）
复跑链：`SpikeScene.Run` → `SpikeBuild.Run` → `caffeinate -dimu -t 260 bash spike/tools/capture_runtime.sh`

1. `python3 spike/tools/macro.py scan spike/project/Shots/art_runtime.png 1300,140,1900,520`
   → **PLUSH 必须变 ROCK**：`macroStd ≥ 26`、`dark ≥ 10%`、`bright ≥ 12%`、`strata ≥ 1.20`
2. 同命令 `100,260,500,560` → `macroStd ≥ 18`、`dark ≥ 6%`、`strata ≥ 1.20`（verdict 不强求 ROCK）
3. 同命令 `620,120,1180,420` → `macroStd ≥ 18`、`dark ≥ 6%`
4. 无回归：`frame.py` 的 `sky mean=(155,170,182)` 每通道 ±3、`ground_near mean=(203,205,200)` ±5、
   `clipped` ≤ 1.5%（基线 0.45%）；`measure_shot` 的 `snow ≥ 0.55`、`clip ≤ 0.03`；
   `[SPIKE] frame church heightPct=44.8`、`centerX≈922` 均 ±1
5. 双平台构建 `Succeeded / 0 errors`；场景日志打印一行 `[MOUNTAINS] cols=240 rows=40 terrace=14m ...`
6. 确定性：连跑两次 `SpikeScene.Run` 的 `[MOUNTAINS]` 行一致（同 seed）

## 目视判据（我自己看图定罪）
1:1 裁剪 `ffmpeg -vf "crop=680:540:1240:120"` 下必须读到：**尖而不圆的峰**、**至少两段近垂直的崖壁**、
**崖壁上的水平岩层**、**雪只挂在平台与山脊上（陡壁露岩）**；不能再出现「软帽 + 均匀马赛克」。
