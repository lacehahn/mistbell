只做一件事：**光照重做（修正版）**——上一版（v14）解决了黑面，但把画面变成了冷灰、平、没对比。这一版要**在有环境光的同时保住"暖墙冷影 + 强体积"**。

参考：`art-ref/ref-gergeti-hd2d.jpg`（暖土黄石墙、冷蓝阴影、强方向光、几乎无高光）。

## 一、v14 犯的错（必须避免）

- 环境光（冷蓝 `#849DBB`）变成了主光源 → 全画面偏冷、对比被拉平、远了像雾
- 主光强度被削弱 → 墙面失去暖土黄，变成灰蓝

## 二、这一版的目标（都在**渲染后画面**上量）

| 指标 | 目标 | 含义 |
|---|---|---|
| 受光墙面 `b*` | **+12 ~ +18** | 暖土黄还在 |
| 背光墙面 `b*` | **−6 ~ 0** | 阴影偏冷 |
| `lit_L − shadow_L` | **≥ 12** | 保留强体积，不能一片灰 |
| `shadow_face_L` | **≥ 18** | 背光面不死黑（v14 的成果要保留） |
| 整帧平均 `b*` | **≥ +2** | 全画面不许偏蓝 |
| 暖色像素占比（`b* > +8`） | **≥ 8%** | 暖色不能被吃掉 |

## 三、做法

1. **主光回到主导**：方向光 `intensity 1.8–2.0`、色 `#FFE0B8`（暖）、仰角 30°、从相机左后方斜射；阴影强度不变
2. **环境光只做填充**：`ambientSkyColor #8C97A6`（中性偏冷，不要饱和蓝）、`ambientEquatorColor #7E8794`、`ambientGroundColor #6B6E73`；总环境贡献控制在受光面的 **35% 以内**
3. **着色器补下限**：`TerrainHaze.shader` 里对朝下面法线给 `max(SampleSH(n), 0.10)`，保证 `L*≥18`（v14 已实现，保留这段，不要回退）
4. **去高光**：石墙/屋顶/台地墙 `_Specular=0`、`_Smoothness=0` 保留（v14 的做法）
5. 雪面允许冷（`b* −8~−2`），但**雪不能被环境光染成蓝色平板**

## 四、必须打印

```
[LIGHT3] lit_L=<f> lit_b=<f> shadow_L=<f> shadow_b=<f> split_L=<f> shadow_face_L=<f>
[LIGHT3_FRAME] mean_b=<f> warm_pct=<f> specular_delta=<f>
[LIGHT3_GATE] warm_ok=<bool> cool_shadow_ok=<bool> volume_ok=<bool> shadow_ok=<bool> frame_ok=<bool> pass=<bool>
```

## 五、硬门槛

- `lit_b ∈ [12, 18]`、`shadow_b ∈ [−6, 0]`、`split_L ≥ 12`
- `shadow_face_L ≥ 18`、`specular_delta ≤ 0.04`
- `mean_b ≥ 2`、`warm_pct ≥ 0.08`
- 整帧：`snow_ratio ≥ 0.55`、`white_clip ≤ 0.05`、`magenta_px = 0`、`mountain_luma 0.45–0.55`（远山不许被雾吃掉）
- 性能：1080p `fps ≥ 200`、`drawcall ≤ 330`

## 六、必须自己跑

```bash
bash spike/tools/accept.sh --fast
```
外加 `-demo-enter-church`（室内三图：室内仍要昏暗，`interior_luma` 不得变白）、`-bench`。
截图必出：`art_close.png`、`art_runtime.png`、`interior_inside.png`、`style_480_upscaled.png`。

⚠️ 一律用 `open -n -a` + caffeinate。
不加包、不 commit、不改材质贴图/几何/玩法/相机/UI。
