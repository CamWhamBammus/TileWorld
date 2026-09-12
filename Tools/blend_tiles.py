# Mixed ground for Tile World: the tiles that go where two countries meet.
#
# A tile for every pair of countries would be a square number of them and most would never be
# seen. But the fourteen grounds in the world are only five things to look at -- sand, grass,
# dark floor, rock and snow -- and a join between two countries is a join between two of those.
# Five families make ten pairs, and each pair gets a series of five graded from one to the other,
# so fifty tiles cover every border in the world.
#
# Each series runs 0 (nearly all of the first family) to 4 (nearly all of the second). The chunk
# picks by which side of the border the tile is on and how near the line, so the two countries
# meet in the middle of the series instead of meeting each other.
import bpy, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
Build, prism, tube, blob, cylinder_along = ft["Build"], ft["prism"], ft["tube"], ft["blob"], ft["cylinder_along"]
TOP, BOTTOM, HALF, INNER = ft["TOP"], ft["BOTTOM"], ft["HALF"], ft["INNER"]
from mathutils import Vector

GROUND = TOP + 0.04

# ---------------------------------------------------------------- what each family looks like
def tufts(b, rng, at, cols, size=1.0, blades=6, high=(0.16, 0.30)):
    x, z = at
    for k in range(blades):
        a = k/blades*math.tau + rng.uniform(-0.35, 0.35)
        h = rng.uniform(*high)*size; lean = rng.uniform(0.05, 0.15); w = 0.022*size
        base = (x + math.cos(a)*0.03, GROUND, z + math.sin(a)*0.03)
        tip = (x + math.cos(a)*lean, GROUND + h, z + math.sin(a)*lean)
        sx, sz = math.cos(a+math.pi/2)*w, math.sin(a+math.pi/2)*w
        col = cols[k % len(cols)]
        b.quad((base[0]-sx,base[1],base[2]-sz),(base[0]+sx,base[1],base[2]+sz),
               (tip[0]+sx*0.25,tip[1],tip[2]+sz*0.25),(tip[0]-sx*0.25,tip[1],tip[2]-sz*0.25), col)
        b.quad((tip[0]-sx*0.25,tip[1],tip[2]-sz*0.25),(tip[0]+sx*0.25,tip[1],tip[2]+sz*0.25),
               (base[0]+sx,base[1],base[2]+sz),(base[0]-sx,base[1],base[2]-sz), col)

def flat_bit(b, rng, at, cols, small=0.09, big=0.16):
    x, z = at; a = rng.uniform(0, math.tau); s = rng.uniform(small, big)
    ring = [(x + math.cos(a + k/5*math.tau)*s*0.85, GROUND + (0.02 if k == 0 else 0.004), z + math.sin(a + k/5*math.tau)*s*0.85) for k in range(5)]
    b.face(ring, rng.choice(cols), out=(0,1,0))

def lump(b, rng, at, cols, small=0.06, big=0.12, squash=0.35):
    s = rng.uniform(small, big)
    blob(b, (at[0], GROUND + s*0.32, at[1]), (s, s*0.55, s*0.85), cols[0], cols[-1], rng, sub=0, squash=squash, moss_from=2.0)

def shell_bit(b, rng, at):
    x, z = at; a = rng.uniform(0, math.tau); s = rng.uniform(0.05, 0.085)
    col = rng.choice(["shell", "shellpink"])
    hinge = (x - math.cos(a)*s*0.6, GROUND + s*0.35, z - math.sin(a)*s*0.6)
    rim = [(x + math.cos(a + (k-2)*0.5)*s, GROUND, z + math.sin(a + (k-2)*0.5)*s) for k in range(5)]
    for k in range(4): b.tri(hinge, rim[k], rim[k+1], col, out=(0,1,0))

