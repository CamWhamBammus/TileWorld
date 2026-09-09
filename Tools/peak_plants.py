# What stands on the peaks: not much, and all of it bent. Two krummholz -- the stunted wind-shorn
# trees of the treeline, which grow away from the weather and go flat on top -- a cushion plant, a
# tussock, and an erratic left by the ice. Built in Blender on their own feet at the origin.
import bpy, bmesh, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
Build, prism, tube, blob, cylinder_along = ft["Build"], ft["prism"], ft["tube"], ft["blob"], ft["cylinder_along"]
from mathutils import Vector

def needle_mass(b, rng, centre, size, flat=0.42):
    """A cushion of needles: dark, dense, and pressed flat by the wind."""
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0)
    for v in bm.verts:
        v.co.x *= size[0]*(1+rng.uniform(-0.2,0.2))
        v.co.y *= size[1]*flat*(1+rng.uniform(-0.15,0.15))
        v.co.z *= size[2]*(1+rng.uniform(-0.2,0.2))
    for f in bm.faces:
        pts = [(centre[0]+v.co.x, centre[1]+v.co.y, centre[2]+v.co.z) for v in f.verts]
        n = f.normal
        col = "needle" if n.y > 0.35 else ("needle3" if n.y < -0.3 else rng.choice(["needle2", "needle2", "needle"]))
        b.face(pts, col)
    bm.free()

def krummholz(b, rng, height, sprawl, wind):
    """A tree that has given up going upward: the trunk leans away from the weather, every branch
    goes downwind, the top is shorn level, and the windward side is bare dead wood."""
    wx, wz = math.cos(wind), math.sin(wind)
    segs = 4
    pts = []; radii = []
    for si in range(segs+1):
        t = si/segs
        pts.append((wx*sprawl*t*t*0.8, height*t, wz*sprawl*t*t*0.8))
        radii.append(0.14*(1 - 0.55*t))
    tube(b, pts, radii, 6, ["bark2", "trunk2", "bark2"], cap_start=True, cap_end=False)
    top = pts[-1]

    # the living side: everything downwind, and level across the top
    for k in range(5):
        t = rng.uniform(0.35, 1.0)
        at = (wx*sprawl*t*t*0.8, height*t, wz*sprawl*t*t*0.8)
        reach = rng.uniform(0.4, 1.0)*sprawl
        spread = rng.uniform(-0.55, 0.55)
        end = (at[0] + math.cos(wind + spread)*reach, at[1] + rng.uniform(-0.05, 0.22), at[2] + math.sin(wind + spread)*reach)
        tube(b, [at, ((at[0]+end[0])/2, (at[1]+end[1])/2 + 0.05, (at[2]+end[2])/2), end],
             [0.055, 0.045, 0.03], 4, ["bark2", "trunk2"], cap_start=False, cap_end=False)
        needle_mass(b, rng, (end[0], end[1] + 0.10, end[2]), (reach*0.62, reach*0.62, reach*0.62), flat=0.5)
    needle_mass(b, rng, (top[0] + wx*sprawl*0.25, top[1] + 0.05, top[2] + wz*sprawl*0.25),
                (sprawl*0.95, sprawl*0.95, sprawl*0.8), flat=0.34)

    # the weather side: bare, dead, stripped
    for k in range(2):
        t = rng.uniform(0.3, 0.8)
        at = (wx*sprawl*t*t*0.8, height*t, wz*sprawl*t*t*0.8)
        end = (at[0] - wx*rng.uniform(0.3, 0.6), at[1] + rng.uniform(0.05, 0.3), at[2] - wz*rng.uniform(0.3, 0.6))
        tube(b, [at, end], [0.04, 0.018], 4, ["deadwood", "deadwood2"], cap_start=False, cap_end=False)

