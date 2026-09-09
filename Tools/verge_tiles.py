# Verge tiles for Tile World: the ground between a beach and whatever grows behind it. Five of
# them, and they are not variants of one another -- they are a series. The first is sand with a
# tuft or two in it and the last is turf with sand showing through, so laying 0 to 4 up the
# shore gives a band that thins from one ground into the other instead of a line.
import bpy, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
Build, prism, tube, blob = ft["Build"], ft["prism"], ft["tube"], ft["blob"]
TOP, BOTTOM, HALF, INNER = ft["TOP"], ft["BOTTOM"], ft["HALF"], ft["INNER"]
from mathutils import Vector

GROUND = TOP + 0.04
SAND = ["sand1", "sand2", "sand3", "sand1"]
EARTH = ["humus", "humus2", "jfloor", "turf_dark2"]

def verge_body(b, rng, green):
    """The block: sand and dark earth mixed on the top by a noise field rather than by the
    tile, so the patches run over the edge of one tile and into the next and the join does not
    show. Sand under the rim whatever the mix, since that is what the shore is made of."""
    n = 5; grid = {}
    o = rng.uniform(0, 50)
    for i in range(n+1):
        for j in range(n+1):
            x = -HALF + 2*HALF*i/n; z = -HALF + 2*HALF*j/n
            edge = i == 0 or j == 0 or i == n or j == n
            y = TOP if edge else TOP + rng.uniform(0.0, 0.05)
            if not edge: x += rng.uniform(-0.05, 0.05); z += rng.uniform(-0.05, 0.05)
            grid[(i,j)] = (x, y, z)
    def turfy(cx, cz):
        # One low wave crossed with another, so a tile carries one or two patches rather than
        # a rash of them. A tile is instanced all over the world and cannot know where it is,
        # so the field cannot run on into its neighbour: what it can do is be big enough that
        # the eye reads patches of ground and not a pattern.
        v = 0.5 + 0.5*math.sin(cx*1.35 + o) * math.cos(cz*1.15 - o*0.8)
        v = v*0.88 + rng.random()*0.12
        return v < green
    for i in range(n):
        for j in range(n):
            a, bq, c, d = grid[(i,j)], grid[(i,j+1)], grid[(i+1,j+1)], grid[(i+1,j)]
            for tri in ((a,bq,c),(a,c,d)):
                cx = sum(p[0] for p in tri)/3; cz = sum(p[2] for p in tri)/3
                col = rng.choice(EARTH) if turfy(cx, cz) else rng.choice(SAND)
                b.tri(*tri, col, out=(0,1,0))
    bands = [(TOP, TOP-0.26, "sand2"), (TOP-0.26, 0.1, "sanddark"), (0.1, BOTTOM, "earth2")]
    corners = [(-HALF,-HALF),(HALF,-HALF),(HALF,HALF),(-HALF,HALF)]
    for k in range(4):
        (x0,z0),(x1,z1) = corners[k], corners[(k+1)%4]
        outward = ((x0+x1)*0.5, 0, (z0+z1)*0.5)
        for (y1,y0,col) in bands:
            b.quad((x0,y0,z0),(x1,y0,z1),(x1,y1,z1),(x0,y1,z0), col, out=outward)
    b.quad((-HALF,BOTTOM,-HALF),(-HALF,BOTTOM,HALF),(HALF,BOTTOM,HALF),(HALF,BOTTOM,-HALF), "earth2", out=(0,-1,0))
    return turfy

def tuft(b, rng, at, size=1.0, dry=False):
    x, z = at
    for k in range(6):
        a = k/6*math.tau + rng.uniform(-0.35, 0.35)
        h = rng.uniform(0.16, 0.30)*size; lean = rng.uniform(0.05, 0.14); w = 0.022*size
        base = (x + math.cos(a)*0.03, GROUND, z + math.sin(a)*0.03)
        tip = (x + math.cos(a)*lean, GROUND + h, z + math.sin(a)*lean)
        sx, sz = math.cos(a+math.pi/2)*w, math.sin(a+math.pi/2)*w
        col = ("marram" if k % 2 else "marram2") if dry else ("turf_dark1" if k % 2 else "grassdark")
        b.quad((base[0]-sx,base[1],base[2]-sz),(base[0]+sx,base[1],base[2]+sz),
               (tip[0]+sx*0.25,tip[1],tip[2]+sz*0.25),(tip[0]-sx*0.25,tip[1],tip[2]-sz*0.25), col)
        b.quad((tip[0]-sx*0.25,tip[1],tip[2]-sz*0.25),(tip[0]+sx*0.25,tip[1],tip[2]+sz*0.25),
               (base[0]+sx,base[1],base[2]+sz),(base[0]-sx,base[1],base[2]-sz), col)