def slab_bit(b, rng, at, size=0.16):
    x, z = at; n = 5; a0 = rng.uniform(0, math.tau)
    lean = (rng.uniform(-0.12, 0.12), rng.uniform(-0.12, 0.12))
    rim = []
    for k in range(n):
        a = a0 + k/n*math.tau; r = size*rng.uniform(0.7, 1.2)
        px, pz = math.cos(a)*r, math.sin(a)*r
        rim.append((x + px, GROUND + 0.02 + px*lean[0] + pz*lean[1], z + pz))
    low = [(p[0], p[1] - size*0.35, p[2]) for p in rim]
    b.face(rim, "alpine" if rng.random() < 0.6 else "alpinecrust", out=(0,1,0))
    for k in range(n):
        a = a0 + (k+0.5)/n*math.tau
        b.quad(rim[k], rim[(k+1)%n], low[(k+1)%n], low[k], "alpine2" if k % 2 else "alpine3", out=(math.cos(a), 0, math.sin(a)))

FAMILY = {
    "sand": dict(
        top=["sand1", "sand2", "sand3", "sand1"], band="sand2", low="sanddark",
        strew=lambda b, rng, at: shell_bit(b, rng, at) if rng.random() < 0.45 else
                                 (tufts(b, rng, at, ["marram", "marram2"], size=0.9, blades=5) if rng.random() < 0.5
                                  else lump(b, rng, at, ["pebble", "stone2"]))),
    "grass": dict(
        top=["turf_light1", "turf_light2", "turf_light3", "turf_dark1"], band="humus", low="earth2",
        strew=lambda b, rng, at: tufts(b, rng, at, ["tuft_light", "turf_dark2"], size=1.0, blades=6) if rng.random() < 0.7
                                 else flat_bit(b, rng, at, ["petal_white", "petal_yellow", "flower_heart"], 0.05, 0.08)),
    "dark": dict(
        top=["jfloor", "jfloor2", "humus", "humus2"], band="jfloor2", low="earth2",
        strew=lambda b, rng, at: flat_bit(b, rng, at, ["litter2", "moss", "moss2", "cocoa"]) if rng.random() < 0.6
                                 else tufts(b, rng, at, ["fern2", "grassdark"], size=0.9, blades=5)),
    "rock": dict(
        top=["alpine", "alpine2", "rock3", "gravel", "alpine"], band="alpine2", low="rockdark",
        strew=lambda b, rng, at: slab_bit(b, rng, at, rng.uniform(0.11, 0.19)) if rng.random() < 0.55
                                 else (flat_bit(b, rng, at, ["lichen", "lichen2", "goldlichen", "alpinecrust"])
                                       if rng.random() < 0.5 else lump(b, rng, at, ["alpine2", "alpine3"]))),
    "snow": dict(
        top=["snow1", "snow1", "snow2", "snow3"], band="snowshade", low="frostrock2",
        strew=lambda b, rng, at: lump(b, rng, at, ["snow1", "snow2"], 0.08, 0.16, squash=0.2) if rng.random() < 0.6
                                 else flat_bit(b, rng, at, ["ice", "ice2", "snow3"])),
    # Dry grass is its own family, not a shade of grass. Straw over red earth meeting green
    # meadow is as big a change as meadow meeting sand, and while the plain counted as grass
    # the two of them met along a hard line with no mixed ground between them at all.
    "dry": dict(
        top=["savgrass", "savgrass2", "savgrass3", "savearth"], band="savearth", low="savearth2",
        strew=lambda b, rng, at: tufts(b, rng, at, ["savgrass", "savgrass2"], size=1.1, blades=7, high=(0.20, 0.38))
                                 if rng.random() < 0.7 else flat_bit(b, rng, at, ["drysand", "savearth2", "bone"])),
}
ORDER = ["sand", "grass", "dark", "rock", "snow", "dry"]
PAIRS = [(a, b) for i, a in enumerate(ORDER) for b in ORDER[i+1:]]

