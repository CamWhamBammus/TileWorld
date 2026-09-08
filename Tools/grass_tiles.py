# Grass tiles for Tile World, built in Blender like the forest floor: five meadows in each of the
# three shades the country uses by height (pale on the high ground, light, dark in the low), on
# the game's terms: 2.0 m on the 2 m grid, a body from -1.00 to a top at 1.05, flat shaded,
# coloured by the palette. Renders previews and exports FBX.
import bpy, math, random, os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
Build, prism, tube, blob, pebbles, tuft, twig = ft["Build"], ft["prism"], ft["tube"], ft["blob"], ft["pebbles"], ft["tuft"], ft["twig"]
TOP, BOTTOM, HALF, INNER = ft["TOP"], ft["BOTTOM"], ft["HALF"], ft["INNER"]
from mathutils import Vector

SHADES = ["pale", "light", "dark"]

def turf_body(b, rng, shade):
    """The block: earth sides under a band of turf, a top of grass facets in the shade's three greens."""
    n = 4; grid = {}
    for i in range(n+1):
        for j in range(n+1):
            x = -HALF + 2*HALF*i/n; z = -HALF + 2*HALF*j/n
            edge = i==0 or j==0 or i==n or j==n
            y = TOP if edge else TOP + rng.uniform(0.0, 0.06)
            if not edge: x += rng.uniform(-0.06,0.06); z += rng.uniform(-0.06,0.06)
            grid[(i,j)] = (x,y,z)
    greens = ["turf_%s1" % shade, "turf_%s1" % shade, "turf_%s2" % shade, "turf_%s3" % shade]
    for i in range(n):
        for j in range(n):
            a,bq,c,d = grid[(i,j)], grid[(i,j+1)], grid[(i+1,j+1)], grid[(i+1,j)]
            b.tri(a,bq,c,rng.choice(greens),out=(0,1,0)); b.tri(a,c,d,rng.choice(greens),out=(0,1,0))
    bands = [(TOP, TOP-0.16, "turf_%s2" % shade), (TOP-0.16, 0.1, "earth"), (0.1, BOTTOM, "earth2")]
    corners = [(-HALF,-HALF),(HALF,-HALF),(HALF,HALF),(-HALF,HALF)]
    for k in range(4):
        (x0,z0),(x1,z1) = corners[k], corners[(k+1)%4]
        outward = ((x0+x1)*0.5, 0, (z0+z1)*0.5)
        for (y1,y0,col) in bands:
            b.quad((x0,y0,z0),(x1,y0,z1),(x1,y1,z1),(x0,y1,z0), col, out=outward)
    b.quad((-HALF,BOTTOM,-HALF),(-HALF,BOTTOM,HALF),(HALF,BOTTOM,HALF),(HALF,BOTTOM,-HALF), "earth2", out=(0,-1,0))

def grass_tuft(b, rng, at, shade, size=1.0):
    """A tuft of meadow grass: seven blades in the shade's own green, bending outward."""
    x,z = at
    col = "tuft_%s" % shade
    for k in range(7):
        a = k/7*math.tau + rng.uniform(-0.3,0.3)
        h = rng.uniform(0.2, 0.32)*size; lean = rng.uniform(0.1, 0.2); w = 0.03*size
        base = (x + math.cos(a)*0.035, TOP+0.05, z + math.sin(a)*0.035)
        tip = (x + math.cos(a)*lean, TOP+0.05+h, z + math.sin(a)*lean)
        sx, sz = math.cos(a+math.pi/2)*w, math.sin(a+math.pi/2)*w
        b.quad((base[0]-sx,base[1],base[2]-sz),(base[0]+sx,base[1],base[2]+sz),(tip[0]+sx*0.25,tip[1],tip[2]+sz*0.25),(tip[0]-sx*0.25,tip[1],tip[2]-sz*0.25), col)
        b.quad((tip[0]-sx*0.25,tip[1],tip[2]-sz*0.25),(tip[0]+sx*0.25,tip[1],tip[2]+sz*0.25),(base[0]+sx,base[1],base[2]+sz),(base[0]-sx,base[1],base[2]-sz), col)

