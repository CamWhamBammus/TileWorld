# Jungle floor tiles for Tile World: the dark wet ground under a closed canopy -- rotting leaf
# litter, buttress roots, a mossy fallen trunk, ferns and broad leaves, standing water. Five
# variants built in Blender on the game's terms, like the forest floor. Renders previews and
# exports FBX.
import bpy, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
Build, prism, tube, blob, cylinder_along = ft["Build"], ft["prism"], ft["tube"], ft["blob"], ft["cylinder_along"]
TOP, BOTTOM, HALF, INNER = ft["TOP"], ft["BOTTOM"], ft["HALF"], ft["INNER"]
from mathutils import Vector

GROUND = TOP + 0.05

def jungle_body(b, rng, puddle=None):
    """The block: a floor of dark rotted leaf, an earth side, standing water sunk into it if asked."""
    n = 4; grid = {}
    for i in range(n+1):
        for j in range(n+1):
            x = -HALF + 2*HALF*i/n; z = -HALF + 2*HALF*j/n
            edge = i==0 or j==0 or i==n or j==n
            y = TOP if edge else TOP + rng.uniform(0.0, 0.07)
            if not edge: x += rng.uniform(-0.07,0.07); z += rng.uniform(-0.07,0.07)
            if puddle is not None and not edge and (x-puddle[0])**2 + (z-puddle[1])**2 < puddle[2]**2: y = TOP - 0.04
            grid[(i,j)] = (x,y,z)
    for i in range(n):
        for j in range(n):
            a,bq,c,d = grid[(i,j)], grid[(i,j+1)], grid[(i+1,j+1)], grid[(i+1,j)]
            for tri in ((a,bq,c),(a,c,d)):
                cx = sum(p[0] for p in tri)/3; cz = sum(p[2] for p in tri)/3
                wet = puddle is not None and (cx-puddle[0])**2 + (cz-puddle[1])**2 < (puddle[2]*0.85)**2
                col = rng.choice(["algae2","puddle2","algae"]) if wet else rng.choice(["jfloor","jfloor","jfloor2","jfloor2","humus","humus2"])
                b.tri(*tri, col, out=(0,1,0))
    bands = [(TOP, TOP-0.18, "jfloor2"), (TOP-0.18, 0.1, "earth"), (0.1, BOTTOM, "earth2")]
    corners = [(-HALF,-HALF),(HALF,-HALF),(HALF,HALF),(-HALF,HALF)]
    for k in range(4):
        (x0,z0),(x1,z1) = corners[k], corners[(k+1)%4]
        outward = ((x0+x1)*0.5, 0, (z0+z1)*0.5)
        for (y1,y0,col) in bands:
            b.quad((x0,y0,z0),(x1,y0,z1),(x1,y1,z1),(x0,y1,z0), col, out=outward)
    b.quad((-HALF,BOTTOM,-HALF),(-HALF,BOTTOM,HALF),(HALF,BOTTOM,HALF),(HALF,BOTTOM,-HALF), "earth2", out=(0,-1,0))

def big_leaf(b, rng, at, size=1.0, colour=None):
    """A fallen jungle leaf: broad, ribbed, one end lifted. Bigger than anything a wood drops."""
    x, z = at; a = rng.uniform(0, math.tau)
    col = colour or rng.choice(["frond", "jungle2", "leafyellow", "litter2"])
    L = 0.30*size; W = 0.13*size
    tip = (x + math.cos(a)*L, GROUND + 0.05*size, z + math.sin(a)*L)
    tail = (x - math.cos(a)*L*0.55, GROUND, z - math.sin(a)*L*0.55)
    for side in (1, -1):
        mid = (x + math.cos(a)*L*0.25 + math.cos(a + math.pi/2)*W*side,
               GROUND + 0.02*size,
               z + math.sin(a)*L*0.25 + math.sin(a + math.pi/2)*W*side)
        b.tri(tail, mid, tip, col, out=(0,1,0))
        b.tri(tail, tip, mid, "jungle3", out=(0,-1,0))

