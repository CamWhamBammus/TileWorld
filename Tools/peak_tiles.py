# Peak tiles for Tile World: the ground of the high country, which until now borrowed the
# lowland's pale grass. Frost-shattered slabs, rock heaved up out of thin turf, snow lying in
# the lee, cushion plants and lichen, an erratic left behind. Five variants built in Blender on
# the game's terms. Renders previews and exports FBX.
import bpy, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
Build, prism, tube, blob, cylinder_along = ft["Build"], ft["prism"], ft["tube"], ft["blob"], ft["cylinder_along"]
TOP, BOTTOM, HALF, INNER = ft["TOP"], ft["BOTTOM"], ft["HALF"], ft["INNER"]
from mathutils import Vector

GROUND = TOP + 0.04
STONE = ["alpine", "alpine2", "alpine", "alpine3", "alpine2"]

def peak_body(b, rng, tones, snow=None, turf=0.0):
    """The block: bare rock on top, in plates rather than a smooth sheet, with turf worked into
    it if asked and snow lying where it is asked for. Cold grey sides all the way down."""
    n = 4; grid = {}
    for i in range(n+1):
        for j in range(n+1):
            x = -HALF + 2*HALF*i/n; z = -HALF + 2*HALF*j/n
            edge = i==0 or j==0 or i==n or j==n
            y = TOP if edge else TOP + rng.uniform(0.0, 0.10)
            if not edge: x += rng.uniform(-0.09,0.09); z += rng.uniform(-0.09,0.09)
            grid[(i,j)] = (x,y,z)
    for i in range(n):
        for j in range(n):
            a,bq,c,d = grid[(i,j)], grid[(i,j+1)], grid[(i+1,j+1)], grid[(i+1,j)]
            for tri in ((a,bq,c),(a,c,d)):
                cx = sum(p[0] for p in tri)/3; cz = sum(p[2] for p in tri)/3
                under = snow is not None and (cx-snow[0])**2 + (cz-snow[1])**2 < snow[2]**2
                if under: col = rng.choice(["snow1","snow1","snow2","snow3"])
                elif rng.random() < turf: col = rng.choice(["alpineturf","alpineturf2","alpineturf"])
                elif rng.random() < 0.10: col = "rustrock"          # iron in the rock, here and there
                else: col = rng.choice(tones)
                b.tri(*tri, col, out=(0,1,0))
    bands = [(TOP, TOP-0.30, "alpine2"), (TOP-0.30, 0.1, "alpine3"), (0.1, BOTTOM, "rockdark")]
    corners = [(-HALF,-HALF),(HALF,-HALF),(HALF,HALF),(-HALF,HALF)]
    for k in range(4):
        (x0,z0),(x1,z1) = corners[k], corners[(k+1)%4]
        outward = ((x0+x1)*0.5, 0, (z0+z1)*0.5)
        for (y1,y0,col) in bands:
            b.quad((x0,y0,z0),(x1,y0,z1),(x1,y1,z1),(x0,y1,z0), col, out=outward)
    b.quad((-HALF,BOTTOM,-HALF),(-HALF,BOTTOM,HALF),(HALF,BOTTOM,HALF),(HALF,BOTTOM,-HALF), "rockdark", out=(0,-1,0))

def slab(b, rng, at, size, lift, tilt=0.06):
    """A plate of rock frost-split off the bedrock: flat, thin, and tipped a little."""
    x, z = at; n = 6
    a0 = rng.uniform(0, math.tau)
    lean = (rng.uniform(-tilt, tilt), rng.uniform(-tilt, tilt))
    rim = []
    for k in range(n):
        a = a0 + k/n*math.tau
        r = size*rng.uniform(0.75, 1.15)
        px, pz = math.cos(a)*r, math.sin(a)*r
        rim.append((x + px, GROUND + lift + px*lean[0] + pz*lean[1], z + pz))
    thick = size*rng.uniform(0.10, 0.18)
    low = [(p[0], p[1] - thick, p[2]) for p in rim]
    top = "alpine" if rng.random() < 0.6 else "alpinecrust"
    b.face(rim, top, out=(0,1,0))
    for k in range(n):
        a = a0 + (k+0.5)/n*math.tau
        b.quad(rim[k], rim[(k+1)%n], low[(k+1)%n], low[k], "alpine2" if k % 2 else "alpine3",
               out=(math.cos(a), 0, math.sin(a)))

