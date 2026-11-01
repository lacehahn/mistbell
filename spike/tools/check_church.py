"""独立校验 .blend：包围盒、三角面、每个物体的尺寸（判断布尔开洞是否真的做了）。"""
import bpy
import sys

path = "/Users/lance/workspace/mistbell/spike/blend/gergeti_church.blend"
bpy.ops.wm.open_mainfile(filepath=path)

tri = 0
lo = [1e9] * 3
hi = [-1e9] * 3
rows = []
for ob in bpy.data.objects:
    if ob.type != 'MESH':
        continue
    me = ob.data
    me.calc_loop_triangles()
    t = len(me.loop_triangles)
    tri += t
    mw = ob.matrix_world
    for v in me.vertices:
        w = mw @ v.co
        for i in range(3):
            lo[i] = min(lo[i], w[i])
            hi[i] = max(hi[i], w[i])
    rows.append((ob.name, t, tuple(round(x, 2) for x in ob.dimensions)))

size = [round(hi[i] - lo[i], 2) for i in range(3)]
print("[CHECK] objects=", len(rows))
print("[CHECK] triangles=", tri)
print("[CHECK] bbox X=%.2f Y=%.2f Z=%.2f (目标 34 x 24 x 27)" % (size[0], size[1], size[2]))
print("[CHECK] bbox_in_range=", abs(size[0] - 34) <= 3 and abs(size[1] - 24) <= 3 and abs(size[2] - 27) <= 3)
print("[CHECK] --- 逐物体 ---")
for name, t, dim in sorted(rows, key=lambda r: -r[1])[:20]:
    print("[CHECK]   %-28s tris=%-5d dim=%s" % (name, t, dim))