def frond(b, rng, at, size=1.0, blades=6, tall=False):
    """A fern or a young palm: long ribbed blades out of one crown, drooping at the ends."""
    x, z = at
    stem = 0.16*size if tall else 0.0
    if tall: prism(b, (x, GROUND, z), 0.035*size, stem, 5, "liana", "liana", taper=0.8)
    for k in range(blades):
        a = k/blades*math.tau + rng.uniform(-0.25, 0.25)
        L = rng.uniform(0.34, 0.52)*size; w = 0.055*size
        segs = 3
        pts = []
        for si in range(segs+1):
            t = si/segs
            pts.append((x + math.cos(a)*L*t,
                        GROUND + stem + L*(0.62*math.sin(t*2.2) - 0.30*t*t),
                        z + math.sin(a)*L*t))
        col = "jungle1" if k % 2 else "jungle2"
        for si in range(segs):
            p0, p1 = pts[si], pts[si+1]
            t0 = 1 - si/segs*0.55; t1 = 1 - (si+1)/segs*0.7
            sx, sz = math.cos(a+math.pi/2)*w, math.sin(a+math.pi/2)*w
            q = [(p0[0]-sx*t0,p0[1],p0[2]-sz*t0), (p0[0]+sx*t0,p0[1],p0[2]+sz*t0),
                 (p1[0]+sx*t1,p1[1],p1[2]+sz*t1), (p1[0]-sx*t1,p1[1],p1[2]-sz*t1)]
            b.quad(*q, col); b.quad(q[3], q[2], q[1], q[0], "jungle3")

def buttress(b, rng, at, angle, length, height):
    """A buttress root: a fin of wood standing out of the floor, thick at the tree and thin at its end."""
    x, z = at
    ax, az = math.cos(angle), math.sin(angle)
    px, pz = math.cos(angle+math.pi/2), math.sin(angle+math.pi/2)
    steps = 4
    prev = None
    for si in range(steps+1):
        t = si/steps
        w = 0.085*(1 - t*0.85); h = height*(1 - t)**1.15
        cx, cz = x + ax*length*t, z + az*length*t
        ring = [(cx + px*w, GROUND - 0.05, cz + pz*w), (cx - px*w, GROUND - 0.05, cz - pz*w),
                (cx - px*w*0.5, GROUND + h, cz - pz*w*0.5), (cx + px*w*0.5, GROUND + h, cz + pz*w*0.5)]
        if prev is not None:
            for k in range(4):
                a0, b0 = prev[k], prev[(k+1)%4]
                a1, b1 = ring[k], ring[(k+1)%4]
                mid = Vector((a0[0]+b0[0]+a1[0]+b1[0], a0[1]+b0[1]+a1[1]+b1[1], a0[2]+b0[2]+a1[2]+b1[2]))/4
                axis = Vector((cx, mid.y, cz))
                b.quad(a0, b0, b1, a1, "moss2" if k == 2 else rng.choice(["liana", "bark2"]), out=tuple(mid - axis))
        prev = ring
    b.face(list(reversed(prev)), "liana", out=(ax, 0, az))

def rotten_log(b, rng, p0, p1, radius):
    """A trunk down and going back into the ground: dark wood under moss, with fungus on it."""
    cylinder_along(b, (p0[0], GROUND + radius*0.7, p0[1]), (p1[0], GROUND + radius*0.75, p1[1]),
                   radius, 8, "bark2", "liana", rng=rng, jitter=0.12, taper=0.92)
    steps = 8
    for si in range(steps):
        t = (si + 0.5)/steps
        cx = p0[0] + (p1[0]-p0[0])*t; cz = p0[1] + (p1[1]-p0[1])*t
        blob(b, (cx + rng.uniform(-0.06,0.06), GROUND + radius*1.35, cz + rng.uniform(-0.06,0.06)),
             (radius*rng.uniform(0.7,1.05), radius*0.42, radius*rng.uniform(0.7,1.05)),
             "moss", "jungle3", rng, sub=0, squash=0.2, moss_from=-0.4)
    for _ in range(3):
        t = rng.uniform(0.15, 0.85)
        cx = p0[0] + (p1[0]-p0[0])*t; cz = p0[1] + (p1[1]-p0[1])*t
        a = rng.uniform(0, math.tau); r = rng.uniform(0.06, 0.10)
        shelf = [(cx + math.cos(a + (k-2)*0.55)*r*(1.4 if k in (1,2,3) else 0.7),
                  GROUND + radius*rng.uniform(0.9, 1.2),
                  cz + math.sin(a + (k-2)*0.55)*r*(1.4 if k in (1,2,3) else 0.7)) for k in range(5)]
        b.face(shelf, "capcream", out=(0,1,0))
        b.face(list(reversed(shelf)), "cocoa", out=(0,-1,0))

