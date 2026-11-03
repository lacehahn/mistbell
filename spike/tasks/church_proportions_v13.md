只做一件事：**继续压矮外墙、再加高尖顶**——v12 只压了 0.5 m（12.6→12.1），因为被"室内净高 12 m 不得降低"卡住。这一轮**允许降低室内净高到 10.2 m**（教堂室内 10 m 仍够用，玩家抬头 F 键仍能看到鼓座与穹顶），从而真正把外墙压下来。材质/玩法/相机不动。

参考：`art-ref/ref-gergeti-hd2d.jpg`。用户要求：外墙更矮、尖顶更高、钟楼更胖（钟楼 v12 已加胖 20%，保持）。

## 一、目标

1. **外墙**：`body_h` 从 12.1 m 降到 **10.4–10.8 m**（占原 12.6 m 的 0.83–0.86）
2. **室内净高**：从 12 m 允许降到 **10.2–10.6 m**（与外墙同步），但**必须保证**：
   - 玩家站姿 + F 抬头仍能看到鼓座 12 窗与锥顶（`[LOOKUP] ceiling_pct ≥ 0.35`、`dome_visible=true` 保持）
   - 鼓座（drum）与锥顶跟着下移，不许悬空或穿墙
   - 吊灯、圣像屏、长椅、地面碰撞全部重新贴合，不许悬空
3. **尖顶**：在 v12 基础上再加高 **6–10%**，最终 `body_roof_ratio ∈ [1.15, 1.32]`
4. **钟楼**：保持 v12 的直径（+20%）与净距 3.01 m，高度等比跟随（允许 ±5%）
5. **进深**：当前 23.04 m 略超上限，收到 **≤ 22.6 m**（后殿外挑收 0.4–0.5 m 即可，不动室内布局）

## 二、必须打印

```
[PROP2] body_h=<f> interior_clear=<f> roof_h=<f> body_roof_ratio=<f> tower_d=<f> tower_h=<f> tower_gap=<f> depth=<f> tris=<n>
[PROP2_LOOKUP] ceiling_pct=<f> dome_visible=<bool>
[PROP2_GATE] body_ok=<bool> interior_ok=<bool> roof_ok=<bool> ratio_ok=<bool> tower_ok=<bool> gap_ok=<bool> depth_ok=<bool> pass=<bool>
```

## 三、硬门槛

- `body_h ∈ [10.4, 10.8]`、`interior_clear ∈ [10.2, 10.6]`
- `body_roof_ratio ∈ [1.15, 1.32]`
- `tower_d` 保持 v12 值（±2%）、`tower_gap ≥ 3.0`
- `depth ≤ 22.6`、占地 `W ∈ [27, 29]`
- `ceiling_pct ≥ 0.35`、`dome_visible=true`
- 三角面 ≤ 原值 ×1.05
- 整帧：`snow_ratio ≥ 0.55`、`white_clip ≤ 0.05`、`magenta_px = 0`、`mountain_luma 0.42–0.60`
- 性能：1920×1080 `fps ≥ 200`、`drawcall ≤ 330`
- 室内不被压扁：`interior_luma` 仍在既有区间、`[INT_PASS]` 全 true

## 四、必须自己跑

```bash
bash spike/tools/accept.sh --fast
```
外加 `-demo-enter-church`（重点看室内三图与抬头）、`-demo-walk-to-tower`、`-bench` 1920×1080。
截图必出：`art_close.png`、`art_runtime.png`、`interior_lookup.png`、`style_480_upscaled.png`。

⚠️ 一律用 `open -n -a` + caffeinate。
不加包、不 commit、不改材质配色/玩法/相机/UI。