# ---------------------------------------------------------------- the tile
def blend_body(b, rng, first, second, mix):
    """The block: the two grounds mixed on the top by a low field, so a tile carries one or two
    patches of the other rather than a rash of them. The sides take whichever is winning, so a
    step in the ground does not show a stripe of the wrong country."""
    A, B = FAMILY[first], FAMILY[second]
    n = 5; grid = {}
    o = rng.uniform(0, 40)
    for i in range(n+1):
        for j in range(n+1):
            x = -HALF + 2*HALF*i/n; z = -HALF + 2*HALF*j/n
            edge = i == 0 or j == 0 or i == n or j == n
            y = TOP if edge else TOP + rng.uniform(0.0, 0.06)
            if not edge: x += rng.uniform(-0.055, 0.055); z += rng.uniform(-0.055, 0.055)
            grid[(i,j)] = (x, y, z)
    def theirs(cx, cz):
        v = 0.5 + 0.5*math.sin(cx*1.35 + o) * math.cos(cz*1.15 - o*0.8)
        return (v*0.88 + rng.random()*0.12) < mix
    for i in range(n):
        for j in range(n):
            a, bq, c, d = grid[(i,j)], grid[(i,j+1)], grid[(i+1,j+1)], grid[(i+1,j)]
            for tri in ((a,bq,c),(a,c,d)):
                cx = sum(p[0] for p in tri)/3; cz = sum(p[2] for p in tri)/3
                use = B if theirs(cx, cz) else A
                b.tri(*tri, rng.choice(use["top"]), out=(0,1,0))
    win = B if mix > 0.5 else A
    bands = [(TOP, TOP-0.26, win["band"]), (TOP-0.26, 0.1, win["low"]), (0.1, BOTTOM, "earth2")]
    corners = [(-HALF,-HALF),(HALF,-HALF),(HALF,HALF),(-HALF,HALF)]
    for k in range(4):
        (x0,z0),(x1,z1) = corners[k], corners[(k+1)%4]
        outward = ((x0+x1)*0.5, 0, (z0+z1)*0.5)
        for (y1,y0,col) in bands:
            b.quad((x0,y0,z0),(x1,y0,z1),(x1,y1,z1),(x0,y1,z0), col, out=outward)
    b.quad((-HALF,BOTTOM,-HALF),(-HALF,BOTTOM,HALF),(HALF,BOTTOM,HALF),(HALF,BOTTOM,-HALF), "earth2", out=(0,-1,0))

def tile(first, second, step):
    mix = [0.10, 0.30, 0.50, 0.70, 0.90][step]
    rng = random.Random(hash((first, second, step)) & 0xffff)
    b = Build(); blend_body(b, rng, first, second, mix)
    keep = []
    def spots(n, margin=0.18, room=0.26):
        out = []
        for _ in range(n):
            for _t in range(30):
                x = rng.uniform(-INNER+margin, INNER-margin); z = rng.uniform(-INNER+margin, INNER-margin)
                if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
            out.append((x,z)); keep.append((x,z,room))
        return out
    # what is strewn follows the mix, so the far end of a series carries the other country's litter
    for at in spots(max(0, round((1 - mix) * 6))): FAMILY[first]["strew"](b, rng, at)
    for at in spots(max(0, round(mix * 6))): FAMILY[second]["strew"](b, rng, at)
    return b.make("Blend %s %s %d" % (first.capitalize(), second.capitalize(), step))

def main():
    scene, cam = ft["setup_scene"]()
    mat = ft["material"](os.path.join(HERE, "TileWorldPalette.png"))
    made = []
    for first, second in PAIRS:
        row = [tile(first, second, k) for k in range(5)]
        for t in row: t.data.materials.append(mat)
        made.append(((first, second), row))
        print("PAIR %-6s %-6s faces %s" % (first, second, [len(t.data.polygons) for t in row]))

    # every series, one row per pair, so the whole set can be read at once
    for _, row in made:
        for t in row: t.hide_render = True
    rng = random.Random(2); placed = []
    for r, (_, row) in enumerate(made):
        for c in range(5):
            for j in range(2):
                ob = bpy.data.objects.new("P", row[c].data); scene.collection.objects.link(ob)
                ob.location = ((c*2 + j)*2.0, -r*2.4*2.0, 0)
                ob.rotation_euler = (0, 0, rng.choice([0,1,2,3])*math.pi/2)
                placed.append(ob)
    ft["look"](cam, (9.0, 1.0, 22.0), 44, 52, 0); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "blend-all.png"))
    for ob in placed: bpy.data.objects.remove(ob)

    for (first, second), row in made:
        for t in row:
            t.hide_render = False; t.location = (0,0,0); t.rotation_euler = (0,0,0)
            bpy.ops.object.select_all(action='DESELECT'); t.select_set(True); bpy.context.view_layer.objects.active = t
            bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, t.name + ".fbx"), use_selection=True,
                                     axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL',
                                     mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE %d tiles" % (len(made) * 5))

main()
