# Savanna tiles for Tile World: dry open grassland, the country between the sand and the woods.
# Straw grass over red earth, tussocks bleached at the tips, cracked bare patches, termite
# workings, the beaten paths animals wear across it, and stones. Five variants on the game's
# terms. Renders previews and exports FBX.
import bpy, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
Build, prism, tube, blob, cylinder_along = ft["Build"], ft["prism"], ft["tube"], ft["blob"], ft["cylinder_along"]
TOP, BOTTOM, HALF, INNER = ft["TOP"], ft["BOTTOM"], ft["HALF"], ft["INNER"]
from mathutils import Vector

GROUND = TOP + 0.04
TONES = ["savgrass", "savgrass2", "savgrass", "savgrass3", "savgrass2"]

def savanna_body(b, rng, bare=0.0, path=None):
    """The block: straw grass over red earth, with the earth showing through where it is worn.
    A path is a band of bare ground straight across, which is what a track looks like from above."""
    n = 5; grid = {}
    o = rng.uniform(0, 30)
    for i in range(n+1):
        for j in range(n+1):
            x = -HALF + 2*HALF*i/n; z = -HALF + 2*HALF*j/n
            edge = i == 0 or j == 0 or i == n or j == n
            y = TOP if edge else TOP + rng.uniform(0.0, 0.05)
            if not edge: x += rng.uniform(-0.06, 0.06); z += rng.uniform(-0.06, 0.06)
            grid[(i,j)] = (x, y, z)
    for i in range(n):
        for j in range(n):
            a, bq, c, d = grid[(i,j)], grid[(i,j+1)], grid[(i+1,j+1)], grid[(i+1,j)]
            for tri in ((a,bq,c),(a,c,d)):
                cx = sum(p[0] for p in tri)/3; cz = sum(p[2] for p in tri)/3
                worn = False
                if path is not None:
                    across = cx*math.cos(path) + cz*math.sin(path)
                    worn = abs(across + math.sin(cz*2.0 + o)*0.16) < 0.26
                if not worn and bare > 0:
                    v = 0.5 + 0.5*math.sin(cx*1.7 + o)*math.cos(cz*1.5 - o)
                    worn = v < bare
                col = rng.choice(["savearth", "savearth", "savearth2", "drysand"]) if worn else rng.choice(TONES)
                b.tri(*tri, col, out=(0,1,0))
    bands = [(TOP, TOP-0.24, "savearth"), (TOP-0.24, 0.1, "savearth2"), (0.1, BOTTOM, "earth2")]
    corners = [(-HALF,-HALF),(HALF,-HALF),(HALF,HALF),(-HALF,HALF)]
    for k in range(4):
        (x0,z0),(x1,z1) = corners[k], corners[(k+1)%4]
        outward = ((x0+x1)*0.5, 0, (z0+z1)*0.5)
        for (y1,y0,col) in bands:
            b.quad((x0,y0,z0),(x1,y0,z1),(x1,y1,z1),(x0,y1,z0), col, out=outward)
    b.quad((-HALF,BOTTOM,-HALF),(-HALF,BOTTOM,HALF),(HALF,BOTTOM,HALF),(HALF,BOTTOM,-HALF), "earth2", out=(0,-1,0))

def tussock(b, rng, at, size=1.0, blades=9):
    """A clump of dry grass: tall, thin, splayed out, bleached at the tips."""
    x, z = at
    for k in range(blades):
        a = k/blades*math.tau + rng.uniform(-0.3, 0.3)
        h = rng.uniform(0.28, 0.52)*size; lean = rng.uniform(0.10, 0.26); w = 0.018*size
        base = (x + math.cos(a)*0.035, GROUND, z + math.sin(a)*0.035)
        tip = (x + math.cos(a)*lean, GROUND + h, z + math.sin(a)*lean)
        sx, sz = math.cos(a+math.pi/2)*w, math.sin(a+math.pi/2)*w
        low = "savgrass2" if k % 2 else "savgrass3"
        b.quad((base[0]-sx,base[1],base[2]-sz),(base[0]+sx,base[1],base[2]+sz),
               (tip[0]+sx*0.2,tip[1],tip[2]+sz*0.2),(tip[0]-sx*0.2,tip[1],tip[2]-sz*0.2), low)
        b.quad((tip[0]-sx*0.2,tip[1],tip[2]-sz*0.2),(tip[0]+sx*0.2,tip[1],tip[2]+sz*0.2),
               (base[0]+sx,base[1],base[2]+sz),(base[0]-sx,base[1],base[2]-sz), "savgrass")

