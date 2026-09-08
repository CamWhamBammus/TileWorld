# Snow trees for Tile World, built in Blender like the others: two laden spruces, a tall fir, a bare
# birch with snow along its branches, and a young spruce; each on its foot at the origin, flat
# shaded, coloured by the palette. Renders them alone and in the snowfield, and exports FBX.
import bpy, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
Build, tube, blob = ft["Build"], ft["tube"], ft["blob"]
tsrc = open(os.path.join(HERE, "forest_trees.py")).read().replace("\nmain()\n", "\n")
tt = {"__file__": os.path.join(HERE, "forest_trees.py"), "__name__": "forest_trees"}
exec(compile(tsrc, "forest_trees.py", "exec"), tt)
trunk = tt["trunk"]
from mathutils import Vector

def tier(b, rng, centre, radius, height, sides, droop=0.18):
    """One tier of a laden conifer: a skirt of dark needles hanging down, and a cap of snow lying on it."""
    cx, cy, cz = centre
    rim = []
    for k in range(sides):
        a = k/sides*math.tau; r = radius*(1+rng.uniform(-0.08,0.08))
        rim.append((cx+math.cos(a)*r, cy - droop*radius + rng.uniform(-0.04,0.04), cz+math.sin(a)*r))
    peak = (cx, cy+height, cz)
    # the underside and the snow on top: the same skirt, its faces dark below and white above
    for k in range(sides):
        a0 = (k+0.5)/sides*math.tau
        b.tri(rim[k], rim[(k+1)%sides], peak, "snow1" if k % 2 == 0 else "snow2", out=(math.cos(a0), 0.9, math.sin(a0)))
    inner = [(cx+math.cos(k/sides*math.tau)*radius*0.55, cy - droop*radius*0.3, cz+math.sin(k/sides*math.tau)*radius*0.55) for k in range(sides)]
    for k in range(sides):
        a0 = (k+0.5)/sides*math.tau
        b.quad(rim[k], inner[k], inner[(k+1)%sides], rim[(k+1)%sides], "needle" if k % 2 else "needle2", out=(math.cos(a0)*0.3, -1, math.sin(a0)*0.3))
        # the green edge of the tier showing under the snow
        b.tri(rim[k], rim[(k+1)%sides], (cx+math.cos(a0)*radius*1.02, rim[k][1]+0.06, cz+math.sin(a0)*radius*1.02), "needle3", out=(math.cos(a0), 0.2, math.sin(a0)))

def spruce(index, tiers=5, height=5.6, spread=1.5):
    rng = random.Random(1100+index); b = Build()
    h = height*rng.uniform(0.92, 1.08)
    top = trunk(b, rng, h*0.92, 0.17, 0.05, lean=(rng.uniform(-0.02,0.02), rng.uniform(-0.02,0.02)), sides=6, wobble=0.02)
    for k in range(tiers):
        t = k/(tiers-1)
        y = h*0.22 + (h*0.75)*t
        r = spread*(1.0 - 0.72*t) + 0.15
        tier(b, rng, (top[0]*t, y, top[2]*t), r, 0.55*r + 0.25, 8)
    # a snowy tip
    blob(b, (top[0], h, top[2]), (0.12, 0.2, 0.12), "snow1", "needle", rng, sub=0, squash=0.6, moss_from=0.2)
    return b.make("Snow Spruce %d" % index)

def fir(index):
    rng = random.Random(1150+index); b = Build()
    h = rng.uniform(6.8, 7.6)
    top = trunk(b, rng, h*0.94, 0.18, 0.05, lean=(rng.uniform(-0.02,0.02), rng.uniform(-0.02,0.02)), sides=6, wobble=0.02)
    tiers = 7
    for k in range(tiers):
        t = k/(tiers-1)
        y = h*0.2 + (h*0.78)*t
        r = 1.25*(1.0 - 0.75*t) + 0.12
        tier(b, rng, (top[0]*t, y, top[2]*t), r, 0.5*r + 0.2, 7, droop=0.24)
    blob(b, (top[0], h, top[2]), (0.1, 0.18, 0.1), "snow1", "needle", rng, sub=0, squash=0.6, moss_from=0.2)
    return b.make("Snow Fir %d" % index)

