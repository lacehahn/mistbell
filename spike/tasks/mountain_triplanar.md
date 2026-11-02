只做一件事：修远山岩面的**横向条纹拉伸**，改为三平面（triplanar）采样。不改相机、教堂、角色、地面材质、天空。

必读：spike/project/Assets/Art/Shaders/RidgeFog.shader、spike/project/Assets/Editor/SpikeScene.cs 的 BuildMountains()。

【问题】
现在岩层/斑块噪声按 **XZ 平面**采样（`input.positionWS.xz/47`、`.xz/129`）。山脊近垂直的崖面上，XZ 投影被拉伸，产生**横贯整条山脉的浅色横条**，读成胶合板/花岗岩横纹。

【要改】
1) 岩面细节改 **triplanar**：用 `abs(normalize(normalWS))` 作权重（pow 4 后归一化），分别采样 `positionWS.yz`、`positionWS.xz`、`positionWS.xy`，三张权重混合。岩石细节与层理都用这个。
2) 层理（strata）保留"沿高度成层"的物理感，但**不能连续贯穿山脉**：把 strataPhase 乘以一个按 triplanar 采样的低频扰动（±18°等效抖动），并让层厚随 x 缓变（±35%）。
3) 雪的覆盖判定不变（高度门 + 坡度门）。
4) 远层（aerial=.78）对比度再压 15%。

【自检打印】
"[MTN2] triplanar_weights=ok strata_jitter=0.18 layer_thickness_vary=0.35 far_contrast=-15%"
并跑：
python3 spike/tools/measure_shot.py spike/project/Shots/art_runtime.png
硬门槛（不得退化）：snow_ratio ≥ 0.55、white_clip_ratio ≤ 0.05、dark_ratio_lower 0.02–0.06、ground_shadow_gap ≥ 0.06、magenta_px = 0、hf_energy_lower 0.020–0.045。

【必须自己跑，顺序不变】
Unity -batchmode -quit -projectPath spike/project -executeMethod SpikeBuild.Run -logFile spike/logs/130_build.log
caffeinate -dimu bash spike/tools/capture_runtime.sh
python3 spike/tools/measure_shot.py spike/project/Shots/art_runtime.png

不加包、不 commit、不改相机/教堂/角色/地面材质/天空/measure_shot.py。
