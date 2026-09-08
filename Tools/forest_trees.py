# Forest trees for Tile World, built in Blender from arithmetic like the floor tiles: an oak, a
# beech, a birch and a sapling, each standing on its own foot at the origin, flat shaded, coloured
# by the palette. Renders them alone and on the floor tiles, and exports FBX.
import bpy, math, random, os, sys, importlib.util
HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("forest_tiles", os.path.join(HERE, "forest_tiles.py"))
ft = importlib.util.module_from_spec(spec)
# the tile script runs its main when loaded; hold that off
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
exec(compile(src, "forest_tiles.py", "exec"), ft.__dict__)
Build, tube, blob, prism, uv_of = ft.Build, ft.tube, ft.blob, ft.prism, ft.uv_of
from mathutils import Vector

def canopy_blob(b, rng, centre, radius, top, side, under, sub=1, stretch=1.0):
    """A lump of leaves: lighter on top, darker under, a little pushed about."""
    import bmesh
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=1.0)
    for v in bm.verts:
        v.co.x *= radius*(1+rng.uniform(-0.16,0.16)); v.co.y *= radius*stretch*(1+rng.uniform(-0.12,0.12)); v.co.z *= radius*(1+rng.uniform(-0.16,0.16))
    for f in bm.faces:
        pts=[(centre[0]+v.co.x, centre[1]+v.co.y, centre[2]+v.co.z) for v in f.verts]
        n = f.normal
        col = top if n.y > 0.45 else (under if n.y < -0.35 else side)
        b.face(pts, col)
    bm.free()

def trunk(b, rng, height, base_r, top_r, lean=(0.0,0.0), sides=6, colours=("trunk","trunk","trunk2"), wobble=0.06):
    """A trunk: a tube up from the foot, thinner as it goes, leaning and wobbling a little."""
    segs = 4
    pts=[]; radii=[]
    for s in range(segs+1):
        t = s/segs
        pts.append((lean[0]*height*t*t + math.sin(t*5.0)*wobble*t, height*t, lean[1]*height*t*t + math.cos(t*4.0)*wobble*t))
        radii.append(base_r + (top_r-base_r)*t)
    tube(b, pts, radii, sides, list(colours), cap_start=True, cap_end=True)
    return pts[-1]

def branch(b, rng, start, direction, length, r):
    d = Vector(direction).normalized()
    pts = [tuple(Vector(start) + d*length*t + Vector((0, 0.35*length*t*t, 0))) for t in (0, 0.5, 1)]
    tube(b, pts, [r, r*0.7, r*0.4], 5, ["trunk","trunk2"], cap_start=False)
    return pts[-1]

def oak(index):
    rng = random.Random(500+index); b = Build()
    h = rng.uniform(2.4, 2.9)
    top = trunk(b, rng, h, 0.24, 0.13, lean=(rng.uniform(-0.05,0.05), rng.uniform(-0.05,0.05)))
    ends = []
    for k in range(3):
        a = k/3*math.tau + rng.uniform(-0.4,0.4)
        ends.append(branch(b, rng, (top[0], top[1]-0.35, top[2]), (math.cos(a), 0.5, math.sin(a)), rng.uniform(0.9,1.3), 0.09))
    # the canopy: a big lump over the trunk and one over each branch end
    canopy_blob(b, rng, (top[0], top[1]+0.9, top[2]), rng.uniform(1.35,1.55), "leaf", "leaf2", "leafdark", stretch=0.85)
    for e in ends:
        canopy_blob(b, rng, (e[0]*0.85, e[1]+0.45, e[2]*0.85), rng.uniform(0.95,1.15), "leaf", "leaf2", "leafdark", stretch=0.8)
    return b.make("Oak %d" % index)

def beech(index):
    rng = random.Random(600+index); b = Build()
    h = rng.uniform(3.4, 3.9)
    top = trunk(b, rng, h, 0.2, 0.1, lean=(rng.uniform(-0.04,0.04), rng.uniform(-0.04,0.04)), colours=("trunk2","trunk","trunk2"))
    for k in range(2):
        a = k*math.pi + rng.uniform(-0.6,0.6)
        e = branch(b, rng, (top[0], top[1]-0.9, top[2]), (math.cos(a), 0.7, math.sin(a)), 0.9, 0.07)
        canopy_blob(b, rng, (e[0], e[1]+0.4, e[2]), rng.uniform(0.85,1.0), "leafpale", "leaf", "leafdark", stretch=1.1)
    canopy_blob(b, rng, (top[0], top[1]+0.7, top[2]), rng.uniform(1.1,1.25), "leafpale", "leaf", "leafdark", stretch=1.35)
    canopy_blob(b, rng, (top[0]+rng.uniform(-0.3,0.3), top[1]+1.7, top[2]+rng.uniform(-0.3,0.3)), rng.uniform(0.7,0.85), "leafpale", "leaf", "leafdark", stretch=1.1)
    return b.make("Beech %d" % index)

