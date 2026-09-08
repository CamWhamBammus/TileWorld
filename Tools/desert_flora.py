# Cacti and palms for Tile World, built in Blender like the trees: three cacti (a saguaro with arms,
# a barrel, a prickly pear) and three palms (tall, leaning, short), each on its foot at the origin,
# flat shaded, coloured by the palette. Renders them alone and on the sand, and exports FBX.
import bpy, bmesh, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
Build, tube, blob = ft["Build"], ft["tube"], ft["blob"]
from mathutils import Vector

def ribbed(b, pts, radii, ribs, colours, cap_end=True):
    """A ribbed column along a path: every other vertex of each ring pulled in, so the sides read as ridges."""
    P=[Vector(p) for p in pts]; rings=[]; u=None
    for i in range(len(P)):
        axis=(P[min(i+1,len(P)-1)]-P[max(i-1,0)]).normalized()
        if u is None:
            ref = Vector((1,0,0)) if abs(axis.x) < 0.9 else Vector((0,0,1))
            u = axis.cross(ref).normalized()
        else: u=(u-axis*u.dot(axis)).normalized()
        v=axis.cross(u).normalized()
        rings.append([tuple(P[i]+(u*math.cos(k/ribs*math.tau)+v*math.sin(k/ribs*math.tau))*radii[i]*(1.0 if k%2==0 else 0.84)) for k in range(ribs)])
    for i in range(len(P)-1):
        for k in range(ribs):
            mid=(Vector(rings[i][k])+Vector(rings[i+1][(k+1)%ribs]))*0.5
            b.quad(rings[i][k], rings[i+1][k], rings[i+1][(k+1)%ribs], rings[i][(k+1)%ribs], colours[k%2], out=tuple(mid-(P[i]+P[i+1])*0.5))
    b.face(rings[0], colours[1], out=tuple(P[0]-P[1]))
    if cap_end: b.face(rings[-1], colours[0], out=tuple(P[-1]-P[-2]))

def saguaro(index):
    rng = random.Random(900+index); b = Build()
    h = rng.uniform(3.2, 4.2)
    pts=[(math.sin(t*3)*0.04, h*t, 0) for t in (0, 0.25, 0.5, 0.75, 0.93, 1.0)]
    radii=[0.28, 0.3, 0.29, 0.27, 0.22, 0.12]
    ribbed(b, pts, radii, 10, ["cactus","cactusdark"])
    # a dome on top
    blob(b, (0, h, 0), (0.14, 0.12, 0.14), "cactuslight", "cactus", rng, sub=1, squash=0.2, moss_from=0.6)
    arms = 1 + rng.randint(0, 1)
    for k in range(arms):
        a = (k * math.pi + 0.5 + rng.uniform(-0.4,0.4)); y0 = h*rng.uniform(0.35, 0.55)
        out = Vector((math.cos(a), 0, math.sin(a)))
        armpts = [tuple(Vector((0,y0,0)) + out*0.2), tuple(Vector((0,y0+0.1,0)) + out*0.55), tuple(Vector((0,y0+0.55,0)) + out*0.72), tuple(Vector((0,y0+1.2+rng.uniform(0,0.6),0)) + out*0.74)]
        ribbed(b, armpts, [0.18, 0.19, 0.18, 0.13], 8, ["cactus","cactusdark"])
        blob(b, armpts[-1], (0.1, 0.09, 0.1), "cactuslight", "cactus", rng, sub=1, squash=0.2, moss_from=0.6)
    return b.make("Saguaro %d" % index)