def crack(b, rng, at, length, angle):
    """A split in the rock, dark and thin, with ice down in it."""
    x, z = at
    w = 0.028
    segs = 3
    for si in range(segs):
        t0 = si/segs; t1 = (si+1)/segs
        wob = math.sin((si + 1)*2.1)*0.05
        p0 = (x + math.cos(angle)*length*t0, GROUND + 0.004, z + math.sin(angle)*length*t0 + wob)
        p1 = (x + math.cos(angle)*length*t1, GROUND + 0.004, z + math.sin(angle)*length*t1 + wob*0.5)
        sx, sz = math.cos(angle+math.pi/2)*w, math.sin(angle+math.pi/2)*w
        b.quad((p0[0]-sx,p0[1],p0[2]-sz), (p0[0]+sx,p0[1],p0[2]+sz),
               (p1[0]+sx,p1[1],p1[2]+sz), (p1[0]-sx,p1[1],p1[2]-sz),
               "ice" if rng.random() < 0.35 else "alpine3", out=(0,1,0))

def lichen_crust(b, rng, at, radius, colour=None):
    """Lichen on the rock: a flat ragged crust, pale green or a hot yellow."""
    n = 8
    ring = [(at[0] + math.cos(k/n*math.tau)*radius*rng.uniform(0.45, 1.25), GROUND + 0.005,
             at[1] + math.sin(k/n*math.tau)*radius*rng.uniform(0.45, 1.25)) for k in range(n)]
    b.face(ring, colour or rng.choice(["alpinecrust", "goldlichen", "lichen", "lichen2"]), out=(0,1,0))

def tussock(b, rng, at, size=1.0):
    """A tuft of mountain grass: short, wiry, bent by the wind, dry olive rather than green."""
    x, z = at; drift = rng.uniform(0, math.tau)
    for k in range(7):
        a = k/7*math.tau + rng.uniform(-0.3, 0.3)
        h = rng.uniform(0.14, 0.26)*size; w = 0.020*size
        bend = rng.uniform(0.05, 0.13)
        base = (x + math.cos(a)*0.03, GROUND, z + math.sin(a)*0.03)
        tip = (base[0] + math.cos(drift)*bend, GROUND + h, base[2] + math.sin(drift)*bend)
        sx, sz = math.cos(a+math.pi/2)*w, math.sin(a+math.pi/2)*w
        col = "alpineturf" if k % 2 else "alpineturf2"
        b.quad((base[0]-sx,base[1],base[2]-sz),(base[0]+sx,base[1],base[2]+sz),
               (tip[0]+sx*0.25,tip[1],tip[2]+sz*0.25),(tip[0]-sx*0.25,tip[1],tip[2]-sz*0.25), col)
        b.quad((tip[0]-sx*0.25,tip[1],tip[2]-sz*0.25),(tip[0]+sx*0.25,tip[1],tip[2]+sz*0.25),
               (base[0]+sx,base[1],base[2]+sz),(base[0]-sx,base[1],base[2]-sz), col)