def mound(b, rng, at, size=1.0):
    """A termite mound the small way: a hard red cone out of the ground, weathered into ribs."""
    x, z = at
    h = rng.uniform(0.26, 0.44)*size; r = rng.uniform(0.13, 0.19)*size
    sides = 7
    rings = [[(x + math.cos(k/sides*math.tau)*r*(1 - t*t*0.82)*(1 + math.sin(k*2.3)*0.10),
               GROUND + h*t,
               z + math.sin(k/sides*math.tau)*r*(1 - t*t*0.82)*(1 + math.sin(k*2.3)*0.10)) for k in range(sides)]
             for t in (0.0, 0.35, 0.68, 1.0)]
    for i in range(3):
        for k in range(sides):
            a0 = (k+0.5)/sides*math.tau
            b.quad(rings[i][k], rings[i][(k+1)%sides], rings[i+1][(k+1)%sides], rings[i+1][k],
                   "termite" if (i + k) % 3 else "savearth2", out=(math.cos(a0), 0.2, math.sin(a0)))
    b.face(rings[-1], "termite", out=(0,1,0))

def crack_pan(b, rng, centre, radius):
    """Bare ground gone hard and split, which is what the dry season leaves."""
    n = 8
    ring = [(centre[0]+math.cos(k/n*math.tau)*radius*rng.uniform(0.75,1.1), GROUND + 0.005,
             centre[1]+math.sin(k/n*math.tau)*radius*rng.uniform(0.75,1.1)) for k in range(n)]
    b.face(ring, "drysand", out=(0,1,0))
    for _ in range(4):
        a = rng.uniform(0, math.tau); l = radius*rng.uniform(0.5, 0.95); w = 0.013
        x0 = centre[0] + math.cos(a+math.pi)*l*0.3; z0 = centre[1] + math.sin(a+math.pi)*l*0.3
        pts = [(x0+math.cos(a)*l*t + math.sin(t*7)*0.02, GROUND + 0.010, z0+math.sin(a)*l*t) for t in (0, 0.5, 1)]
        for i in range(2):
            p0, p1 = pts[i], pts[i+1]; sx, sz = math.cos(a+math.pi/2)*w, math.sin(a+math.pi/2)*w
            b.quad((p0[0]-sx,p0[1],p0[2]-sz), (p0[0]+sx,p0[1],p0[2]+sz),
                   (p1[0]+sx,p1[1],p1[2]+sz), (p1[0]-sx,p1[1],p1[2]-sz), "savearth2", out=(0,1,0))

def thorn(b, rng, at, size=1.0):
    """A low thorn bush: bare grey sticks with a little dark leaf caught in them."""
    x, z = at
    for k in range(7):
        a = k/7*math.tau + rng.uniform(-0.4, 0.4)
        h = rng.uniform(0.16, 0.30)*size; lean = rng.uniform(0.10, 0.22)
        pts = [(x, GROUND, z), (x+math.cos(a)*lean*0.5, GROUND+h*0.55, z+math.sin(a)*lean*0.5),
               (x+math.cos(a)*lean, GROUND+h, z+math.sin(a)*lean)]
        tube(b, pts, [0.014, 0.010, 0.006], 3, ["acaciabark", "branch"], cap_start=False)
        if rng.random() < 0.6:
            blob(b, (pts[2][0], pts[2][1] + 0.02, pts[2][2]), (0.055*size, 0.035*size, 0.05*size),
                 "acacia", "acacia3", rng, sub=0, squash=0.5, moss_from=-2)

def bone_bit(b, rng, at):
    """A bone gone white in the sun, which is half of what the savanna leaves lying about."""
    x, z = at; a = rng.uniform(0, math.tau); l = rng.uniform(0.09, 0.16)
    cylinder_along(b, (x-math.cos(a)*l, GROUND+0.028, z-math.sin(a)*l),
                   (x+math.cos(a)*l, GROUND+0.030, z+math.sin(a)*l), 0.026, 5, "bone", "shell", rng=rng, jitter=0.1)