def barrel(index):
    rng = random.Random(920+index); b = Build()
    h = rng.uniform(0.7, 0.95); r = rng.uniform(0.38, 0.48)
    pts=[(0, 0, 0), (0, h*0.5, 0), (0, h, 0)]
    ribbed(b, pts, [r*0.8, r, r*0.7], 12, ["cactus","cactusdark"], cap_end=True)
    # a ring of buds on the crown
    for k in range(5):
        a = k/5*math.tau + 0.3
        blob(b, (math.cos(a)*r*0.4, h+0.04, math.sin(a)*r*0.4), (0.06, 0.06, 0.06), "bud" if index % 2 else "budyellow", "bud" if index % 2 else "budyellow", rng, sub=0, squash=0.7, moss_from=-1)
    return b.make("Barrel Cactus %d" % index)

def pad(b, rng, base, direction, size, tilt):
    """One prickly pear pad: a flattened ellipsoid stood on its edge."""
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0)
    d = Vector(direction).normalized(); side = d.cross(Vector((0,1,0))).normalized()
    for v in bm.verts:
        x, y, z = v.co.x*size*0.75, v.co.y*size, v.co.z*size*0.16
        v.co = base + d*(x) + Vector((0,1,0))*(y + size*0.95) * 1.0 + side*z
        # lean the pad over
        v.co = base + (v.co - base).lerp(base + Vector((0,1,0))*size, 0) if False else v.co
    for f in bm.faces:
        pts=[tuple(v.co) for v in f.verts]
        b.face(pts, "cactuslight" if f.normal.y > 0.5 else ("cactus" if f.normal.dot(side) > 0 else "cactusdark"))
    bm.free()
    return base + d*size*0.6 + Vector((0,1,0))*size*1.7

def prickly(index):
    rng = random.Random(940+index); b = Build()
    tips = [Vector((0,0,0))]
    for k in range(3):
        a = k/3*math.tau + rng.uniform(-0.5,0.5)
        top = pad(b, rng, Vector((0,0,0)), (math.cos(a), 0, math.sin(a)), rng.uniform(0.32,0.4), 0)
        for j in range(rng.randint(1,2)):
            a2 = a + rng.uniform(-0.8,0.8)
            top = pad(b, rng, top - Vector((0,1,0))*0.1, (math.cos(a2), 0, math.sin(a2)), rng.uniform(0.24,0.32), 0)
            if rng.random() < 0.5: blob(b, tuple(top + Vector((0,0.02,0))), (0.05,0.06,0.05), "bud", "bud", rng, sub=0, squash=0.8, moss_from=-1)
    return b.make("Prickly Pear %d" % index)

def frond(b, rng, base, direction, length, droop):
    """A palm frond: a spine of quads that rises then droops, wider at the middle."""
    d = Vector(direction).normalized(); side = d.cross(Vector((0,1,0))).normalized()
    segs = 5; prev = None
    for s in range(segs+1):
        t = s/segs
        p = base + d*length*t + Vector((0,1,0))*(length*(0.45*t - droop*t*t))
        w = 0.16*length*math.sin(t*math.pi)**0.7 + 0.02
        cur = (tuple(p - side*w), tuple(p + side*w))
        if prev is not None:
            col = "palmleaf" if s % 2 else "palmleaf2"
            b.quad(prev[0], prev[1], cur[1], cur[0], col, out=(0,1,0))
            b.quad(cur[0], cur[1], prev[1], prev[0], col, out=(0,-1,0))
        prev = cur

def palm(index):
    rng = random.Random(960+index); b = Build()
    h = [6.2, 5.0, 3.6][index % 3]; lean = [0.05, 0.22, 0.1][index % 3]
    a = rng.uniform(0, math.tau)
    pts=[]; radii=[]
    segs = 8
    for s in range(segs+1):
        t = s/segs
        pts.append((math.cos(a)*lean*h*t*t + math.sin(t*9)*0.03, h*t, math.sin(a)*lean*h*t*t))
        radii.append(0.2 - 0.09*t + (0.03 if s % 2 else 0))     # the rings of a palm's trunk
    tube(b, pts, radii, 7, ["palmtrunk","palmtrunk","palmtrunk2"])
    top = Vector(pts[-1])
    n = rng.randint(8, 10)
    for k in range(n):
        fa = k/n*math.tau + rng.uniform(-0.2,0.2)
        frond(b, rng, top + Vector((0,0.1,0)), (math.cos(fa), 0, math.sin(fa)), rng.uniform(1.9, 2.6) * (0.75 if index % 3 == 2 else 1.0), rng.uniform(0.45, 0.7))
    for k in range(3):
        ca = k/3*math.tau + 0.4
        blob(b, tuple(top + Vector((math.cos(ca)*0.22, -0.12, math.sin(ca)*0.22))), (0.11,0.13,0.11), "coconut", "coconut", rng, sub=0, squash=0.9, moss_from=-1)
    return b.make("Palm %d" % index)

