# terrain_slope 验收

仅执行 `spike/tasks/terrain_slope.md`。横向混合区改为60–140m，10m外坡肩按0.11m/m增长，90m封顶；保留路线高度。路槽内4.5m、z≥-40的顶点和法线/切线保持原值；其他场景组件及Settings资产内容前后相同。不增加岩脊，不修改台阶、围栏、人物、雾或常态玩法/reveal相机。

为保留现有雪面细节及所有已加工资产，构建脚本在现有Terrain.asset上施加新旧TerrainHeight的高度差，不重建全场景。`terrain_baseline.json.gz`为本任务修改前的地形快照，重复执行从同一快照重算，不累加高度；两次构建的六项坡度测量一致。此快照针对当前场景，不应套用到将来另行重建的地形。

测量使用最终Terrain MeshCollider逐点向下射线，左右均检验。field_rise在各z截面取CenterX(z)±30m，比较z=-205和z=-40；包含教堂区域原有接缝，不外推路线高度代替实测。

运行：

```bash
/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "$PWD/spike/project" -executeMethod TerrainSlopeBuild.Run -logFile "$PWD/spike/logs/terrain_slope_build.log"
caffeinate -dimu -t 700 python3 spike/tools/verify_terrain_slope.py
```

未运行rebuild_all；未修改AGENTS.md；未提交。构建日志位于`../../logs/terrain_slope_build.log`，播放器原始日志与[机器验收结果](acceptance.json)位于本目录。

截图：[侧后方雪坡](climb_slope.png)、[玩法视图](play_view.png)、[台阶](climb_steps.png)。雪坡截图使用独立命令`-demo-terrain-slope`，仅该次进程对远景重新对焦；不保存或修改常态相机、雾和Volume资产。

最终验收：全部通过，验收命令退出码0，macOS构建Succeeded/0errors。

| 项目 | 实测 |
| --- | --- |
| z=-150横向10m，左/右 | 1.121759 / 1.156958 m |
| z=-95横向10m，左/右 | 0.937762 / 1.322863 m |
| field_rise，左/右 | 21.127580 / 21.017280 m |
| walk6 | 17.734 m，bad=0，贴地误差0，漏射线0，exit=0 |
| 入堂/reveal | exit=0/0，bad=0/0，reveal gate=pass |
| 台阶 | flights=3，bad=0，exit=0 |
| 1920×1080室外平均FPS | z01=211.35，terrace=224.75 |
| 保护 | protected_vertices_changed=0，protected_unchanged=True |
| 三张图 | 1920×1080，均已生成并目视检查 |
