只做一件事：给远山加**山脊转折**，消掉"整片平滑渐变"的平感。不改教堂/室内/玩法/相机。

必读：`Shots/art_runtime.png`、`MountainBackdrop.cs`。

【问题】
四层山加完细节后，山体仍是"一大片从亮到暗的平滑渐变"，缺真实山地的**脊线转折**（山脊相交形成的沟谷与支脊）。

【要改】
1) 每层山体叠 **2–3 条支脊**：从主脊线向斜下延伸的窄亮带（宽 8–18 px，亮度比周围 +0.05～+0.08），末端渐隐；支脊角度在 25°–55° 之间随机，不与主脊平行。
2) 每两条支脊之间压**一条沟谷**（宽 10–22 px，亮度 −0.06），沟底可有一条 1–2 px 的深色线。
3) **雪的表现随之分叉**：支脊上积雪（沿脊线 3–8 px 宽、长 20–60 px），沟谷里露岩（无雪）。
4) L1 的支脊/沟谷最明显（对比 ±0.08），L2 减半，L3/L4 只保留轻微沟谷（±0.03）。

【自检打印】
"[MTN12] spurs=<l1>/<l2>/<l3> spur_angle=25..55 spur_width_px=8..18 gully_width_px=10..22 spur_contrast=<f> gully_contrast=<f>"
硬门槛：每层 `spurs ≥ 8`、`spur_contrast ≥ 0.05`、`gully_contrast ≥ 0.06`，且 `mountain_luma` 仍在 **0.42–0.60**、`seam_max_grad ≤ 0.035`（不得退化）。

【必须自己跑】
`capture_runtime.sh`（三张）、`-demo-cinema`、`-demo-reveal`（gate=pass 保持）、`-demo-enter-church`。
回归：`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`measure_shot.py`。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改教堂/室内/玩法/相机/measure_shot.py。