def shell_bit(b, rng, at):
    x, z = at; a = rng.uniform(0, math.tau); s = rng.uniform(0.05, 0.085)
    col = rng.choice(["shell", "shellpink"])
    hinge = (x - math.cos(a)*s*0.6, GROUND + s*0.35, z - math.sin(a)*s*0.6)
    rim = [(x + math.cos(a + (k-2)*0.5)*s, GROUND, z + math.sin(a + (k-2)*0.5)*s) for k in range(5)]
    for k in range(4): b.tri(hinge, rim[k], rim[k+1], col, out=(0,1,0))

def leafy(b, rng, at):
    x, z = at; a = rng.uniform(0, math.tau); s = rng.uniform(0.09, 0.15)
    col = rng.choice(["litter2", "humus2", "leafyellow", "moss2"])
    ring = [(x + math.cos(a + k/5*math.tau)*s*0.8, GROUND + (0.02 if k == 0 else 0.0), z + math.sin(a + k/5*math.tau)*s*0.8) for k in range(5)]
    b.face(ring, col, out=(0,1,0))

def stone(b, rng, at, s):
    blob(b, (at[0], GROUND + s*0.3, at[1]), (s, s*0.55, s*0.8), "pebble", "stone2", rng, sub=0, squash=0.3, moss_from=2.0)

def tile(index):
    """0 is nearly all sand, 4 is nearly all turf. The things standing on them follow: shells
    and dune grass at the sand end, leaf litter and darker tufts at the other."""
    green = [0.10, 0.30, 0.52, 0.74, 0.92][index]
    rng = random.Random(12000 + index)
    b = Build(); verge_body(b, rng, green)
    keep = []
    def spots(n, margin=0.18, room=0.26):
        out = []
        for _ in range(n):
            for _t in range(30):
                x = rng.uniform(-INNER+margin, INNER-margin); z = rng.uniform(-INNER+margin, INNER-margin)
                if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
            out.append((x,z)); keep.append((x,z,room))
        return out
    for at in spots(int(1 + green*6)):
        tuft(b, rng, at, size=0.85 + green*0.5, dry=rng.random() > green)
    for at in spots(max(0, int((1 - green) * 4))): shell_bit(b, rng, at)
    for at in spots(int(green * 4)): leafy(b, rng, at)
    for at in spots(2): stone(b, rng, at, rng.uniform(0.055, 0.10))
    return b.make("Verge Tile %d" % index)

def main():
    scene, cam = ft["setup_scene"]()
    mat = ft["material"](os.path.join(HERE, "TileWorldPalette.png"))
    tiles = [tile(i) for i in range(5)]
    for t in tiles:
        t.data.materials.append(mat)
        print("TILE %s verts %d faces %d" % (t.name, len(t.data.vertices), len(t.data.polygons)))
    for i, t in enumerate(tiles): t.location = ((i-2)*2.6, 0, 0)
    ft["look"](cam, (0, 1.0, 0), 11.5, 32, 0); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "verge-lineup.png"))

    # the band as it will be laid: the five in order across the shore
    for t in tiles: t.hide_render = True
    rng = random.Random(5); placed = []
    for gz in range(-6, 6):
        for gx in range(-9, 9):
            u = (gx + 9) / 18.0
            src = tiles[min(4, max(0, int(u * 5)))]
            ob = bpy.data.objects.new("P", src.data); scene.collection.objects.link(ob)
            ob.location = (gx*2.0+1.0, -(gz*2.0+1.0), 0)
            ob.rotation_euler = (0, 0, rng.choice([0,1,2,3])*math.pi/2)
            placed.append(ob)
    ft["look"](cam, (0, 1.2, 0), 24, 28, 8); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "verge-band.png"))
    for ob in placed: bpy.data.objects.remove(ob)

    for t in tiles:
        t.hide_render = False; t.location = (0,0,0); t.rotation_euler = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT'); t.select_set(True); bpy.context.view_layer.objects.active = t
        bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, t.name + ".fbx"), use_selection=True,
                                 axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL',
                                 mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