def stones(b, rng, count, keep):
    for _ in range(count):
        for _t in range(20):
            x = rng.uniform(-INNER+0.14, INNER-0.14); z = rng.uniform(-INNER+0.14, INNER-0.14)
            if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
        s = rng.uniform(0.07, 0.14)
        blob(b, (x, GROUND + s*0.3, z), (s, s*0.55, s*0.8), "pebble", "stone2", rng, sub=0, squash=0.3, moss_from=2.0)

def tile(index):
    rng = random.Random(9500+index)
    b = Build(); keep = []
    def spots(n, margin=0.2, room=0.3):
        out = []
        for _ in range(n):
            for _t in range(30):
                x = rng.uniform(-INNER+margin, INNER-margin); z = rng.uniform(-INNER+margin, INNER-margin)
                if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
            out.append((x,z)); keep.append((x,z,room))
        return out

    if index == 0:
        # open grass, which is most of it
        savanna_body(b, rng, bare=0.12)
        for at in spots(5, 0.22, 0.3): tussock(b, rng, at, size=rng.uniform(0.9, 1.3))
        stones(b, rng, 2, keep)
    elif index == 1:
        # worn through to the earth, with the grass hanging on round the edges
        savanna_body(b, rng, bare=0.45)
        crack_pan(b, rng, spots(1, 0.36, 0.5)[0], rng.uniform(0.28, 0.40))
        for at in spots(3, 0.22, 0.28): tussock(b, rng, at, size=rng.uniform(0.8, 1.1))
        bone_bit(b, rng, spots(1, 0.2)[0])
        stones(b, rng, 2, keep)
    elif index == 2:
        # short grass over hard ground, with stones lying in it. The termite mounds used to be
        # on this tile and so stood on a fifth of the country, like traffic cones; they are
        # planted now instead, and rarely.
        savanna_body(b, rng, bare=0.28)
        crack_pan(b, rng, spots(1, 0.34, 0.44)[0], rng.uniform(0.22, 0.32))
        for near in spots(4, 0.22, 0.26): tussock(b, rng, near, size=rng.uniform(0.8, 1.1))
        stones(b, rng, 3, keep)
    elif index == 3:
        # a path worn across it
        savanna_body(b, rng, bare=0.10, path=rng.uniform(0, math.pi))
        for at in spots(4, 0.24, 0.3): tussock(b, rng, at, size=rng.uniform(1.0, 1.4))
        stones(b, rng, 1, keep)
        bone_bit(b, rng, spots(1, 0.2)[0])
    else:
        # thorn scrub taking hold
        savanna_body(b, rng, bare=0.18)
        for at in spots(2, 0.3, 0.38): thorn(b, rng, at, size=rng.uniform(1.0, 1.4))
        for at in spots(3, 0.22, 0.28): tussock(b, rng, at, size=rng.uniform(0.9, 1.2))
        stones(b, rng, 2, keep)
    return b.make("Savanna Tile %d" % index)

def main():
    scene, cam = ft["setup_scene"]()
    mat = ft["material"](os.path.join(HERE, "TileWorldPalette.png"))
    tiles = [tile(i) for i in range(5)]
    for t in tiles:
        t.data.materials.append(mat)
        print("TILE %s verts %d faces %d" % (t.name, len(t.data.vertices), len(t.data.polygons)))
    for i, t in enumerate(tiles): t.location = ((i-2)*2.6, 0, 0)
    ft["look"](cam, (0, 1.0, 0), 11.5, 30, 0); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "savanna-lineup.png"))
    rng = random.Random(41); placed = []
    for t in tiles: t.hide_render = True
    for gx in range(-4, 4):
        for gz in range(-4, 4):
            ob = bpy.data.objects.new("P", rng.choice(tiles).data); scene.collection.objects.link(ob)
            ob.location = (gx*2.0+1.0, -(gz*2.0+1.0), rng.choice([0, 0, 0, 0.25, -0.25]))
            ob.rotation_euler = (0, 0, rng.choice([0,1,2,3])*math.pi/2)
            placed.append(ob)
    ft["look"](cam, (0, 1.2, 0), 15.5, 32, 28); cam.data.lens = 38
    ft["render"](os.path.join(HERE, "savanna-patch.png"))
    for ob in placed: bpy.data.objects.remove(ob)
    for t in tiles:
        t.hide_render = False; t.location = (0,0,0); t.rotation_euler = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT'); t.select_set(True); bpy.context.view_layer.objects.active = t
        bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, t.name + ".fbx"), use_selection=True,
                                 axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL',
                                 mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
