只做一件事：**让远山重新有存在感**——现在是"雾中几乎看不见"，参考图里雪山占画面上 1/3 且细节清楚。只调远山层（雾量/亮度/雪线/云），不动教堂/地形/材质/相机。

参考：`art-ref/ref-gergeti-hd2d.jpg`。

## 一、具体做法

1. **雾量回调**：`MistHaze` 从 `0.20/0.40/0.58/0.74` 改为 **`0.12/0.26/0.42/0.58`**
2. **雪线提亮**：每层雪盖的亮部提 **+8%**，并把雪/岩边界的过渡从 1 px 加到 2 px（远看更"厚"）
3. **山体占位**：最前层（L1, 900 m）的 `top_viewport_y` 从当前值提到 **≤ 0.30**（即山顶进入画面上 1/3）
4. **空气透视分层**：L1→L4 的雾色递进保持，但 L4 不要低于 `mountain_luma 0.52`（现在压得太淡）
5. **云带**：`_CloudAmount` 0.70 → 0.62，云尺度 ×0.85（让山脊露出来，不被云糊住）
6. 山脚地雾 `_HazeBottom` 保持 0.25（已经能接上雪原）

## 二、必须打印

```
[MTN16] haze=0.12/0.26/0.42/0.58 snow_gain=<f> top_y=0.xx/0.xx/0.xx/0.xx mountain_luma=<f> seam_max_grad=<f> cloud=<f>
[MTN16_GATE] luma_ok=<bool> top_ok=<bool> seam_ok=<bool> frame_ok=<bool> pass=<bool>
```

## 三、硬门槛

- `mountain_luma ∈ [0.45, 0.55]`
- L1 的 `top_viewport_y ≤ 0.30`（山顶进入画面上 1/3）
- `seam_max_grad ≤ 0.035`
- `snow_gain ∈ [1.06, 1.12]`
- 整帧：`snow_ratio ≥ 0.55`、`white_clip ≤ 0.05`、`magenta_px = 0`
- 性能不退化：1080p `fps ≥ 200`、`drawcall ≤ 330`

## 四、必须自己跑

```bash
bash spike/tools/accept.sh --fast
```
外加 `-demo-cinema`（远景镜头最能看出山）、`-demo-reveal`（gate=pass）、`-bench`。

截图必出：`art_runtime.png`、`art_close.png`、`style_480_upscaled.png`。

⚠️ 一律用 `open -n -a` + caffeinate。
不加包、不 commit、不改教堂/地形/材质/相机/玩法/UI。
