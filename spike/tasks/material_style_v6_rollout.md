只做一件事：把已确认的材质风格**铺开到剩余四类**——屋顶石板、裸岩、枯草/枯枝、室内石材。教堂石材（已定稿）、挡土墙（用户明确保持现状）、雪、木门**都不许改**。

已定稿的口径（照抄，不要重新发明）：单色相灰阶、4 档硬边明暗、`chroma_ab_max ≤ 6`、色数 ≤ 12、`hf` 落在 0.010–0.018、`texel_per_m` 按表、`seam_grad ≤ 0.02`。

## 一、四个材质

| 材质 | 色相 | 4 档（sRGB，线性化后 L* 见括号） | texel/m | 结构要求 |
|---|---|---|---|---|
| 屋顶石板 `Roof_Slate` | 冷灰 | `#3A3F45(23) / #474D54(30) / #555C64(37) / #646C75(44)` | 26 | 叠瓦：每片 6–8 px 宽、3–4 px 高，**下缘 1 px 亮线**（+12%），上缘压 1 px 深线；沿屋脊方向的**错缝**；不许出现照片噪点 |
| 裸岩 `Rock` | 中性灰（比教堂略冷） | `#414449(26) / #4E5156(32) / #5C5F64(38) / #6B6E73(44)` | 24 | 4 档**硬边**块面，块 12–30 px；块与块之间不做灰缝（岩石没有砌缝），靠明度台阶区分；少量 1 px 深色裂纹 |
| 枯草/枯枝 `Grass` | 暖灰褐、低饱和 | `#3A342C / #464036 / #545044 / #635E52` | 1 px = 3 cm | 极稀疏细线（前面已定：前景 60 m 内 12–20 丛），本次只换**贴图明度阶梯**，不改密度与形状 |
| 室内石材 `Stone_Interior` | 同教堂灰相 | 教堂四档各降 18%（L* 约 46→38） | 28 | 复用教堂砌块逻辑（1 px 灰缝 + 倒角 + 错缝），只是更暗；**不许把室内打成浅色厅** |

## 二、绝对不许改（SHA-256 必须与当前一致）

`Stone_Albedo.png`（教堂，已定稿 L*=46.0）、`Stone_Sub_Albedo.png`（挡土墙）、`Snow_Albedo.png`、`Wood_Albedo.png`

若实现上需要给室内石材单独一张贴图，请**新建** `Stone_Interior_Albedo.png`，不要覆盖教堂那张。

## 三、必须打印

```
[STYLE6] <material> palette=<n> luma_steps=<n> mean_L=<f> chroma_ab_max=<f> texel_per_m=<f> hf=<f> seam_grad=<f>
[STYLE6_HF] sigma=1.0 Stone=<f> Sub=<f> Wood=<f> Snow=<f> Roof=<f> Rock=<f> Grass=<f> Interior=<f> spread_all=<f>
[STYLE6_UNCHANGED] Stone_sha_same=<bool> Sub_sha_same=<bool> Snow_sha_same=<bool> Wood_sha_same=<bool>
[STYLE6_GATE] palette_ok=<bool> mono_ok=<bool> hf_ok=<bool> steps_ok=<bool> unchanged_ok=<bool> pass=<bool>
```

## 四、硬门槛

- 四新材质：`palette ≤ 12`、`luma_steps ∈ [4,5]`、`chroma_ab_max ≤ 6`（Grass 允许 ≤ 10，因为它是暖褐）、`hf ∈ [0.010, 0.018]`、`seam_grad ≤ 0.02`
- **七材质（含已定稿四个）`spread_all ≤ 0.20`**
- `Stone_Interior mean_L ≤ Stone mean_L − 8`（室内必须明显更暗）
- `unchanged_ok = True`（四张已定稿贴图 SHA 不变）
- 整帧：`snow_ratio ≥ 0.55`、`white_clip_ratio ≤ 0.05`、`magenta_px = 0`、`mountain_luma 0.42–0.60`
- 性能：1920×1080 `fps ≥ 200`、`drawcall ≤ 330`

## 五、必须自己跑

```bash
bash spike/tools/accept.sh --fast        # 建场景→出包→11 项演示→截图→量测
```
再单独跑 `-demo-enter-church`（室内三图，检查室内石材没有变浅）。
性能单独用 1920×1080 跑 `-bench`。

**截图要求**：重出 `art_close.png`、`art_runtime.png`、`style_480_upscaled.png`（480×270 LANCZOS 下采样 → NEAREST ×4）。上一轮出现过 480 图是旧图的情况，这次必须基于本轮渲染重新生成。

⚠️ 一律用脚本里的 `open -n -a` + caffeinate；直接执行二进制会静默卡死。

不加包、不 commit、不改玩法/相机/关卡/UI。