def plant(index):
    rng = random.Random(4400+index)
    b = Build()
    if index == 0:
        krummholz(b, rng, rng.uniform(1.5, 2.0), rng.uniform(0.9, 1.2), rng.uniform(0, math.tau))
        name = "Krummholz"
    elif index == 1:
        # the other kind: barely a tree at all, a mat of needles over the rock
        wind = rng.uniform(0, math.tau)
        krummholz(b, rng, rng.uniform(0.7, 1.0), rng.uniform(1.2, 1.6), wind)
        name = "Krummholz Mat"
    elif index == 2:
        r = rng.uniform(0.26, 0.38)
        blob(b, (0, r*0.2, 0), (r, r*0.45, r*rng.uniform(0.85, 1.1)), "cushion", "alpineturf2", rng, sub=1, squash=0.15, moss_from=-0.3)
        for _ in range(rng.randint(5, 9)):
            a = rng.uniform(0, math.tau); d = rng.uniform(0, r*0.8)
            px, pz = math.cos(a)*d, math.sin(a)*d
            rr = 0.035
            b.face([(px + math.cos(k/5*math.tau)*rr, r*0.44, pz + math.sin(k/5*math.tau)*rr) for k in range(5)],
                   rng.choice(["alpinebloom", "petal_white", "alpinebloom", "budyellow"]), out=(0,1,0))
        name = "Cushion Plant"
    elif index == 3:
        drift = rng.uniform(0, math.tau)
        for k in range(11):
            a = k/11*math.tau + rng.uniform(-0.3, 0.3)
            h = rng.uniform(0.28, 0.46); w = 0.026
            bend = rng.uniform(0.10, 0.22)
            base = (math.cos(a)*0.05, 0, math.sin(a)*0.05)
            tip = (base[0] + math.cos(drift)*bend, h, base[2] + math.sin(drift)*bend)
            sx, sz = math.cos(a+math.pi/2)*w, math.sin(a+math.pi/2)*w
            col = "alpineturf" if k % 2 else "alpineturf2"
            b.quad((base[0]-sx,base[1],base[2]-sz),(base[0]+sx,base[1],base[2]+sz),
                   (tip[0]+sx*0.25,tip[1],tip[2]+sz*0.25),(tip[0]-sx*0.25,tip[1],tip[2]-sz*0.25), col)
            b.quad((tip[0]-sx*0.25,tip[1],tip[2]-sz*0.25),(tip[0]+sx*0.25,tip[1],tip[2]+sz*0.25),
                   (base[0]+sx,base[1],base[2]+sz),(base[0]-sx,base[1],base[2]-sz), col)
        name = "Mountain Tussock"
    else:
        # an erratic: a block of somewhere else, dropped here and lichened over
        size = (rng.uniform(0.7, 1.0), rng.uniform(0.6, 0.9), rng.uniform(0.7, 1.0))
        blob(b, (0, size[1]*0.55, 0), size, "alpinecrust", "alpine2", rng, sub=1, squash=0.35, moss_from=0.35, patchy=0.4)
        for _ in range(3):
            a = rng.uniform(0, math.tau); d = size[0]*rng.uniform(0.9, 1.4)
            s = rng.uniform(0.10, 0.18)
            blob(b, (math.cos(a)*d, s*0.4, math.sin(a)*d), (s, s*0.5, s*0.8), "alpine2", "alpine3", rng, sub=0, squash=0.3, moss_from=2.0)
        name = "Erratic"
    return b.make(name)

def main():
    scene, cam = ft["setup_scene"]()
    mat = ft["material"](os.path.join(HERE, "TileWorldPalette.png"))
    plants = [plant(i) for i in range(5)]
    for p in plants:
        p.data.materials.append(mat)
        print("PLANT %-18s verts %4d faces %4d height %.2f" % (p.name, len(p.data.vertices), len(p.data.polygons), max(v.co.z for v in p.data.vertices)))
    xs = [-3.4, -1.0, 0.9, 2.2, 3.8]
    for p, x in zip(plants, xs): p.location = (x, 0, 0)
    ft["look"](cam, (0.2, 0.9, 0), 9.5, 12, 0); cam.data.lens = 42
    ft["render"](os.path.join(HERE, "peak-plants.png"))

    pt = {"__file__": os.path.join(HERE, "peak_tiles.py"), "__name__": "peak_tiles"}
    exec(compile(open(os.path.join(HERE, "peak_tiles.py")).read().replace("\nmain()\n", "\n"), "peak_tiles.py", "exec"), pt)
    tiles = [pt["tile"](i) for i in range(5)]
    for t in tiles: t.data.materials.append(mat); t.hide_render = True
    for p in plants: p.hide_render = True
    rng = random.Random(31); placed = []
    for gx in range(-5, 5):
        for gz in range(-5, 5):
            ob = bpy.data.objects.new("P", rng.choice(tiles).data); scene.collection.objects.link(ob)
            lift = rng.choice([0, 0, 0.25, 0.5, -0.25, 0.75])
            ob.location = (gx*2.0+1.0, -(gz*2.0+1.0), lift)
            ob.rotation_euler = (0, 0, rng.choice([0,1,2,3])*math.pi/2)
            placed.append(ob)
            roll = rng.random()
            which = 0 if roll < 0.06 else 1 if roll < 0.13 else 2 if roll < 0.26 else 3 if roll < 0.44 else 4 if roll < 0.50 else -1
            if which >= 0:
                pl = bpy.data.objects.new("T", plants[which].data); scene.collection.objects.link(pl)
                pl.location = (ob.location.x + rng.uniform(-0.5, 0.5), ob.location.y + rng.uniform(-0.5, 0.5), lift + 1.05)
                pl.rotation_euler = (0, 0, rng.uniform(0, math.tau))
                s = rng.uniform(0.8, 1.25); pl.scale = (s, s, s)
                placed.append(pl)
    ft["look"](cam, (0, 2.2, 0), 20, 20, 26); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "peak-ground.png"))
    ft["look"](cam, (0, 1.6, 2), 9.0, 7, 55); cam.data.lens = 42
    ft["render"](os.path.join(HERE, "peak-eye.png"))
    for ob in placed: bpy.data.objects.remove(ob)

    for p in plants:
        p.hide_render = False; p.location = (0,0,0); p.rotation_euler = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT'); p.select_set(True); bpy.context.view_layer.objects.active = p
        bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, p.name + ".fbx"), use_selection=True,
                                 axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL',
                                 mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