def bloom(b, rng, at, size=1.0):
    """A flower off the floor: a short stalk with a ring of bright petals."""
    x, z = at
    h = rng.uniform(0.12, 0.22)*size
    prism(b, (x, GROUND, z), 0.014*size, h, 4, "jungle2", "jungle2", taper=0.8)
    col = rng.choice(["bloom", "bloom2", "petal_white", "budyellow"])
    r = 0.075*size
    hub = (x, GROUND + h + 0.015, z)
    for k in range(6):
        a0 = k/6*math.tau; a1 = (k+1)/6*math.tau
        b.tri(hub, (x + math.cos(a0)*r, GROUND + h, z + math.sin(a0)*r),
              (x + math.cos(a1)*r, GROUND + h, z + math.sin(a1)*r), col, out=(0,1,0))
    b.face([(x + math.cos(k/5*math.tau)*r*0.3, GROUND + h + 0.03, z + math.sin(k/5*math.tau)*r*0.3) for k in range(5)], "budyellow", out=(0,1,0))

def sprout(b, rng, at, size=1.0):
    """A seedling reaching for the light: two or three broad leaves on a thin stalk."""
    x, z = at
    h = rng.uniform(0.18, 0.30)*size
    prism(b, (x, GROUND, z), 0.016*size, h, 4, "jungle2", "jungle2", taper=0.7)
    for k in range(3):
        a = k/3*math.tau + rng.uniform(-0.3, 0.3)
        L = rng.uniform(0.14, 0.22)*size; w = 0.06*size
        base = (x, GROUND + h*rng.uniform(0.6, 1.0), z)
        tip = (x + math.cos(a)*L, base[1] + L*0.35, z + math.sin(a)*L)
        sx, sz = math.cos(a+math.pi/2)*w, math.sin(a+math.pi/2)*w
        q = [(base[0]-sx*0.3,base[1],base[2]-sz*0.3), (base[0]+sx*0.3,base[1],base[2]+sz*0.3),
             (tip[0]+sx,tip[1],tip[2]+sz), (tip[0]-sx,tip[1],tip[2]-sz)]
        b.quad(*q, "jungle1"); b.quad(q[3], q[2], q[1], q[0], "jungle3")

def stone_mossy(b, rng, at, size):
    blob(b, (at[0], GROUND + size[1]*0.45, at[1]), size, "moss", "wetstone", rng, sub=1, squash=0.35, moss_from=0.35, patchy=0.3)

def moss_bed(b, rng, count, keep):
    """Moss over the floor: flat mottled patches, which is most of what a jungle floor is."""
    for _ in range(count):
        x = rng.uniform(-INNER+0.15, INNER-0.15); z = rng.uniform(-INNER+0.15, INNER-0.15)
        n = 7; r = rng.uniform(0.22, 0.40)
        ring = [(x + math.cos(k/n*math.tau)*r*rng.uniform(0.6,1.2), GROUND + 0.006,
                 z + math.sin(k/n*math.tau)*r*rng.uniform(0.6,1.2)) for k in range(n)]
        b.face(ring, rng.choice(["moss", "moss2", "jungle3"]), out=(0,1,0))

def litter(b, rng, count, keep):
    for _ in range(count):
        for _t in range(20):
            x = rng.uniform(-INNER+0.1, INNER-0.1); z = rng.uniform(-INNER+0.1, INNER-0.1)
            if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
        if rng.random() < 0.55:
            big_leaf(b, rng, (x, z), size=rng.uniform(0.6, 1.0))
        else:
            a = rng.uniform(0, math.tau); s = rng.uniform(0.10, 0.17)
            col = rng.choice(["litter2", "humus2", "cocoa", "jungle3"])
            ring = [(x + math.cos(a + k/5*math.tau)*s*0.8, GROUND + (0.03 if k == 0 else 0.0), z + math.sin(a + k/5*math.tau)*s*0.8) for k in range(5)]
            b.face(ring, col, out=(0,1,0))

