只做一件事：加**程序化音频**——钟声（45 m 处响一次）、风雪底噪、脚步。不许下载任何音频素材，全部代码合成。

必读：`Assets/Scripts/MistReveal.cs`（45 m 钟声脉冲的位置）、`Assets/Scripts/PlayerController.cs`（脚步时机）、`docs/02-gameplay.md`（声景：风与脚步先于文字）。

【新增 1 个脚本】`Assets/Scripts/AudioDirector.cs`
1) **钟声**：运行时合成 `AudioClip`（44.1 kHz, 单声道, 4.0 s）：
   - 基频 196 Hz，泛音 [1, 2.02, 2.99, 4.21, 5.43] 相对幅度 [1.0, .55, .38, .22, .12]
   - 每个泛音的衰减指数不同（低频衰减慢）：`exp(-t*(1.1+0.35*i))`
   - 起音 8 ms 线性淡入，避免爆音；末尾 0.2 s 淡出
   - 播放一次，触发点与 MistReveal 的 `bell_at=45m` 同一帧
2) **风雪底噪**：8 s 循环 AudioClip：白噪声 → 一阶低通（α≈0.06）+ 慢速幅度调制（0.08–0.15 Hz，深度 ±35%），音量 0.18，循环播放。
3) **脚步**：0.12 s 噪声脉冲，低通更强（α≈0.25），音量 0.25；在走路动画的**第 0 帧与第 3 帧**各触发一次（即每 6 帧两次）。
4) 所有 AudioSource：`spatialBlend = 0`（2D），不启用 Doppler。

【自检打印】
"[AUDIO] bell_len=<s> bell_rms=<f> wind_len=<s> wind_rms=<f> step_len=<s> step_rms=<f> bell_played_at_d=<m> footsteps=<n>"
硬门槛：bell_rms ≥ 0.02、wind_rms ≥ 0.01、step_rms ≥ 0.01、`bell_played_at_d` 在 44–46 之间、`footsteps ≥ 6`（走 6 秒）。

【必须自己跑】
`-demo-walk 6`（会走路并有脚步；`distance ≥ 15 m`、贴地 ≤ 0.02）
`-demo-reveal`（走到 45 m 时应触发钟声一次，日志确认只触发一次）
`capture_runtime.sh` exit 0 且 `measure_shot.py` 的 `magenta_px = 0`（截图链路不许被音频影响）
退出码全 0，同屏打印 FRAME md5 与 MEASURE。

不加包、不 commit、不改美术材质/山体/雪面/相机/雾/POI 文案/measure_shot.py。