def bare_birch(index):
    rng = random.Random(1200+index); b = Build()
    h = rng.uniform(4.6, 5.4)
    segs = 6; pts=[]; radii=[]
    lean = (rng.uniform(-0.05,0.05), rng.uniform(-0.05,0.05))
    for s in range(segs+1):
        t = s/segs
        pts.append((lean[0]*h*t*t + math.sin(t*6.0)*0.05*t, h*t, lean[1]*h*t*t)); radii.append(0.14 + (0.05-0.14)*t)
    P=[Vector(p) for p in pts]; rings=[]; u=None
    for i in range(len(P)):
        axis=(P[min(i+1,len(P)-1)]-P[max(i-1,0)]).normalized()
        u = axis.cross(Vector((1,0,0))).normalized() if u is None else (u - axis*u.dot(axis)).normalized()
        v = axis.cross(u).normalized()
        rings.append([tuple(P[i] + (u*math.cos(k/6*math.tau)+v*math.sin(k/6*math.tau))*radii[i]) for k in range(6)])
    for i in range(len(P)-1):
        for k in range(6):
            mid=(Vector(rings[i][k])+Vector(rings[i+1][(k+1)%6]))*0.5
            b.quad(rings[i][k], rings[i+1][k], rings[i+1][(k+1)%6], rings[i][(k+1)%6], "birchmark" if (i+k)%4==0 else "birch", out=tuple(mid-(P[i]+P[i+1])*0.5))
    b.face(rings[0], "birch", out=(0,-1,0)); b.face(rings[-1], "birch", out=(0,1,0))
    # bare branches, snow lying along their upper sides and at the tips
    for k in range(7):
        t = 0.45 + 0.5*k/7
        a = k*2.4 + rng.uniform(-0.4,0.4)
        base = Vector(pts[int(t*segs)]) 
        length = rng.uniform(0.7, 1.3)*(1.1 - t*0.5)
        d = Vector((math.cos(a), 0.55 + rng.uniform(-0.1,0.2), math.sin(a))).normalized()
        bpts = [tuple(base + d*length*q + Vector((0, 0.15*length*q*q, 0))) for q in (0, 0.5, 1)]
        tube(b, bpts, [0.045, 0.03, 0.015], 4, ["birchmark","birch"], cap_start=False)
        for q in (0.35, 0.7, 1.0):
            p = Vector(bpts[0]).lerp(Vector(bpts[2]), q)
            blob(b, (p.x, p.y+0.05, p.z), (0.07, 0.04, 0.07), "snow1", "snow2", rng, sub=0, squash=0.4, moss_from=-1)
        for j in range(2):
            a2 = a + rng.uniform(-1.0, 1.0)
            tip = Vector(bpts[2]); d2 = Vector((math.cos(a2), 0.9, math.sin(a2))).normalized()
            tube(b, [tuple(tip), tuple(tip + d2*rng.uniform(0.3,0.5))], [0.015, 0.008], 3, ["birchmark"], cap_start=False)
    return b.make("Snow Birch %d" % index)

def young(index):
    return spruce(index+20, tiers=3, height=2.4, spread=0.8)

def main():
    scene, cam = ft["setup_scene"]()
    mat = ft["material"](os.path.join(HERE, "TileWorldPalette.png"))
    trees = [spruce(0), spruce(1), fir(0), bare_birch(0), young(0)]
    for t in trees: t.data.materials.append(mat); print("TREE %s verts %d faces %d height %.1f" % (t.name, len(t.data.vertices), len(t.data.polygons), max(v.co.z for v in t.data.vertices)))
    xs = [-8.6, -5.0, -1.2, 3.0, 6.6]
    for t, x in zip(trees, xs): t.location = (x, 0, 0)
    ft["look"](cam, (0, 2.8, 0), 21, 10, 0); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "snowtrees-lineup.png"))
    for t in trees: t.hide_render = True
    ssrc = open(os.path.join(HERE, "snow_tiles.py")).read().replace("\nmain()\n", "\n")
    st = {"__file__": os.path.join(HERE, "snow_tiles.py"), "__name__": "snow_tiles"}; exec(compile(ssrc, "snow_tiles.py", "exec"), st)
    tiles = [st["tile"](i) for i in range(5)]
    for t in tiles: t.data.materials.append(mat); t.hide_render = True
    rng = random.Random(31); placed = []
    for gx in range(-5, 5):
        for gz in range(-5, 5):
            h = round(0.8*math.sin(gx*0.5)*math.cos(gz*0.4)*2)/2*0.25 + rng.choice([0,0,0,0.25])
            ob = bpy.data.objects.new("P", rng.choice(tiles).data); scene.collection.objects.link(ob)
            ob.location = (gx*2.0+1.0, -(gz*2.0+1.0), h); ob.rotation_euler = (0,0,rng.choice([0,1,2,3])*math.pi/2); placed.append(ob)
            if rng.random() < 0.2:
                tr = bpy.data.objects.new("T", rng.choice(trees).data); scene.collection.objects.link(tr)
                tr.location = (gx*2.0+1.0+rng.uniform(-0.3,0.3), -(gz*2.0+1.0)+rng.uniform(-0.3,0.3), h+1.05); tr.rotation_euler = (0,0,rng.uniform(0,math.tau)); placed.append(tr)
    ft["look"](cam, (0, 2.0, 0), 21, 26, 30); cam.data.lens = 38
    ft["render"](os.path.join(HERE, "snowfield.png"))
    ft["look"](cam, (1, 1.6, 2), 9, 10, 55); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "snowfield-low.png"))
    for ob in placed: bpy.data.objects.remove(ob)
    for t in trees:
        t.hide_render = False; t.location = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT'); t.select_set(True); bpy.context.view_layer.objects.active = t
        bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, t.name + ".fbx"), use_selection=True, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL', mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
