# Marsh tiles for Tile World: the dark wet ground of the low flats, the reedbeds, the dead woods and
# the beds of lakes and ponds. Five variants built in Blender on the game's terms, like the forest
# floor and the grass. Renders previews and exports FBX.
import bpy, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
Build, prism, tube, blob, pebbles, twig = ft["Build"], ft["prism"], ft["tube"], ft["blob"], ft["pebbles"], ft["twig"]
TOP, BOTTOM, HALF, INNER = ft["TOP"], ft["BOTTOM"], ft["HALF"], ft["INNER"]
from mathutils import Vector

def mud_body(b, rng, puddle=None):
    """The block: earth sides, a top of mud facets, lower and flatter than grass, with a puddle sunk in it if asked."""
    n = 4; grid = {}
    for i in range(n+1):
        for j in range(n+1):
            x = -HALF + 2*HALF*i/n; z = -HALF + 2*HALF*j/n
            edge = i==0 or j==0 or i==n or j==n
            y = TOP if edge else TOP + rng.uniform(0.0, 0.04)
            if not edge: x += rng.uniform(-0.05,0.05); z += rng.uniform(-0.05,0.05)
            if puddle is not None and not edge and (x-puddle[0])**2 + (z-puddle[1])**2 < puddle[2]**2: y = TOP - 0.03
            grid[(i,j)] = (x,y,z)
    for i in range(n):
        for j in range(n):
            a,bq,c,d = grid[(i,j)], grid[(i,j+1)], grid[(i+1,j+1)], grid[(i+1,j)]
            for tri in ((a,bq,c),(a,c,d)):
                cx = sum(p[0] for p in tri)/3; cz = sum(p[2] for p in tri)/3
                wet = puddle is not None and (cx-puddle[0])**2 + (cz-puddle[1])**2 < (puddle[2]*0.85)**2
                col = rng.choice(["puddle","puddle","puddle2"]) if wet else rng.choice(["mud","mud","mud2","mud3"])
                b.tri(*tri, col, out=(0,1,0))
    bands = [(TOP, TOP-0.14, "mud2"), (TOP-0.14, 0.1, "earth"), (0.1, BOTTOM, "earth2")]
    corners = [(-HALF,-HALF),(HALF,-HALF),(HALF,HALF),(-HALF,HALF)]
    for k in range(4):
        (x0,z0),(x1,z1) = corners[k], corners[(k+1)%4]
        outward = ((x0+x1)*0.5, 0, (z0+z1)*0.5)
        for (y1,y0,col) in bands:
            b.quad((x0,y0,z0),(x1,y0,z1),(x1,y1,z1),(x0,y1,z0), col, out=outward)
    b.quad((-HALF,BOTTOM,-HALF),(-HALF,BOTTOM,HALF),(HALF,BOTTOM,HALF),(HALF,BOTTOM,-HALF), "earth2", out=(0,-1,0))

def sedge(b, rng, at, size=1.0, blades=6):
    """A clump of sedge: tall narrow blades, darker than grass, leaning outward."""
    x,z = at
    for k in range(blades):
        a = k/blades*math.tau + rng.uniform(-0.3,0.3)
        h = rng.uniform(0.32, 0.5)*size; lean = rng.uniform(0.08, 0.18); w = 0.022*size
        base = (x + math.cos(a)*0.03, TOP+0.03, z + math.sin(a)*0.03)
        tip = (x + math.cos(a)*lean, TOP+0.03+h, z + math.sin(a)*lean)
        sx, sz = math.cos(a+math.pi/2)*w, math.sin(a+math.pi/2)*w
        col = "sedge" if k%2 else "sedge2"
        b.quad((base[0]-sx,base[1],base[2]-sz),(base[0]+sx,base[1],base[2]+sz),(tip[0]+sx*0.2,tip[1],tip[2]+sz*0.2),(tip[0]-sx*0.2,tip[1],tip[2]-sz*0.2), col)
        b.quad((tip[0]-sx*0.2,tip[1],tip[2]-sz*0.2),(tip[0]+sx*0.2,tip[1],tip[2]+sz*0.2),(base[0]+sx,base[1],base[2]+sz),(base[0]-sx,base[1],base[2]-sz), col)

def hummock(b, rng, centre, radius):
    blob(b, (centre[0], TOP+0.02, centre[1]), (radius, 0.09, radius*rng.uniform(0.7,1.0)), "hummock", "sedge2", rng, sub=1, squash=0.1, moss_from=0.2)

def wet_stone(b, rng, centre, size):
    blob(b, (centre[0], TOP+0.03+size[1]*0.45, centre[1]), size, "wetstone", "wetstone2", rng, sub=1, squash=0.35, moss_from=0.55, patchy=0.3)