def tile(index):
    rng = random.Random(6000+index)
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
        # the litter floor: what a closed canopy drops, and the seedlings under it
        jungle_body(b, rng)
        for at in spots(2, 0.3, 0.4): frond(b, rng, at, size=rng.uniform(0.85, 1.1))
        for at in spots(2, 0.2): sprout(b, rng, at, size=rng.uniform(0.9, 1.2))
        moss_bed(b, rng, 3, keep); litter(b, rng, 30, keep)
    elif index == 1:
        # buttress roots, spread from a trunk standing off the tile
        jungle_body(b, rng)
        base = spots(1, 0.5, 0.3)[0]
        for k in range(4):
            a = k/4*math.tau + rng.uniform(-0.4, 0.4)
            buttress(b, rng, base, a, rng.uniform(0.65, 0.95), rng.uniform(0.45, 0.68))
        keep.append((base[0], base[1], 0.8))
        for at in spots(2, 0.25): frond(b, rng, at, size=0.8)
        moss_bed(b, rng, 2, keep); litter(b, rng, 24, keep)
    elif index == 2:
        # a fallen trunk gone soft, with fungus and moss over it
        a = rng.uniform(0, math.tau)
        jungle_body(b, rng)
        rotten_log(b, rng, (-0.75*math.cos(a), -0.75*math.sin(a)), (0.75*math.cos(a), 0.75*math.sin(a)), 0.19)
        keep.append((0, 0, 0.5))
        for at in spots(2, 0.3): frond(b, rng, at, size=rng.uniform(0.8, 1.0))
        bloom(b, rng, spots(1, 0.2)[0], size=1.1)
        moss_bed(b, rng, 2, keep); litter(b, rng, 22, keep)
    elif index == 3:
        # the thick of it: undergrowth over most of the tile
        jungle_body(b, rng)
        for at in spots(4, 0.28, 0.36): frond(b, rng, at, size=rng.uniform(0.95, 1.3), blades=rng.choice([6, 7]), tall=rng.random() < 0.4)
        for at in spots(2, 0.2): bloom(b, rng, at, size=rng.uniform(0.9, 1.2))
        moss_bed(b, rng, 2, keep); litter(b, rng, 20, keep)
    else:
        # standing water, which a jungle floor is half made of
        p = (rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25), 0.55)
        jungle_body(b, rng, puddle=p); keep.append((p[0], p[1], 0.62))
        for at in spots(2, 0.3, 0.34): frond(b, rng, at, size=rng.uniform(0.85, 1.05))
        stone_mossy(b, rng, spots(1, 0.3)[0], (rng.uniform(0.16, 0.24), 0.14, rng.uniform(0.14, 0.2)))
        for at in spots(1, 0.2): sprout(b, rng, at)
        moss_bed(b, rng, 3, keep); litter(b, rng, 20, keep)
    return b.make("Jungle Tile %d" % index)

def main():
    scene, cam = ft["setup_scene"]()
    mat = ft["material"](os.path.join(HERE, "TileWorldPalette.png"))
    tiles = [tile(i) for i in range(5)]
    for t in tiles:
        t.data.materials.append(mat)
        print("TILE %s verts %d faces %d" % (t.name, len(t.data.vertices), len(t.data.polygons)))
    for i, t in enumerate(tiles): t.location = ((i-2)*2.6, 0, 0)
    ft["look"](cam, (0, 1.0, 0), 11.5, 30, 0); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "jungle-lineup.png"))
    for i, t in enumerate(tiles):
        for o in tiles: o.hide_render = (o is not t)
        ft["look"](cam, (t.location.x, 1.2, 0), 4.4, 26, 35); cam.data.lens = 45
        ft["render"](os.path.join(HERE, "jungle-tile%d.png" % i))
    for o in tiles: o.hide_render = False
    rng = random.Random(3); placed = []
    for gx in range(-4, 4):
        for gz in range(-4, 4):
            ob = bpy.data.objects.new("P", rng.choice(tiles).data); scene.collection.objects.link(ob)
            ob.location = (gx*2.0+1.0, -(gz*2.0+1.0), rng.choice([0, 0, 0, 0.25, -0.25]))
            ob.rotation_euler = (0, 0, rng.choice([0,1,2,3])*math.pi/2)
            placed.append(ob)
    for t in tiles: t.hide_render = True
    ft["look"](cam, (0, 1.2, 0), 15.5, 34, 28); cam.data.lens = 38
    ft["render"](os.path.join(HERE, "jungle-patch.png"))
    ft["look"](cam, (0, 1.4, 1), 7.0, 12, 55); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "jungle-low.png"))
    for ob in placed: bpy.data.objects.remove(ob)
    for t in tiles:
        t.hide_render = False; t.location = (0,0,0); t.rotation_euler = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT'); t.select_set(True); bpy.context.view_layer.objects.active = t
        bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, t.name + ".fbx"), use_selection=True,
                                 axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL',
                                 mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