def cushion(b, rng, at, radius, flowers=True):
    """A cushion plant: a tight low dome that grows where nothing else will, sometimes in flower."""
    x, z = at
    blob(b, (x, GROUND + radius*0.18, z), (radius, radius*0.42, radius*rng.uniform(0.8, 1.05)),
         "cushion", "alpineturf2", rng, sub=1, squash=0.15, moss_from=-0.3)
    if not flowers: return
    for _ in range(rng.randint(2, 5)):
        a = rng.uniform(0, math.tau); d = rng.uniform(0, radius*0.75)
        px, pz = x + math.cos(a)*d, z + math.sin(a)*d
        r = 0.026
        b.face([(px + math.cos(k/5*math.tau)*r, GROUND + radius*0.42, pz + math.sin(k/5*math.tau)*r) for k in range(5)],
               rng.choice(["alpinebloom", "petal_white", "alpinebloom"]), out=(0,1,0))

def shards(b, rng, count, keep):
    """Frost-split chips lying where they fell."""
    for _ in range(count):
        for _t in range(20):
            x = rng.uniform(-INNER+0.12, INNER-0.12); z = rng.uniform(-INNER+0.12, INNER-0.12)
            if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
        s = rng.uniform(0.07, 0.15)
        if rng.random() < 0.5:
            slab(b, rng, (x, z), s, 0.0, tilt=0.18)
        else:
            blob(b, (x, GROUND + s*0.3, z), (s, s*0.45, s*0.8), "alpine2", "alpine3", rng, sub=0, squash=0.3, moss_from=2.0)

def erratic(b, rng, at, size):
    """A boulder left behind by ice: angular, lichened on the weather side."""
    blob(b, (at[0], GROUND + size[1]*0.5, at[1]), size, "alpinecrust", "alpine2", rng, sub=1, squash=0.30, moss_from=0.45, patchy=0.45)

def snow_lie(b, rng, centre, radius):
    """Snow that has not gone: a low sheet in the lee of something, its edge ragged."""
    n = 9
    ring = [(centre[0] + math.cos(k/n*math.tau)*radius*rng.uniform(0.6, 1.15), GROUND + 0.03,
             centre[1] + math.sin(k/n*math.tau)*radius*rng.uniform(0.6, 1.15)) for k in range(n)]
    hub = (centre[0], GROUND + 0.10, centre[1])
    for k in range(n):
        b.tri(hub, ring[k], ring[(k+1)%n], "snow1" if k % 3 else "snow2", out=(0,1,0))

