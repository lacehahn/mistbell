只做一件事：**光照重做**——去掉 PBR 高光、补有效的环境光、背光面不再压黑、主光改成暖侧逆光。材质贴图与几何不动。

参考：`art-ref/ref-gergeti-hd2d.jpg`（HD-2D 插画，暖墙冷影、几乎无高光）。

## 一、必须解决的三个现象（改完要能对着截图看出差别）

1. 台地墙背光面、屋檐下、室内现在是近黑（`L* 0–5`）→ 目标 **`L* ≥ 18`**
2. 石墙平面上有随镜头移动的平滑高光（塑料感）→ 石墙/屋顶/台地墙的 **高光项归零**
3. 墙面受光面与背光面的**色温差**几乎没有（都是灰褐）→ 受光偏暖 `b* +12~+18`、背光偏冷 `b* −6~0`

## 二、具体做法

1. **环境光**：`RenderSettings.ambientMode=Trilight` 保留，但把 `ambientGroundColor` 抬到 `#6E7385`、`ambientEquatorColor #7C8CA3`；并在 `TerrainHaze.shader` 的 irradiance 里确保 `SampleSH(n)` 对**朝下面法线**也有下限（`max(sh, 0.10)`）
2. **主光**：方向光改暖色 `#FFE3BE`、强度 1.15、仰角 30°、方位从左后方（逆光偏侧）；阴影色偏冷（通过环境光自然达成）
3. **去高光**：石墙/屋顶/台地墙材质 `_Specular=0`、`_Smoothness=0`；若走 Lit，改用 `_SpecularHighlights` 关闭
4. **补光**：室内与屋檐下各加一盏极弱半球补光（不可见光，`intensity 0.15`、`range 12`）
5. 雪面反射：`_SnowAlbedo` 不动，但雪面背光处允许偏蓝（`b* −8~−2`）

## 三、必须打印（都在**渲染后画面**上量，不用贴图）

```
[LIGHT2] shadow_face_L=<f> eave_under_L=<f> interior_floor_L=<f>
[LIGHT2_SPLIT] lit_b=<f> shadow_b=<f> specular_delta=<f>
[LIGHT2_GATE] shadow_ok=<bool> specular_ok=<bool> split_ok=<bool> frame_ok=<bool> pass=<bool>
```

## 四、硬门槛

- `shadow_face_L ≥ 18`、`eave_under_L ≥ 18`、`interior_floor_L ≥ 8`
- `specular_delta ≤ 0.04`（同一平面沿一条水平线扫描，最大亮度跳变；高光会拉出 >0.1）
- `lit_b − shadow_b ≥ 10`（暖亮 / 冷暗的分离）
- 整帧不变差：`snow_ratio ≥ 0.55`、`white_clip ≤ 0.05`、`magenta_px = 0`、`mountain_luma 0.42–0.60`
- 性能：1080p `fps ≥ 200`、`drawcall ≤ 330`

## 五、必须自己跑

```bash
bash spike/tools/accept.sh --fast
```
外加 `-demo-enter-church`（室内三图：室内不能因为提亮变"白墙厅"，`interior_luma` 仍需在昏暗区间）、`-bench`。

截图必出：`art_close.png`、`art_runtime.png`、`interior_inside.png`、`style_480_upscaled.png`。

⚠️ 一律用 `open -n -a` + caffeinate 启动。
不加包、不 commit、不改材质贴图/几何/玩法/相机/UI。