def sunk_log(b, rng, p0, p1, radius):
    """A log half sunk in the mud, dark and rotten."""
    ft["cylinder_along"](b, (p0[0], TOP+0.02+radius*0.45, p0[1]), (p1[0], TOP+0.02+radius*0.45, p1[1]), radius, 7, "rot", "rot2", rng=rng, jitter=0.12, taper=0.9)

def dark_mushroom(b, rng, at, size=1.0):
    x,z = at; h = 0.08*size; r = 0.09*size
    prism(b, (x, TOP+0.03, z), 0.025*size, h, 5, "darkcap", "darkcap", taper=0.9)
    cap = [(x+math.cos(k/6*math.tau)*r, TOP+0.03+h, z+math.sin(k/6*math.tau)*r) for k in range(6)]
    peak = (x, TOP+0.03+h+0.04*size, z)
    for k in range(6):
        a0 = (k+0.5)/6*math.tau
        b.tri(cap[k], cap[(k+1)%6], peak, "darkcap" if k%2 else "rot", out=(math.cos(a0), 0.8, math.sin(a0)))
    b.face(cap, "rot2", out=(0,-1,0))

def tile(index):
    rng = random.Random(4000+index)
    b = Build(); keep = []
    def spots(n, margin=0.15):
        out=[]
        for _ in range(n):
            for _t in range(30):
                x = rng.uniform(-INNER+margin, INNER-margin); z = rng.uniform(-INNER+margin, INNER-margin)
                if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
            out.append((x,z)); keep.append((x,z,0.3))
        return out
    if index == 0:
        p = (rng.uniform(-0.3,0.3), rng.uniform(-0.3,0.3), 0.5)
        mud_body(b, rng, puddle=p); keep.append((p[0],p[1],0.6))
        for at in spots(3): sedge(b, rng, at)
        pebbles(b, rng, 1, keep)
    elif index == 1:
        mud_body(b, rng)
        for at in spots(5): sedge(b, rng, at, size=rng.uniform(0.9,1.3), blades=rng.choice([6,8]))
        c = spots(1, 0.35)[0]; hummock(b, rng, c, 0.3)
    elif index == 2:
        mud_body(b, rng)
        c = spots(1, 0.4)[0]; wet_stone(b, rng, c, (rng.uniform(0.3,0.4), 0.22, rng.uniform(0.24,0.34)))
        for at in spots(2): wet_stone(b, rng, at, (0.16, 0.12, 0.14))
        for at in spots(2): sedge(b, rng, at, size=0.9)
    elif index == 3:
        mud_body(b, rng)
        a = rng.uniform(0, math.tau)
        sunk_log(b, rng, (-0.6*math.cos(a), -0.6*math.sin(a)), (0.6*math.cos(a), 0.6*math.sin(a)), 0.17); keep.append((0,0,0.35))
        for at in spots(3, 0.2): dark_mushroom(b, rng, at, size=rng.uniform(0.8,1.3))
        sedge(b, rng, spots(1)[0], size=0.9)
    else:
        mud_body(b, rng)
        for at in spots(2, 0.3): hummock(b, rng, at, rng.uniform(0.22,0.3))
        for at in spots(2): sedge(b, rng, at, size=0.8, blades=5)
        for k in range(2): twig(b, rng, spots(1)[0], rng.uniform(0,math.tau), 0.3)
        pebbles(b, rng, 1, keep)
    return b.make("Marsh Tile %d" % index)

def main():
    scene, cam = ft["setup_scene"]()
    mat = ft["material"](os.path.join(HERE, "TileWorldPalette.png"))
    tiles = [tile(i) for i in range(5)]
    for t in tiles: t.data.materials.append(mat); print("TILE %s verts %d faces %d" % (t.name, len(t.data.vertices), len(t.data.polygons)))
    for i,t in enumerate(tiles): t.location = ((i-2)*2.6, 0, 0)
    ft["look"](cam, (0, 1.0, 0), 11.5, 30, 0); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "marsh-lineup.png"))
    for t in tiles: t.hide_render = True
    rng = random.Random(9); placed = []
    for gx in range(-4, 4):
        for gz in range(-4, 4):
            ob = bpy.data.objects.new("P", rng.choice(tiles).data); scene.collection.objects.link(ob)
            ob.location = (gx*2.0+1.0, -(gz*2.0+1.0), rng.choice([0,0,0,0,0.25])); ob.rotation_euler = (0, 0, rng.choice([0,1,2,3])*math.pi/2)
            placed.append(ob)
    ft["look"](cam, (0, 1.2, 0), 15.5, 34, 28); cam.data.lens = 38
    ft["render"](os.path.join(HERE, "marsh-patch.png"))
    ft["look"](cam, (0, 1.3, 1), 7.5, 12, 55); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "marsh-low.png"))
    for ob in placed: bpy.data.objects.remove(ob)
    for t in tiles:
        t.hide_render = False; t.location = (0,0,0); t.rotation_euler = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT'); t.select_set(True); bpy.context.view_layer.objects.active = t
        bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, t.name + ".fbx"), use_selection=True, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL', mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
