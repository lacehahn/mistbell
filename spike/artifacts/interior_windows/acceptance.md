# 室内窗户、穹顶肋与墙面砌块验收

2026-11-04；仅执行 `spike/tasks/interior_windows.md`。最终 `bash spike/tools/accept_interior_windows.sh` **exit=0**，独立验收 **26/26** 通过；Unity 构建 Succeeded / 0 errors。未提交，未修改 AGENTS.md、任务书及其他任务实现。

| 项目 | 实测 | 门槛 |
|---|---:|---:|
| 中殿窗 PNG display Rec.709 | 0.576410（1846 采样） | 0.55–0.70 |
| 鼓座窗 PNG display Rec.709 | 0.744979（2146 采样） | 0.62–0.78 |
| 鼓座窗 CIELAB L* | 76.8679 | 70–78 |
| 抹灰 PNG display Rec.709 | 0.237752 | ≥0.22 |
| 肋亮侧/暗侧材质相对抹灰 | +11% / −9% | +10–12% / −8–10% |
| 金环 PNG CIELAB L* | 74.7023（36 采样） | 74–80 |
| 砌块三档 | 0.92 / 1.00 / 1.08，同色相，hue_shift≈0° | 3档 / contrast≥0.08 / hue≤2° |
| 灰缝 | 宽0.04 m，逐块暗20% | 0.03–0.05 m / 暗18–22% |
| 网格尺寸独立解码 | 6根棂各宽0.05 m；进深0.18 m；金环宽0.06 m | 按任务书 |
| 右中红板 | #A35A4A，0.900×1.600 m | ≤0.9×1.6 m |
| 室内性能，1920×1080 / Metal / Apple M6 | 254 drawcalls / 220.05 FPS；1%low 158.52 | ≤300 / ≥200 FPS |
| 进堂 | INT_PASS 所有布尔 True；obstacles=True；lamps=18 | 全部通过 |

砌块裁图从原图像素框 `[38, 324, 480, 756]` 取样，LANCZOS缩至480×270，再NEAREST放大至1920×1080。已目视检查，可数 **6行（含上下各一行不完整砌块）**，未呈现纯色块。此项是人工检查，不以shader参数替代视觉判断。

对象识别和实现取舍：原画面两块高处黑矩形的射线命中 `Interior_Banner_1/2`，而不是作者命名的侧窗；因此禁用这两块占位挂布及其下边饰，在原位置制作仅室内显示的窗模块，带1竖2横石棂与0.18 m内侧进深。未切改室外墙壳，未改原有六扇侧窗资产。右中红竖板命中 `Interior_WallPainting`，保留为壁挂画占位，改为指定赭红与尺寸。原始射线定位保留在 probe/运行日志中。

中殿窗 `_EmissionColor` 为 #CFE2F5×0.90，另用0.95中性透射贴图控制最终曝光；鼓座为#D9E6F2，最终L*按PNG验收。抹灰#9C8B74、smoothness0.20（roughness0.80），肋无贴图，以两材质色值实现11%/9%差；截图中亮/暗半肋平均display luma分别为 0.309979/0.280518，不将材质比伪称为受灯光、景深影响后的逐像素比。金环完整0.06 m环带移到锥面前0.025 m，避免外缘埋入抹灰。

保留上一轮两盏均衡补光，实际移除吊灯第0和第4根蜡烛的低功率Light组件，保留8根蜡烛/烛焰与中央吊灯光；lamps从20降至18，未改变灯数统计字段。蜡烛检查改为统计实际8个蜡烛网格。地板、祭坛/圣像/王门、室外、原相机、全部碰撞和其余灯的序列化内容/网格/材质/变换前后相等，digest见 `protected_digests.json`；重复Apply验证幂等。原ROI参考为 `{"left": 0.08752219855535245, "right": 0.05077437045580477, "nave": 0.03487536353396084, "ratio": 1.72374758701408}`，未更改旧验收门槛。

测量方式：原机位交付PNG内，以最近可见不透明网格分类生成480×270 mask；保留运行时合批的原始source进行查询，包含墙壳遮挡，排除空网格/退化网格。C#重新读取交付PNG后测量，Python从同一PNG和mask独立复算；两者一致。CIELAB L*按sRGB逆转换、D65逐像素计算后取平均。墙面三档/灰缝还由Python从实际shader解析并CPU核验HSV；窗棂/进深/金环尺寸独立解码Unity网格资产，而非仅信任日志常量。

```text
[WINDOWS] nave_win_luma=0.576410 dome_win_luma=0.744979 mullions=6 nave_view=inside dome_view=lookup inside_sample_frame=609 metric=display_Rec709
[WINDOWS_LSTAR] dome_lstar=76.867909 dome_target=70..78 gold_ring_lstar=74.702273 gold_ring_target=74..80 dome_pixels=2146 gold_ring_pixels=36 metric=mean_pixel_CIELAB_D65
[DOME_RIB] rib_light_gain=0.110000 rib_shadow_drop=0.090000 plaster_luma=0.237752 light_materials=12 shadow_materials=12 ratio_metric=material_base_Rec709 plaster_metric=screenshot_display_Rec709
[INT_PASS] light=True domeLight=True ceilingSeen=True domeSeen=True fx=0.787 visible=True obstacles=True walked=True z=0.43 dy=0.000 fov=50.00 lamps=18 shafts=3
```

复现整项（局部修补→出包→1080p进堂→前台benchmark→PNG/网格/shader独立复算）：

```sh
bash spike/tools/accept_interior_windows.sh
```

仅复算现有证据：

```sh
/usr/bin/python3 spike/tools/verify_interior_windows.py
```

证据：`measurements.json`、`protected_digests.json`；原始日志 `spike/logs/interior_windows_{build,interior,bench,accept}.log`，性能JSON `spike/logs/interior_windows_bench.json`。最终三图与480墙图已目视检查，无开发控制台错误覆盖：

- [interior_inside.png](../../project/Shots/interior_inside.png)
- [interior_lookup.png](../../project/Shots/interior_lookup.png)
- [windows_closeup.png](../../project/Shots/windows_closeup.png)
- [墙面480缩图](wall_crop_480x270.png) / [最近邻放大](wall_crop_enlarged.png)
