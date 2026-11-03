只做一件事：**调整教堂比例**——外墙压低、尖顶加高、钟楼加胖。材质保持 v11（暖土黄石墙 + 冷灰屋顶 + 非平铺图集），不要动材质配色。

参考：`art-ref/ref-gergeti-hd2d.jpg`（HD-2D 概念图）、`art-ref/sheet-v2-church-hd2d.jpg`（设定画三视图）。用户原话：「外墙和尖顶比例更小一些（调小外墙高度，加高屋顶，钟楼也更胖一些）」。

## 一、先量当前实际比例（照这个口径打印，不要凭感觉）

用教堂世界包围盒与部件高度算出：
```
[PROP_BEFORE] bbox=<W>x<H>x<D> body_h=<f> roof_h=<f> body_roof_ratio=<f> tower_d=<f> tower_h=<f> tower_h_over_body=<f>
```
要求：`body_h` = 主殿墙体（地面到檐口）、`roof_h` = 锥顶（檐口到十字架底）、`tower_d` = 钟楼水平最大直径、`tower_h` = 钟楼总高。

## 二、目标比例（按参考图）

1. **主体压矮**：`body_h` 降低 **12–16%**（不动占地 28×22 m，也不动台地与台阶）
2. **尖顶加高**：`roof_h` 提高 **18–25%**（主塔锥顶与钟楼锥顶都要），`body_roof_ratio` 从当前值降到 **1.15–1.35**（即屋顶约为主体的 0.74–0.87 倍高，视觉上屋顶更挺拔）
3. **钟楼加胖**：`tower_d` 增大 **18–24%**（现在的钟楼偏细像个烟囱），`tower_h` 允许略微降低 5% 以内；加胖后 **钟楼与主殿净距仍 ≥ 3.0 m**（这是既有硬门槛）
4. **保持不动**：总占地 28±1 × 22±1 m、门洞进深 ≥1.2 m、三层递进拱 2.8/2.3/1.9 m、9 级石阶、台地 37×27 m、室内净高不降低（室内天花板高度随主体压低会撞到室内构件——**若冲突，以"室内净高不变"为准，主体最多压到撞线为止**）
5. 装饰物（十字架、窗、门、浮雕）随部件等比缩放，不要出现悬空或穿插

## 三、必须打印

```
[PROP_AFTER] bbox=<W>x<H>x<D> body_h=<f> roof_h=<f> body_roof_ratio=<f> tower_d=<f> tower_h=<f> tower_gap=<f> interior_clear=<f>
[PROP_GATE] body_ok=<bool> roof_ok=<bool> tower_ok=<bool> gap_ok=<bool> interior_ok=<bool> pass=<bool>
```

## 四、硬门槛

- `body_h` 降低到 `[0.84, 0.88] × 原值`
- `roof_h` 提高到 `[1.18, 1.25] × 原值`
- `body_roof_ratio ∈ [1.15, 1.35]`
- `tower_d ∈ [1.18, 1.24] × 原值`、`tower_h ≤ 1.0 × 原值`
- `tower_gap ≥ 3.0`、`interior_clear ≥ 原值`
- 占地 `W ∈ [27, 29]`、`D ∈ [21, 23]`
- 三角面 ≤ 原值 ×1.1（加胖钟楼不能靠堆面数）
- 整帧：`snow_ratio ≥ 0.55`、`white_clip ≤ 0.05`、`magenta_px = 0`、`mountain_luma 0.42–0.60`
- 性能：1920×1080 `fps ≥ 200`、`drawcall ≤ 330`

## 五、必须自己跑

```bash
bash spike/tools/accept.sh --fast
```
外加 `-demo-enter-church`（室内三图，确认室内没被压扁）、`-demo-walk-to-tower`（钟楼可达性不能被加胖破坏）、`-bench` 1920×1080。
截图必出：`art_close.png`、`art_runtime.png`、`style_480_upscaled.png`（480×270 LANCZOS → NEAREST ×4）。

⚠️ 一律用 `open -n -a` + caffeinate。
不加包、不 commit、不改材质配色/玩法/相机/UI。