def flower(b, rng, at, petal, shade):
    """A flower: a thin stem, five petals round a heart, all facing up."""
    x,z = at
    h = rng.uniform(0.16, 0.26)
    tube(b, [(x, TOP+0.05, z), (x+rng.uniform(-0.03,0.03), TOP+0.05+h, z+rng.uniform(-0.03,0.03))], [0.012, 0.009], 3, ["tuft_%s" % shade], cap_start=False, cap_end=False)
    cx, cy, cz = x, TOP+0.05+h, z
    r = rng.uniform(0.05, 0.075)
    for k in range(5):
        a0 = k/5*math.tau; a1 = (k+0.5)/5*math.tau; a2 = (k+1)/5*math.tau
        b.face([(cx,cy,cz), (cx+math.cos(a0)*r*0.5, cy, cz+math.sin(a0)*r*0.5), (cx+math.cos(a1)*r, cy+0.01, cz+math.sin(a1)*r), (cx+math.cos(a2)*r*0.5, cy, cz+math.sin(a2)*r*0.5)], petal, out=(0,1,0))
    b.face([(cx+math.cos(k/5*math.tau)*r*0.28, cy+0.012, cz+math.sin(k/5*math.tau)*r*0.28) for k in range(5)], "flower_heart", out=(0,1,0))

def flowers(b, rng, centre, radius, petals, count, shade):
    for _ in range(count):
        a = rng.uniform(0, math.tau); d = rng.uniform(0, radius)
        flower(b, rng, (centre[0]+math.cos(a)*d, centre[1]+math.sin(a)*d), rng.choice(petals), shade)

def molehill(b, rng, centre):
    blob(b, (centre[0], TOP+0.02, centre[1]), (0.24, 0.16, 0.22), "mole", "earth", rng, sub=1, squash=0.1, moss_from=-1.0)

def clover(b, rng, centre, radius, shade):
    blob(b, (centre[0], TOP+0.03, centre[1]), (radius, 0.07, radius*rng.uniform(0.7,1.0)), "turf_%s3" % shade, "turf_%s3" % shade, rng, sub=1, squash=0.1, moss_from=-1.0)

def lichen_rock(b, rng, centre, size):
    blob(b, (centre[0], TOP+0.04+size[1]*0.5, centre[1]), size, "lichen", "stone", rng, sub=1, squash=0.35, moss_from=0.5, patchy=0.5)

def branch(b, rng, at, angle, length):
    x,z = at
    pts=[(x+math.cos(angle)*length*t + math.sin(t*4)*0.05, TOP+0.05+0.035+0.02*math.sin(t*math.pi), z+math.sin(angle)*length*t) for t in (0, 0.35, 0.7, 1)]
    tube(b, pts, [0.035, 0.03, 0.024, 0.014], 5, ["branch","bark2"])

def tile(shade, index):
    rng = random.Random(3000 + SHADES.index(shade)*10 + index)
    b = Build()
    turf_body(b, rng, shade)
    keep = []
    def spots(n, margin=0.15):
        out=[]
        for _ in range(n):
            for _t in range(30):
                x = rng.uniform(-INNER+margin, INNER-margin); z = rng.uniform(-INNER+margin, INNER-margin)
                if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
            out.append((x,z)); keep.append((x,z,0.28))
        return out
    if index == 0:
        for at in spots(4): grass_tuft(b, rng, at, shade, size=rng.uniform(0.9,1.2))
        c = spots(1, 0.3)[0]; flowers(b, rng, c, 0.28, ["petal_white","petal_yellow"], 5, shade)
        pebbles(b, rng, 1, keep)
    elif index == 1:
        c = spots(1, 0.35)[0]; clover(b, rng, c, 0.42, shade); keep.append((c[0],c[1],0.5))
        m = spots(1, 0.3)[0]; molehill(b, rng, m)
        for at in spots(3): grass_tuft(b, rng, at, shade)
        for at in spots(2): flower(b, rng, at, rng.choice(["petal_white","petal_purple"]), shade)
    elif index == 2:
        c = spots(1, 0.4)[0]; lichen_rock(b, rng, c, (rng.uniform(0.32,0.42), 0.26, rng.uniform(0.26,0.36))); keep.append((c[0],c[1],0.55))
        for at in spots(3): grass_tuft(b, rng, at, shade)
        pebbles(b, rng, 2, keep)
    elif index == 3:
        c = spots(1, 0.35)[0]; flowers(b, rng, c, 0.34, ["petal_red","petal_purple","petal_white"], 6, shade); keep.append((c[0],c[1],0.45))
        c2 = spots(1, 0.3)[0]; flowers(b, rng, c2, 0.26, ["petal_yellow","petal_white"], 5, shade)
        for at in spots(2): grass_tuft(b, rng, at, shade)
    else:
        for at in spots(3): grass_tuft(b, rng, at, shade, size=rng.uniform(0.8,1.1))
        s0 = spots(1, 0.5)[0]; branch(b, rng, s0, rng.uniform(0, math.tau), rng.uniform(0.7, 0.9))
        pebbles(b, rng, 1, keep)
    return b.make("Grass %s %d" % (shade, index))

