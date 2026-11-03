只做一件事：**屋顶石板再浅一点，但仍要比墙面深**。其他全部冻结。

用户原话：「这个对了，屋顶稍微浅点但是比墙壁深就可以了」。

## 当前实测（线性化 Lab）

- `Stone`（教堂）`L* = 39.76`、`b* = +16.3` —— **冻结，不许动**
- `Roof_Slate`（屋顶）`L* = 32.57`（比教堂深 7.19）—— **本轮只改它**
- 其余：`Stone_Sub 13.8`、`Stone_Interior 30.3`、`Snow 93.8`、`Wood 23.7`、`Rock 37.2`、`Grass 31.0` —— 全部冻结

## 要改的

`Roof_Slate mean_L` 目标 **`[34.5, 36.5]`**（从 32.57 提上来一点，视觉上"浅一档"），
并**必须满足 `Roof mean_L ≤ Stone mean_L − 3.5`**（即 ≤ 36.26；仍明确比墙深，但差距别拉到 7 那么远）。

保持：
- 冷灰单色相（`|a*|,|b*| ≤ 6`，现在 b*≈−4.3）
- 叠瓦结构：每片 6–8 px 宽、3–4 px 高、下缘 1 px 亮线、沿屋脊错缝
- 色数 ≤ 12、`seam_grad ≤ 0.02`、`hf ∈ [0.010, 0.018]`

## 冻结清单（SHA-256 必须与当前一致）

`Stone_Albedo.png`、`Stone_Sub_Albedo.png`、`Stone_Interior_Albedo.png`、`Snow_Albedo.png`、`Wood_Albedo.png`、`Rock_Albedo.png`、`Grass_Albedo.png`

## 必须打印

```
[STYLE8] Roof mean_L=<f> Roof ab_max=<f> stone_minus_roof=<f>
[STYLE8_UNCHANGED] Stone_sha_same=<bool> Sub_sha_same=<bool> Interior_sha_same=<bool> Snow_sha_same=<bool> Wood_sha_same=<bool> Rock_sha_same=<bool> Grass_sha_same=<bool>
[STYLE8_HF] sigma=1.0 Roof=<f> spread_all=<f>
[STYLE8_GATE] roof_ok=<bool> roof_darker_ok=<bool> unchanged_ok=<bool> pass=<bool>
```

## 硬门槛

- `Roof mean_L ∈ [34.5, 36.5]` 且 `Roof mean_L ≤ Stone mean_L − 3.5`
- `Roof ab_max ≤ 6`
- 七张冻结贴图 `sha_same = True`
- `spread_all ≤ 0.22`（本轮只动屋顶明度，不要改别的高频细节）
- 整帧：`snow_ratio ≥ 0.55`、`white_clip_ratio ≤ 0.05`、`magenta_px = 0`、`mountain_luma 0.42–0.60`

## 必须自己跑

```bash
bash spike/tools/accept.sh --fast
```
外加 1920×1080 `-bench`（`fps ≥ 200`、`drawcall ≤ 330`）。
**截图**：重出 `art_close.png`、`art_runtime.png`、`style_480_upscaled.png`（480×270 LANCZOS → NEAREST ×4），必须基于本轮渲染。

⚠️ 一律用脚本里的 `open -n -a` + caffeinate。

不加包、不 commit、不改玩法/相机/关卡/UI。
