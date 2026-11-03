只做一件事：**教堂石材加深、屋顶石板提亮**（屋顶仍必须比墙面深）。其余材质一律不动。

用户原话：「教堂本身颜色太浅了，加深，屋顶太深了，变浅但是依然比墙面深」。

## 当前实测（我复量的基准，用线性化 Lab）

- `Stone`（教堂）`L* = 46.0`
- `Roof_Slate`（屋顶）`L* = 27.7`
- `Stone_Sub`（挡土墙）`L* = 13.8`（**不动**）
- `Stone_Interior` `L* = 37.8`（要跟着教堂下调）
- 其余：`Snow 93.8`、`Wood 23.7`、`Rock 37.2`、`Grass 31.0`（**都不动**）

## 要改的

1) **教堂石材 `Stone` 加深**：`mean_L` 目标 **`[38, 41]`**（从 46.0 降下来，约 −6）
   - 保持单色相（`|a*|,|b*| ≤ 3`）、4 档阶梯、色数 ≤ 10、1 px 灰缝、倒角 +12%
   - 明度关系：最亮档像素占比 ≤ 12% 不变
2) **屋顶石板 `Roof_Slate` 提亮**：`mean_L` 目标 **`[31, 34]`**（从 27.7 升上来）
   - **必须同时满足 `roof_L ≤ stone_L − 5`**（屋顶可以变浅，但读起来仍要比墙暗一档）
   - 保持冷灰单色相、叠瓦结构（6–8 px 片、下缘 1 px 亮线、错缝）、色数 ≤ 12
3) **室内石材 `Stone_Interior` 跟随下调**：`mean_L ≤ Stone mean_L − 8`（教堂降了，室内也要降，目标约 30–31），室内依旧是昏暗石堂，不许变亮

## 绝对不许改（SHA-256 必须与当前一致）

`Stone_Sub_Albedo.png`（挡土墙）、`Snow_Albedo.png`、`Wood_Albedo.png`、`Rock_Albedo.png`、`Grass_Albedo.png`

## 必须打印

```
[STYLE7] Stone mean_L=<f> Roof mean_L=<f> Interior mean_L=<f> roof_minus_stone=<f>
[STYLE7_CHROMA] Stone ab_max=<f> Roof ab_max=<f>
[STYLE7_UNCHANGED] Sub_sha_same=<bool> Snow_sha_same=<bool> Wood_sha_same=<bool> Rock_sha_same=<bool> Grass_sha_same=<bool>
[STYLE7_HF] sigma=1.0 <八材质逐个> spread_all=<f>
[STYLE7_GATE] stone_ok=<bool> roof_ok=<bool> roof_darker_ok=<bool> interior_ok=<bool> unchanged_ok=<bool> hf_ok=<bool> pass=<bool>
```

## 硬门槛

- `Stone mean_L ∈ [38, 41]`、`Roof mean_L ∈ [31, 34]`、**`Roof mean_L ≤ Stone mean_L − 5`**
- `Stone_Interior mean_L ≤ Stone mean_L − 8` 且 `≤ 32`
- 三者 `chroma_ab_max ≤ 6`
- 五张锁定贴图 `sha_same = True`
- `spread_all ≤ 0.22`（本轮只调明度，不要为了压 spread 去动其它材质的高频细节）
- 整帧：`snow_ratio ≥ 0.55`、`white_clip_ratio ≤ 0.05`、`magenta_px = 0`、`mountain_luma 0.42–0.60`
- 性能：1920×1080 `fps ≥ 200`、`drawcall ≤ 330`

## 必须自己跑

```bash
bash spike/tools/accept.sh --fast
```
外加 `-demo-enter-church` 室内三图（确认室内没变亮）、1920×1080 `-bench`。

**截图**：重出 `art_close.png`、`art_runtime.png`、`style_480_upscaled.png`（480×270 LANCZOS → NEAREST ×4），必须基于本轮渲染。

⚠️ 一律用脚本里的 `open -n -a` + caffeinate。

不加包、不 commit、不改玩法/相机/关卡/UI。
