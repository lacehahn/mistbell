# 任务：长椅装反了——整组镜像回来（用户发现）

## 结论（已用几何确认）
`InteriorPewsBuild.cs` 第 66–71 行：
```
float z = .55f + row * pitch;                    // 排距沿 +Z 递增
Box(seat,  new Vector3(x, fy+.455f, z))          // 座面
Box(back,  new Vector3(x, ...,      z + backZ))  // 靠背在座面的 +Z 侧（backZ ≈ +0.21）
```
而**祭坛在 +Z 方向**（王门在 `z=7.55`，祭坛在它前面、比长椅更靠 +Z；入口在 `z≈-9`）。
靠背落在座面的 +Z 侧 = 靠背朝着祭坛 ⇒ **人坐下去是背对祭坛、面朝大门**，整组长椅前后反了。

## 要做的
1. 把长椅的靠背、靠背上沿收边条、靠背倾角统统镜像到座面的 **−Z 侧**：
   - 靠背位置：`z + backZ` → `z - backZ`
   - 倾角：`Quaternion.Euler(6,0,0)` → `Quaternion.Euler(-6,0,0)`
   - 收边条同步：`z + .21 + .42*sin(a)` → `z - (.21 + .42*Mathf.Sin(angle))`
   - 端板/椅腿/接触阴影位置不变（它们是对称的），只动靠背相关件。
2. 坐姿方向自检要机械化，写进日志：
   `[PEW_FACING] back_z_side=-1 seat_front_z=+1 faces_altar=true altar_z≈… pew_z≈…`
   判据：靠背中心 z 必须**小于**座面中心 z（因为祭坛在 +Z）。
3. 截图 `pews_side.png`（从侧后方 45° 看一排，能看出靠背在远离祭坛那侧）+ 复用 `pews_closeup.png`。

## 不要动
- 排数（6）、宽度（2.35 m）、净空（0.28 m）、材质、地板、光照、祭坛、王门、室外、相机。
- `-demo-enter-church` 全绿（`lamps=20`、`obstacles=True`）。

## 交付
- `[PEW_FACING]` 日志一行；
- 两张截图；
- 说明改了哪几处（列出代码位置）。