def main():
    scene, cam = ft["setup_scene"]()
    mat = ft["material"](os.path.join(HERE, "TileWorldPalette.png"))
    plants = [saguaro(0), saguaro(1), barrel(0), barrel(1), prickly(0), palm(0), palm(1), palm(2)]
    for p in plants: p.data.materials.append(mat); print("PLANT %s verts %d faces %d height %.1f" % (p.name, len(p.data.vertices), len(p.data.polygons), max(v.co.z for v in p.data.vertices)))
    xs = [-9.5, -7.2, -5.2, -3.6, -1.6, 1.4, 4.6, 7.8]
    for p, x in zip(plants, xs): p.location = (x, 0, 0)
    ft["look"](cam, (0, 2.4, 0), 22, 12, 0); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "desert-lineup.png"))
    # on the sand: the desert tiles with cacti, the beach tiles with palms
    for p in plants: p.hide_render = True
    ssrc = open(os.path.join(HERE, "sand_tiles.py")).read().replace("\nmain()\n", "\n")
    st = {"__file__": os.path.join(HERE, "sand_tiles.py"), "__name__": "sand_tiles"}; exec(compile(ssrc, "sand_tiles.py", "exec"), st)
    beaches = [st["beach"](i) for i in range(5)]; deserts = [st["desert"](i) for i in range(5)]
    for t in beaches + deserts: t.data.materials.append(mat); t.hide_render = True
    rng = random.Random(21); placed = []
    for gx in range(-5, 5):
        for gz in range(-5, 5):
            desert_side = gz >= 0
            src = rng.choice(deserts if desert_side else beaches)
            lift = rng.choice([0,0,0,0.25]) if desert_side else 0
            ob = bpy.data.objects.new("P", src.data); scene.collection.objects.link(ob)
            ob.location = (gx*2.0+1.0, -(gz*2.0+1.0), lift); ob.rotation_euler = (0,0,rng.choice([0,1,2,3])*math.pi/2); placed.append(ob)
            r = rng.random()
            pick = None
            if desert_side and r < 0.16: pick = rng.choice(plants[:5])
            elif not desert_side and r < 0.12: pick = rng.choice(plants[5:])
            if pick is not None:
                tr = bpy.data.objects.new("T", pick.data); scene.collection.objects.link(tr)
                tr.location = (gx*2.0+1.0+rng.uniform(-0.4,0.4), -(gz*2.0+1.0)+rng.uniform(-0.4,0.4), lift+1.05); tr.rotation_euler = (0,0,rng.uniform(0,math.tau)); placed.append(tr)
    ft["look"](cam, (0, 2.0, 0), 22, 26, 30); cam.data.lens = 38
    ft["render"](os.path.join(HERE, "desert-scene.png"))
    ft["look"](cam, (1, 1.6, 3), 9, 10, 50); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "desert-low.png"))
    ft["look"](cam, (1, 1.6, -4), 9, 10, 130); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "beach-low.png"))
    for ob in placed: bpy.data.objects.remove(ob)
    for p in plants:
        p.hide_render = False; p.location = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT'); p.select_set(True); bpy.context.view_layer.objects.active = p
        bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, p.name + ".fbx"), use_selection=True, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL', mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
