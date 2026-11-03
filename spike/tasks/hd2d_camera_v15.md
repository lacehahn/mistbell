只做一件事：**相机与景深改成"低机位 + 移轴微缩"**。玩法输入/碰撞/交互不变，只改相机高度、FOV、景深参数与构图。

参考：`art-ref/ref-gergeti-hd2d.jpg`（低机位仰视、前景灌木强烈虚化、中景清晰、教堂显高大）。

## 一、具体做法

1. **机位压低**：跟随相机偏移从 `(0, 4.4, -10.5)` 改为 **`(0, 1.55, -7.2)`**（接近角色肩高），看向 `+1.6 m`（原 3.2 m）
2. **FOV 放宽**：主视角 35° → **48–52°**（室内保持 50°）；`FollowFieldOfView` 反算仍要保证教堂顶不被切（`top 0.06–0.20`）
3. **景深改成真移轴**：
   - `focusDistance` = 到角色的实际距离（动态跟随，不要写死 26 m）
   - `aperture 1.4–1.8`（现在 2.8 太保守）
   - `focalLength 50`、`bladeCount 6` 不变
   - 远景（>120 m）与前景（<6 m）都应有明显虚化
4. **仰视构图**：相机看向点抬高到角色胸口以上 0.3 m，形成轻微仰角；教堂在画面中的高度占比应从当前约 0.30 提到 **0.38–0.48**
5. 章节演示（`-demo-cinema`/`-demo-reveal`）的机位不受影响，仍按各自脚本

## 二、必须打印

```
[CAM2] offset=<x,y,z> fov=<f> look_h=<f> focus=<f> aperture=<f> church_height_pct=<f>
[CAM2_DOF] fg_hf=<f> mid_hf=<f> bg_hf=<f> fg_ratio=<f> bg_ratio=<f>
[CAM2_GATE] fov_ok=<bool> height_ok=<bool> dof_ok=<bool> frame_ok=<bool> pass=<bool>
```

`fg_hf`/`mid_hf`/`bg_hf`：画面下 15%（前景）、中间 15%（教堂）、上 20%（远景）三块的高频能量（`hf_energy` 口径）。

## 三、硬门槛

- `fov ∈ [48, 52]`、相机离地高度 `∈ [1.4, 1.8] m`
- `church_height_pct ∈ [0.38, 0.48]`
- **`fg_ratio = fg_hf / mid_hf ≤ 0.55`**（前景要明显更虚）、**`bg_ratio ≤ 0.75`**
- 教堂顶不被切：`[CLOSEUP] top ∈ [0.06, 0.20]`
- 玩法不退化：`-demo-walk 6` 仍 `distance ≥ 15`、`-demo-walk-to-tower` 仍 `reached=true`、`-demo-reveal gate=pass`
- 整帧：`snow_ratio ≥ 0.55`、`white_clip ≤ 0.05`、`magenta_px = 0`

## 四、必须自己跑

```bash
bash spike/tools/accept.sh --fast
```
外加 `-demo-enter-church`（室内 FOV 50 与抬头 F 键仍成立）、`-bench`。

截图必出：`art_close.png`、`art_runtime.png`、`style_480_upscaled.png`，并额外出一张 `cam_low.png`（新机位下的教堂全景）。

⚠️ 一律用 `open -n -a` + caffeinate。
不加包、不 commit、不改玩法/输入/碰撞/材质/UI。