def main():
    scene, cam = ft["setup_scene"]()
    mat = ft["material"](os.path.join(HERE, "TileWorldPalette.png"))
    tiles = {shade: [tile(shade, i) for i in range(5)] for shade in SHADES}
    for shade in SHADES:
        for t in tiles[shade]: t.data.materials.append(mat); print("TILE %s verts %d faces %d" % (t.name, len(t.data.vertices), len(t.data.polygons)))
    # the line-ups: a row per shade
    for r, shade in enumerate(SHADES):
        for i, t in enumerate(tiles[shade]): t.location = ((i-2)*2.6, -(r-1)*3.2, 0)
    ft["look"](cam, (0, 1.0, 0), 15.5, 34, 0); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "grass-lineup.png"))
    # the country: a patch of terraces, shade by height, a few trees stood on it
    for shade in SHADES:
        for t in tiles[shade]: t.hide_render = True
    trees = []
    try:
        tsrc = open(os.path.join(HERE, "forest_trees.py")).read().replace("\nmain()\n", "\n")
        tt = {"__file__": os.path.join(HERE, "forest_trees.py"), "__name__": "forest_trees"}; exec(compile(tsrc, "forest_trees.py", "exec"), tt)
        trees = [tt["oak"](0), tt["birch"](0), tt["sapling"](0)]
        for t in trees: t.data.materials.append(mat); t.hide_render = True
    except Exception as e: print("no trees for the patch:", e)
    rng = random.Random(5); placed = []
    for gx in range(-5, 5):
        for gz in range(-5, 5):
            h = round(1.4 * math.sin(gx*0.45) * math.cos(gz*0.35) * 2) / 2 * 0.25 + rng.choice([0, 0, 0.25])
            shade = "pale" if h >= 0.5 else ("light" if h >= 0.25 else "dark")
            src = rng.choice(tiles[shade])
            ob = bpy.data.objects.new("P", src.data); scene.collection.objects.link(ob)
            ob.location = (gx*2.0+1.0, -(gz*2.0+1.0), h); ob.rotation_euler = (0, 0, rng.choice([0,1,2,3])*math.pi/2)
            placed.append(ob)
            if trees and rng.random() < 0.12:
                tr = bpy.data.objects.new("T", rng.choice(trees).data); scene.collection.objects.link(tr)
                tr.location = (gx*2.0+1.0+rng.uniform(-0.3,0.3), -(gz*2.0+1.0)+rng.uniform(-0.3,0.3), h+1.05); tr.rotation_euler = (0,0,rng.uniform(0,math.tau))
                placed.append(tr)
    ft["look"](cam, (0, 1.8, 0), 21, 30, 28); cam.data.lens = 38
    ft["render"](os.path.join(HERE, "grass-country.png"))
    ft["look"](cam, (0, 1.4, 1), 8.5, 12, 55); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "grass-low.png"))
    for ob in placed: bpy.data.objects.remove(ob)
    for shade in SHADES:
        for t in tiles[shade]:
            t.hide_render = False; t.location = (0,0,0); t.rotation_euler = (0,0,0)
            bpy.ops.object.select_all(action='DESELECT'); t.select_set(True); bpy.context.view_layer.objects.active = t
            bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, t.name + ".fbx"), use_selection=True, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL', mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
