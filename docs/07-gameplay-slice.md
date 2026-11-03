# 07 · 可玩切片（M1 验证）

> 状态：**可玩、可走进教堂内部**。记录时间：2026-11-02 23:20

## 1. 怎么玩

```bash
open spike/project/Builds/Mac/Mistbell.app
```

| 操作 | 键 |
| --- | --- |
| 移动 | `W A S D` / 方向键 |
| 加速 | 按住 `Shift`（3.0 → 5.0 m/s） |
| 交互 / 开门 | `E`（进入 POI 半径时左下角出现「按 E 查看」） |
| 室内抬头看顶 | `F`（仅室内生效，切回再按一次） |
| 关闭面板 | `E` 或 `Esc` |
| 设置菜单 | `Esc`（无 POI 面板时打开：音量 / 中文 / English / 双语 / 返回 / 退出） |
| 启动续玩 | 有存档时按 `C` 继续、`N` 重新开始（3 秒后自动继续） |

带 `-shot` / `-shots` 参数启动时**完全走截图模式**，不会启用玩家控制（保证美术验收链路可复现）。

### 札记与脚印

- **已读标记**：打开过的 POI 在正文顶部显示「已读 · 第 N 次打开」，标题右侧带「· 已读」
- **札记列表**：`Esc` → 设置菜单「札记 Notes」，列出全部 8 个 POI；未读显示「未读 · ？？？」且不可点，已读可回看
- **脚印**：按累计位移每 0.75 m 落一片（0.26×0.44 m，左右脚偏移 ±0.18 m），120 片环形复用；石板与室内地面不落，台阶边缘（0.5 m 内地面高差 >0.12 m）不落；场景原有的 64 个静态脚印保留

### 存档

- 路径：`~/Library/Application Support/Lot/Mistbell/mistbell_save.json`（设置同名 `mistbell_settings.json`）
- 自动存档点：首次踏上台地、首次进入室内、关闭任意 POI 面板
- 内容：位置 / 朝向 / 是否在室内 / 门是否开启 / 已访问 POI 列表
- 实测：`[SAVE] load pos=(-2.0,0.374,5.029) inside=true door=true pois=1 delta_m=0.000000`

## 2. 已验证的体验（都有日志数字）

| 体验 | 证据 |
| --- | --- |
| 攀爬 | `[PLAY] distance=18.106 ground_error=0.000000 row=3`；上台阶 `on_deck=true`（0.313 vs deck 0.286） |
| 碰撞 | `[COLLIDE] church_colliders=3 wall_colliders=28 clearance=0.310`（被门挡在 0.31 m 外） |
| 云雾显现 | 两段式曲线；`[REVEAL] gate=pass ratio20_80=1.949`；钟声 `bell_at=44.861 m` 只响一次 |
| 到达仰视 | `[CINEMA] church_top=0.0433 bottom=0.8084 once=true restore_error=0.000` |
| 进门 | `[INTERIOR_U] door_open=100° walk_in=true player_inside=true fov=32` |
| 室内 | `interior_luma=0.3415`；长椅/祭坛/圣像屏/吊灯全可见；`floor_slabs=218`、`beams=5` |
| 音频 | 钟 `rms=0.110` / 风 `0.104` / 脚步 `0.074`，全部代码合成、无外部素材 |

## 3. POI（数据驱动，`PoiData` + `PoiDatabase`）

| id | 名称 | 位置 |
| --- | --- | --- |
| `gergeti_p01` | 正门 · Main Portal | 台阶顶 (0, −13) |
| `gergeti_p02` | 钟楼 · Bell Tower | 塔前 (10.5, −4.4) |
| `gergeti_p03` | 门廊浮雕 · Portal Relief | 门前 (0, −14.5) |
| `gergeti_p04` | 圣像屏 · Iconostasis | 室内后墙前 |
| `gergeti_p05` | 烛台 · Votive Candles | 室内左侧 |
| `gergeti_p06` | 壁画残片 · Wall Painting | 室内右墙 |
| `gergeti_p00` | 回望点 · The Lookback | Z03 折返处（路线加长后新增） |
| `gergeti_p0a` | 界石 · Boundary Stone | Z01→Z02 转折（路线加长后新增） |

文案遵循 docs/02 的「标题 + 2–4 段 + 出处」口径；中文用显式 `\n` 手工排版（Unity legacy Text 会在空格与中日韩/数字边界断行）。

## 4. 复现命令

```bash
UNITY=/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity
$UNITY -batchmode -quit -projectPath spike/project -executeMethod SpikeScene.Run  -logFile spike/logs/scene.log
$UNITY -batchmode -quit -projectPath spike/project -executeMethod SpikeBuild.Run  -logFile spike/logs/build.log

APP=spike/project/Builds/Mac/Mistbell.app/Contents/MacOS/Mistbell
caffeinate -dimu $APP -demo-walk 6              # 走路回归
caffeinate -dimu $APP -demo-walk-stairs         # 台阶 + 落台
caffeinate -dimu $APP -demo-walk-to-tower       # 钟楼可达
caffeinate -dimu $APP -demo-cinema              # 到达仰视过场
caffeinate -dimu $APP -demo-reveal              # 三档云雾
caffeinate -dimu $APP -demo-enter-church        # 进门 + 室内
caffeinate -dimu $APP -demo-poi gergeti_p04     # 指定 POI 面板
caffeinate -dimu bash spike/tools/capture_runtime.sh   # 美术机位截图
python3 spike/tools/measure_shot.py spike/project/Shots/art_runtime.png
```

## 5. 还没做（明确边界）

- **多 Zone 完整路线**：文档是 6 Zone / 25 分钟；当前为 3 段折返（加长进行中）
- **呼吸与体力**：docs/02 有取舍，未实现
- **存档 / 设置 / 本地化切换**：未做
- **性能基线**：尚未在 1920×1080 下做 drawcall / fps 实测（任务书 `performance_bench.md` 已写）
- **室内天花板与吊灯可见性**：天花板建在 11.6 m（人眼平视看不到）；吊灯投影指标算出 −8.7，实际仍在画面外
