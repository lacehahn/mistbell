只做一件事：**前景雪原太空**（`Shots/art_runtime.png` 左下 1/4 几乎是一整片纯白，只有几块雪石；从出生点到台地的前 60 m 缺少近景层次）。不改教堂/玩法/相机/室内/远山/天空。

必读：`Shots/art_runtime.png`、`Shots/art_close.png`、`SpikeScene.cs` 的雪原地形与散石生成（`ContactMound` / 巨石那段）、`snowfield_detail.md` 已有的中尺度雪丘规则。

【要加（都只做"近景 60 m 内"，且保持道路两侧 3 m 净空）】
1) **风蚀雪脊 sastrugi**：沿主导风向（与路径夹角 20–35°）排布的细长雪脊，高 0.10–0.25 m、长 1.5–6 m、间距 0.8–2.5 m，覆盖率 **18–30%** 的前景雪面；只改高度场，不加新 drawcall 对象。
2) **雪下裸岩簇**：3–8 块/100 m²，床岩露出 0.15–0.6 m，顶面留雪帽；用合并网格（一个 MeshRenderer 装多块），**整个簇算 1 个 drawcall**。
3) **枯草/灌木残枝**：极稀疏（前景 60 m 内 12–20 丛），深色 `#3A342C`，高 0.15–0.35 m，用 2 个合并网格（左右两侧各一）。
4) **路标石堆 cairn**：沿路径每 45–60 m 一个，3–5 块叠石，共 3–4 个，合并成 1 个 drawcall。
5) 以上都不许放在 `|x-CenterX(z)| < 3 m` 的路径带内，也不许压在台阶/台地上。

【自检打印】
"[FGD] sastrugi_cov=<f> rock_clusters=<n> grass_patches=<n> cairns=<n> new_drawcalls=<n> path_clear_min=<f> fg_variance=<f> fps=<f>"
硬门槛：
- `sastrugi_cov` 在 0.18–0.30
- `new_drawcalls ≤ 6`
- `path_clear_min ≥ 3.0`
- `fg_variance`（左下 1/4 区域亮度方差）**≥ 0.0015**（当前接近 0）
- 1920×1080 `fps ≥ 200`、drawcall 总数 `≤ 330`
- `snow_ratio ≥ 0.55`、`white_clip_ratio ≤ 0.05`、`magenta_px = 0`

【必须自己跑】
`capture_runtime.sh`（3 张）、`-bench`（1920×1080 满分辨率，注意：`run_demos.sh` 传的是 1600×900，bench 需要单独用 1920×1080 参数跑）、`-demo-cinema`、`-demo-reveal`（gate=pass）。
回归：`-demo-walk 6`、`-demo-walk-stairs`、`-demo-walk-to-tower`、`-demo-enter-church`、`-demo-journal`、`-demo-trail 10`、`measure_shot.py`。
⚠️ 演示一律用 `bash spike/tools/run_demos.sh "<参数>"`（内含 caffeinate + `open -n -a`）。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改教堂/玩法/相机/室内/远山/天空/measure_shot.py。
