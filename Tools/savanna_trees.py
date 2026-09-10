# What stands in a savanna: acacias, mostly, and the whole look of the country is their shape --
# a bare trunk that splits high and spreads into a canopy flat enough to walk on, so the tree is
# a line and a plate with sky under it. Plus a smaller one, a thorn bush and a clump of tall
# grass. Each on its own foot at the origin.
import bpy, bmesh, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
Build, prism, tube, blob = ft["Build"], ft["prism"], ft["tube"], ft["blob"]
from mathutils import Vector

def crown(b, rng, centre, radius, thick):
    """A plate of leaf: an icosphere squashed nearly flat, dark underneath so the shade reads."""
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0)
    for v in bm.verts:
        v.co.x *= radius*(1+rng.uniform(-0.16,0.16))
        v.co.y *= thick*(1+rng.uniform(-0.2,0.2))
        v.co.z *= radius*(1+rng.uniform(-0.16,0.16))
    for f in bm.faces:
        pts = [(centre[0]+v.co.x, centre[1]+v.co.y, centre[2]+v.co.z) for v in f.verts]
        n = f.normal
        col = ("acacia" if rng.random() < 0.75 else "acacia2") if n.y > 0.25 else ("acacia3" if n.y < -0.2 else "acacia2")
        b.face(pts, col)
    bm.free()

def bole(b, rng, height, base_r, top_r, lean=(0, 0)):
    segs = 4; pts = []; radii = []
    for si in range(segs+1):
        t = si/segs
        pts.append((lean[0]*height*t*t + math.sin(t*3.4)*0.07*t, height*t, lean[1]*height*t*t + math.cos(t*2.8)*0.07*t))
        radii.append(base_r + (top_r - base_r)*t)
    tube(b, pts, radii, 6, ["acaciabark", "acaciabark", "branch"], cap_start=True, cap_end=False)
    return pts[-1]

def limb(b, rng, start, angle, rise, length, radius):
    pts = []; radii = []
    for si in range(4):
        t = si/3
        pts.append((start[0] + math.cos(angle)*length*t,
                    start[1] + rise*length*(t*0.85 + t*t*0.15),
                    start[2] + math.sin(angle)*length*t))
        radii.append(radius*(1 - 0.62*t))
    tube(b, pts, radii, 5, ["acaciabark", "branch"], cap_start=False, cap_end=False)
    return pts[-1]

def acacia(b, rng, height, spread, arms):
    """The shape: nothing at all for two thirds of the way up, then everything at once, out
    sideways, and a flat top over it. A trunk that branched low would read as an oak."""
    top = bole(b, rng, height, 0.20, 0.115, lean=(rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03)))
    tips = []
    for k in range(arms):
        a = k/arms*math.tau + rng.uniform(-0.28, 0.28)
        at = (top[0], top[1] - rng.uniform(0.0, 0.55), top[2])
        tips.append(limb(b, rng, at, a, rng.uniform(0.30, 0.46), spread*rng.uniform(0.42, 0.60), 0.075))
    lid = top[1] + spread*0.30
    crown(b, rng, (top[0], lid, top[2]), spread*0.80, spread*0.115)
    for t in tips:
        crown(b, rng, (t[0]*0.85, max(lid - 0.18, t[1] + 0.16), t[2]*0.85), spread*rng.uniform(0.34, 0.48), spread*0.085)