def tile(index):
    rng = random.Random(8000+index)
    b = Build(); keep = []
    def spots(n, margin=0.2, room=0.32):
        out = []
        for _ in range(n):
            for _t in range(30):
                x = rng.uniform(-INNER+margin, INNER-margin); z = rng.uniform(-INNER+margin, INNER-margin)
                if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
            out.append((x,z)); keep.append((x,z,room))
        return out

    if index == 0:
        # bedrock split into plates by the frost
        peak_body(b, rng, STONE)
        for at in spots(3, 0.32, 0.44): slab(b, rng, at, rng.uniform(0.28, 0.42), 0.02)
        for k in range(3): crack(b, rng, (rng.uniform(-0.7, 0.7), rng.uniform(-0.7, 0.7)), rng.uniform(0.5, 0.9), rng.uniform(0, math.tau))
        for at in spots(3, 0.15, 0.2): lichen_crust(b, rng, at, rng.uniform(0.12, 0.22))
        shards(b, rng, 4, keep)
    elif index == 1:
        # rock heaved up through thin turf
        peak_body(b, rng, STONE, turf=0.45)
        for at in spots(2, 0.3, 0.4): slab(b, rng, at, rng.uniform(0.22, 0.34), 0.03, tilt=0.14)
        for at in spots(3, 0.22, 0.26): tussock(b, rng, at, size=rng.uniform(0.9, 1.3))
        for at in spots(2, 0.2, 0.22): cushion(b, rng, at, rng.uniform(0.13, 0.20))
        for at in spots(2, 0.15, 0.18): lichen_crust(b, rng, at, rng.uniform(0.10, 0.18))
        shards(b, rng, 3, keep)
    elif index == 2:
        # snow that stays in the lee, and ice down in the cracks
        s = (rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), 0.58)
        peak_body(b, rng, STONE, snow=s)
        snow_lie(b, rng, (s[0], s[1]), 0.52); keep.append((s[0], s[1], 0.66))
        for k in range(3): crack(b, rng, (rng.uniform(-0.8, 0.8), rng.uniform(-0.8, 0.8)), rng.uniform(0.4, 0.8), rng.uniform(0, math.tau))
        for at in spots(2, 0.2, 0.24): lichen_crust(b, rng, at, rng.uniform(0.10, 0.18))
        shards(b, rng, 4, keep)
    elif index == 3:
        # the turf itself, where a little soil has gathered
        peak_body(b, rng, STONE, turf=0.80)
        for at in spots(4, 0.22, 0.3): tussock(b, rng, at, size=rng.uniform(1.0, 1.4))
        for at in spots(3, 0.2, 0.24): cushion(b, rng, at, rng.uniform(0.15, 0.24))
        for at in spots(1, 0.3, 0.34): slab(b, rng, at, rng.uniform(0.2, 0.3), 0.02, tilt=0.16)
        shards(b, rng, 2, keep)
    else:
        # an erratic, and the scatter round its foot
        peak_body(b, rng, STONE, turf=0.20)
        at = spots(1, 0.42, 0.66)[0]
        erratic(b, rng, at, (rng.uniform(0.34, 0.46), rng.uniform(0.30, 0.42), rng.uniform(0.32, 0.44)))
        for near in spots(2, 0.2, 0.2): lichen_crust(b, rng, near, rng.uniform(0.12, 0.2))
        for near in spots(2, 0.22, 0.24): tussock(b, rng, near, size=0.9)
        cushion(b, rng, spots(1, 0.2)[0], 0.15)
        shards(b, rng, 5, keep)
    return b.make("Peak Tile %d" % index)

def main():
    scene, cam = ft["setup_scene"]()
    mat = ft["material"](os.path.join(HERE, "TileWorldPalette.png"))
    tiles = [tile(i) for i in range(5)]
    for t in tiles:
        t.data.materials.append(mat)
        print("TILE %s verts %d faces %d" % (t.name, len(t.data.vertices), len(t.data.polygons)))
    for i, t in enumerate(tiles): t.location = ((i-2)*2.6, 0, 0)
    ft["look"](cam, (0, 1.0, 0), 11.5, 30, 0); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "peak-lineup.png"))
    for i, t in enumerate(tiles):
        for o in tiles: o.hide_render = (o is not t)
        ft["look"](cam, (t.location.x, 1.2, 0), 4.4, 26, 35); cam.data.lens = 45
        ft["render"](os.path.join(HERE, "peak-tile%d.png" % i))
    for o in tiles: o.hide_render = False
    rng = random.Random(23); placed = []
    for gx in range(-4, 4):
        for gz in range(-4, 4):
            ob = bpy.data.objects.new("P", rng.choice(tiles).data); scene.collection.objects.link(ob)
            ob.location = (gx*2.0+1.0, -(gz*2.0+1.0), rng.choice([0, 0, 0.25, 0.5, -0.25]))
            ob.rotation_euler = (0, 0, rng.choice([0,1,2,3])*math.pi/2)
            placed.append(ob)
    for t in tiles: t.hide_render = True
    ft["look"](cam, (0, 1.4, 0), 15.5, 34, 28); cam.data.lens = 38
    ft["render"](os.path.join(HERE, "peak-patch.png"))
    ft["look"](cam, (0, 1.5, 1), 7.0, 12, 55); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "peak-low.png"))
    for ob in placed: bpy.data.objects.remove(ob)
    for t in tiles:
        t.hide_render = False; t.location = (0,0,0); t.rotation_euler = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT'); t.select_set(True); bpy.context.view_layer.objects.active = t
        bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, t.name + ".fbx"), use_selection=True,
                                 axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL',
                                 mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
