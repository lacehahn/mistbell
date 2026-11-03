只做一件事：修远山的**像素台阶/块状边**——1024×512 贴图在近处被放大后，山脊出现明显的方块锯齿与"白色糖霜块"。不改教堂/室内/玩法/相机。

必读：`Shots/art_runtime.png`（山脊上的块状锯齿、雪帽像糖霜块）、`MountainBackdrop.cs`。

【要改】
1) **贴图提分辨率**：每层从 1024×512 → **2048×1024**（四层共 4 张，注意在内存可接受范围内）。
2) **远层改双线性**：L3 / L4 的材质 `filterMode = Bilinear`（近处的 L1 / L2 保持 Point，保留像素感），并在导入设置里对 L3/L4 关闭 mipmap 之外的锐化。
3) **雪帽下缘抗台阶**：雪与岩的交界处，允许 1 px 半透明过渡（alpha 0.5 的中间色 #D5E2EE），避免"一刀切"的方块边。
4) **山脊线平滑**：脊线高度用 **线性插值到 0.5 px** 精度再取整绘制（等价于 2 倍超采样后降采样），消除阶梯。
5) 山脚硬切线：底部 10% 高度加一条**向上渐隐的地雾**（不透明度 0→0.35），把山与雪原接起来。

【自检打印】
"[MTN13] tex=2048x1024 filter=L1:Point/L2:Point/L3:Bilinear/L4:Bilinear snow_edge_alpha=0.5 supersample=2x foot_haze_pct=10"

【必须自己测量（用截图）】
- `ridge_stair_px`：在山脊线附近取 200 个采样点，统计"相邻 3 px 亮度跳变 > 0.25"的比例 → 目标 **≤ 0.05**
- `mountain_luma` 保持 **0.42–0.60**、`seam_max_grad ≤ 0.035`
硬门槛：`ridge_stair_px ≤ 0.05`、`magenta_px = 0`。

【必须自己跑】
`capture_runtime.sh`（三张）、`-demo-cinema`、`-demo-reveal`（gate=pass 保持）。
回归：`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`-demo-enter-church`、`measure_shot.py`。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改教堂/室内/玩法/相机/measure_shot.py。