def birch(index):
    rng = random.Random(700+index); b = Build()
    h = rng.uniform(4.2, 4.8)
    # a pale trunk with dark marks: every third band of the tube is dark
    segs = 6; pts=[]; radii=[]
    lean = (rng.uniform(-0.06,0.06), rng.uniform(-0.06,0.06))
    for s in range(segs+1):
        t = s/segs
        pts.append((lean[0]*h*t*t + math.sin(t*6.0)*0.05*t, h*t, lean[1]*h*t*t))
        radii.append(0.15 + (0.06-0.15)*t)
    # the tube by hand so the bands can differ
    P=[Vector(p) for p in pts]; rings=[]; u=None
    for i in range(len(P)):
        axis=(P[min(i+1,len(P)-1)]-P[max(i-1,0)]).normalized()
        u = axis.cross(Vector((1,0,0))).normalized() if u is None else (u - axis*u.dot(axis)).normalized()
        v = axis.cross(u).normalized()
        rings.append([tuple(P[i] + (u*math.cos(k/6*math.tau)+v*math.sin(k/6*math.tau))*radii[i]) for k in range(6)])
    for i in range(len(P)-1):
        for k in range(6):
            dark = (i + k) % 4 == 0
            b.quad(rings[i][k], rings[i+1][k], rings[i+1][(k+1)%6], rings[i][(k+1)%6], "birchmark" if dark else "birch")
    b.face(rings[0], "birch"); b.face(list(reversed(rings[-1])), "birch")
    top = pts[-1]
    for k in range(3):
        a = k/3*math.tau + rng.uniform(-0.5,0.5)
        e = branch(b, rng, (top[0], top[1]-1.2-k*0.3, top[2]), (math.cos(a), 0.8, math.sin(a)), rng.uniform(0.6,0.9), 0.05)
        canopy_blob(b, rng, (e[0], e[1]+0.35, e[2]), rng.uniform(0.6,0.8), "leafyellow", "leafpale", "leaf2", stretch=1.2)
    canopy_blob(b, rng, (top[0], top[1]+0.5, top[2]), rng.uniform(0.75,0.9), "leafyellow", "leafpale", "leaf2", stretch=1.4)
    return b.make("Birch %d" % index)

def sapling(index):
    rng = random.Random(800+index); b = Build()
    h = rng.uniform(1.3, 1.7)
    top = trunk(b, rng, h, 0.06, 0.03, lean=(rng.uniform(-0.1,0.1), rng.uniform(-0.1,0.1)), sides=5, wobble=0.03)
    canopy_blob(b, rng, (top[0], top[1]+0.35, top[2]), rng.uniform(0.5,0.65), "leaf", "leaf2", "leafdark", stretch=0.9)
    canopy_blob(b, rng, (top[0]+rng.uniform(-0.3,0.3), top[1]+0.05, top[2]+rng.uniform(-0.3,0.3)), rng.uniform(0.35,0.45), "leaf", "leaf2", "leafdark")
    return b.make("Sapling %d" % index)

def main():
    scene, cam = ft.setup_scene()
    mat = ft.material(os.path.join(HERE, "TileWorldPalette.png"))
    trees = [oak(0), oak(1), beech(0), birch(0), birch(1), sapling(0)]
    for t in trees: t.data.materials.append(mat)
    for t in trees: print("TREE %s verts %d faces %d height %.1f" % (t.name, len(t.data.vertices), len(t.data.polygons), max(v.co.z for v in t.data.vertices)))

    # the line-up, each on its foot, tallest in the middle
    xs = [-8.5, -5.2, -1.6, 2.2, 5.4, 8.2]
    for t, x in zip(trees, xs): t.location = (x, 0, 0)
    ft.look(cam, (0, 2.6, 0), 21, 12, 0); cam.data.lens = 40
    ft.render(os.path.join(HERE, "trees-lineup.png"))

    # the wood: the floor tiles on the grid with trees stood on some of them
    tiles = [ft.tile(i) for i in range(5)]
    for t in tiles: t.data.materials.append(mat); t.hide_render = True
    rng = random.Random(11); placed = []
    for gx in range(-4, 4):
        for gz in range(-4, 4):
            src = rng.choice(tiles)
            ob = bpy.data.objects.new("P", src.data); scene.collection.objects.link(ob)
            lift = rng.choice([0, 0, 0, 0.25, -0.25])
            ob.location = (gx*2.0+1.0, -(gz*2.0+1.0), lift); ob.rotation_euler = (0, 0, rng.choice([0,1,2,3])*math.pi/2)
            placed.append(ob)
            if rng.random() < 0.28:
                tree = rng.choice(trees)
                tr = bpy.data.objects.new("T", tree.data); scene.collection.objects.link(tr)
                tr.location = (gx*2.0+1.0+rng.uniform(-0.4,0.4), -(gz*2.0+1.0)+rng.uniform(-0.4,0.4), lift+1.05)
                tr.rotation_euler = (0, 0, rng.uniform(0, math.tau))
                placed.append(tr)
    for t in trees: t.hide_render = True
    ft.look(cam, (0, 2.2, 0), 19, 26, 30); cam.data.lens = 38
    ft.render(os.path.join(HERE, "wood.png"))
    ft.look(cam, (1, 1.6, 1), 9, 9, 55); cam.data.lens = 40
    ft.render(os.path.join(HERE, "wood-low.png"))
    for ob in placed: bpy.data.objects.remove(ob)
    for t in trees: t.hide_render = False

    for t in trees:
        t.location = (0,0,0); t.rotation_euler = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT'); t.select_set(True); bpy.context.view_layer.objects.active = t
        bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, t.name + ".fbx"), use_selection=True, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL', mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
