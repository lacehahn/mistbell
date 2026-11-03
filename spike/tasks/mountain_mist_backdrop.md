只做一件事：**把远山压成"雾中远脉"**——现在四层山占画面近 40%，亮白带硬边，读起来像层叠剪纸。目标：让山退成浅浅的轮廓，教堂重新成为画面主体。不改教堂/玩法/相机/地形/天空。

必读：`Shots/art_runtime.png`、`MountainBackdrop.cs` 里每层材质设置的 `_Haze / _HazeColor / _HazeBottom`（`PaintedMountain.shader`）。

【要改】
1) **雾量整体上调**：L1/L2/L3/L4 的 `_Haze` 从 `0.06/0.26/0.48/0.72` 提到 **`0.34/0.54/0.70/0.84`**。
2) **雾色取天空**：`_HazeColor` 不要写死 `#C9D7E4`，改成与天空地平线同色（从 sky gradient 的地平线颜色取，或按当前雾色 `#C9D7E4` 与天空色的实际采样值）。
3) **降低明暗对比**：同层最亮/最暗面 Luma 差从现在压到 **≤ 0.12**（雾里不该有强明暗）。
4) **山脚地雾加强**：`_HazeBottom` 0.25 → **0.45**。
5) 山脊轮廓保留（有轮廓才有纵深），但整体亮度抬到 **0.58–0.66**，让山比天空略暗一档而不是明显更暗。

【自检打印】
"[MIST_MTN] haze=0.34/0.54/0.70/0.84 haze_color=#<hex> face_gap=<f> mountain_luma=<f> ridge_contrast=<f> foot_haze=0.45"
硬门槛：`face_gap ≤ 0.12`、`mountain_luma 0.58–0.66`、`ridge_contrast ≤ 0.12`、`seam_max_grad ≤ 0.035`、`magenta_px=0`、`snow_ratio ≥ 0.55`。

【必须自己跑】
`capture_runtime.sh`（3 张）、`-demo-cinema`、`-demo-reveal`（gate=pass）。
回归：`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`-demo-enter-church`、`-demo-journal`、`-demo-trail 10`、`measure_shot.py`。
⚠️ 演示一律用 `bash spike/tools/run_demos.sh "<参数>"`。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改教堂/玩法/相机/地形/天空/measure_shot.py。