def plant(index):
    rng = random.Random(7700+index)
    b = Build()
    if index == 0:
        acacia(b, rng, rng.uniform(5.4, 6.6), rng.uniform(6.4, 7.6), 6)
        name = "Acacia"
    elif index == 1:
        acacia(b, rng, rng.uniform(3.4, 4.2), rng.uniform(4.0, 5.0), 5)
        name = "Acacia Small"
    elif index == 2:
        # a thorn bush: grey sticks with dark leaf caught in them, and no trunk to speak of
        for k in range(11):
            a = k/11*math.tau + rng.uniform(-0.4, 0.4)
            h = rng.uniform(0.55, 1.15); lean = rng.uniform(0.30, 0.70)
            pts = [(0, 0, 0), (math.cos(a)*lean*0.45, h*0.55, math.sin(a)*lean*0.45),
                   (math.cos(a)*lean, h, math.sin(a)*lean)]
            tube(b, pts, [0.030, 0.020, 0.011], 4, ["acaciabark", "branch"], cap_start=False, cap_end=False)
            if rng.random() < 0.7:
                crown(b, rng, (pts[2][0], pts[2][1] + 0.05, pts[2][2]), rng.uniform(0.22, 0.36), 0.09)
        name = "Thorn Bush"
    elif index == 3:
        # a termite mound: a hard red spire, weathered into ribs, standing on its own
        h = rng.uniform(1.1, 2.0); r = rng.uniform(0.34, 0.52); sides = 8
        rings = [[(math.cos(k/sides*math.tau)*r*(1 - t*t*0.80)*(1 + math.sin(k*2.1)*0.13),
                   h*t,
                   math.sin(k/sides*math.tau)*r*(1 - t*t*0.80)*(1 + math.sin(k*2.1)*0.13)) for k in range(sides)]
                 for t in (0.0, 0.28, 0.56, 0.82, 1.0)]
        for i in range(4):
            for k in range(sides):
                a0 = (k+0.5)/sides*math.tau
                b.quad(rings[i][k], rings[i][(k+1)%sides], rings[i+1][(k+1)%sides], rings[i+1][k],
                       "termite" if (i + k) % 3 else "savearth2", out=(math.cos(a0), 0.2, math.sin(a0)))
        b.face(rings[-1], "termite", out=(0,1,0))
        # the smaller spires that grow off the side of a big one
        for k in range(rng.randint(1, 3)):
            a = rng.uniform(0, math.tau); d = r*rng.uniform(0.5, 0.9)
            hh = h*rng.uniform(0.3, 0.55); rr = r*rng.uniform(0.3, 0.45)
            prism(b, (math.cos(a)*d, 0, math.sin(a)*d), rr, hh, 6, "termite", "savearth2", taper=0.12)
        name = "Termite Mound"
    else:
        # tall grass, the sort that stands over your knees and hides everything
        drift = rng.uniform(0, math.tau)
        for k in range(15):
            a = k/15*math.tau + rng.uniform(-0.3, 0.3)
            h = rng.uniform(0.55, 1.00); w = 0.030
            bend = rng.uniform(0.16, 0.38)
            base = (math.cos(a)*0.07, 0, math.sin(a)*0.07)
            tip = (base[0] + math.cos(drift)*bend, h, base[2] + math.sin(drift)*bend)
            sx, sz = math.cos(a+math.pi/2)*w, math.sin(a+math.pi/2)*w
            col = "savgrass" if k % 3 else "savgrass2"
            b.quad((base[0]-sx,base[1],base[2]-sz),(base[0]+sx,base[1],base[2]+sz),
                   (tip[0]+sx*0.2,tip[1],tip[2]+sz*0.2),(tip[0]-sx*0.2,tip[1],tip[2]-sz*0.2), col)
            b.quad((tip[0]-sx*0.2,tip[1],tip[2]-sz*0.2),(tip[0]+sx*0.2,tip[1],tip[2]+sz*0.2),
                   (base[0]+sx,base[1],base[2]+sz),(base[0]-sx,base[1],base[2]-sz), "savgrass3")
        name = "Tall Grass"
    return b.make(name)

def main():
    scene, cam = ft["setup_scene"]()
    mat = ft["material"](os.path.join(HERE, "TileWorldPalette.png"))
    plants = [plant(i) for i in range(5)]
    for p in plants:
        p.data.materials.append(mat)
        print("PLANT %-14s verts %4d faces %4d height %.2f" % (p.name, len(p.data.vertices), len(p.data.polygons), max(v.co.z for v in p.data.vertices)))
    xs = [-7.0, 0.0, 4.2, 6.4, 8.4]
    for p, x in zip(plants, xs): p.location = (x, 0, 0)
    ft["look"](cam, (-1.0, 2.6, 0), 22, 9, 0); cam.data.lens = 42
    ft["render"](os.path.join(HERE, "savanna-trees.png"))

    st = {"__file__": os.path.join(HERE, "savanna_tiles.py"), "__name__": "savanna_tiles"}
    exec(compile(open(os.path.join(HERE, "savanna_tiles.py")).read().replace("\nmain()\n", "\n"), "savanna_tiles.py", "exec"), st)
    tiles = [st["tile"](i) for i in range(5)]
    for t in tiles: t.data.materials.append(mat); t.hide_render = True
    for p in plants: p.hide_render = True
    rng = random.Random(13); placed = []
    for gx in range(-8, 8):
        for gz in range(-8, 8):
            ob = bpy.data.objects.new("P", rng.choice(tiles).data); scene.collection.objects.link(ob)
            lift = rng.choice([0, 0, 0, 0.25, -0.25])
            ob.location = (gx*2.0+1.0, -(gz*2.0+1.0), lift)
            ob.rotation_euler = (0, 0, rng.choice([0,1,2,3])*math.pi/2)
            placed.append(ob)
            roll = rng.random()
            which = 0 if roll < 0.022 else 1 if roll < 0.05 else 2 if roll < 0.10 else 3 if roll < 0.122 else 4 if roll < 0.32 else -1
            if which >= 0:
                pl = bpy.data.objects.new("T", plants[which].data); scene.collection.objects.link(pl)
                pl.location = (ob.location.x + rng.uniform(-0.5, 0.5), ob.location.y + rng.uniform(-0.5, 0.5), lift + 1.05)
                pl.rotation_euler = (0, 0, rng.uniform(0, math.tau))
                s = rng.uniform(0.85, 1.2); pl.scale = (s, s, s)
                placed.append(pl)
    ft["look"](cam, (0, 2.0, 0), 30, 9, 26); cam.data.lens = 42
    ft["render"](os.path.join(HERE, "savanna-plain.png"))
    for ob in placed: bpy.data.objects.remove(ob)

    for p in plants:
        p.hide_render = False; p.location = (0,0,0); p.rotation_euler = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT'); p.select_set(True); bpy.context.view_layer.objects.active = p
        bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, p.name + ".fbx"), use_selection=True,
                                 axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL',
                                 mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
