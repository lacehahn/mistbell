只做一件事：**存档 / 继续游戏 / 设置（音量、中英切换）**。不改美术、不改场景几何、不改相机机位。

必读：`PlayerController.cs`、`InfoPanel.cs`、`PoiData.cs`、`AudioDirector.cs`、`ChurchInterior.cs`。

【1 存档】
- 路径：`Application.persistentDataPath/mistbell_save.json`（UTF-8）。
- 内容：`position{x,y,z}`、`rotationY`、`insideInterior`（bool）、`doorOpen`（bool）、`visitedPois: string[]`、`version: 1`、`savedAtUnix`。
- 自动存档时机：①首次踏上台地（z ≥ −16）②首次进入室内 ③打开任意 POI 面板关闭时。
- 启动时若存在存档：显示一行提示「检测到上次进度 · 按 C 继续 / 按 N 重新开始（3 秒后自动继续）」；按 N 时删除存档并从 Z01 出生。

【2 继续】
- 按 C 或超时：恢复位置/朝向/是否在室内（在室内则门为开启态）/已访问 POI（面板里可显示"已读"标记）。
- 恢复后 `[SAVE] load pos=(x,y,z) inside=<bool> door=<bool> pois=<n>`。

【3 设置面板】
- `Esc` 在无 POI 面板时打开设置：音量滑条（0–100%，默认 80）、语言（中文 / English / 双语，默认双语）、「返回游戏」「退出游戏」。
- 语言只影响 POI 面板正文显示：中文模式只显示中文段、English 只显示英文段、双语文案照现在。
- 设置写 `mistbell_settings.json`（音量、语言），启动时读取并应用（音量作用于 AudioDirector 的 master 音量）。

【4 回归不许退化】
现有全部 demo（walk / walk-stairs / walk-to-tower / cinema / reveal / enter-church / poi）必须仍然 exit 0；`capture_runtime.sh` exit 0；`measure_shot.py` magenta_px=0。

【自检打印】
`[SAVE] path=<abs> wrote=true keys=position,rotationY,insideInterior,doorOpen,visitedPois`
`[SAVE] load pos=(x,y,z) inside=<bool> door=<bool> pois=<n> delta_m=<f>`
`[SETTINGS] volume=<0..1> language=<cn|en|both> path=<abs>`
硬门槛：`delta_m ≤ 0.05`、`pois` 恢复数一致、切换语言后 `[POI] body_lang=<cn|en|both>` 与设置一致。

【必须自己跑】
新增演示：`-demo-save`（走到台地→进室内→打开 p04→存档→退出码 0）、`-demo-load`（读档后校验 delta_m）、`-demo-settings`（切 cn→en→both，各截一张 `Shots/settings_<lang>.png` 并打印 `[POI] body_lang=`）。
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改美术/几何/相机机位/measure_shot.py。
