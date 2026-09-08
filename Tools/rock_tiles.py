# Rock tiles for Tile World, two sets from one script: stone -- the barrens, the beds of deep water
# and of frozen lakes -- as bare rock in slabs, cracked, lichened, with a boulder, with a crop of
# stones; and scree -- the steep faces -- as broken rock and gravel sliding down. Built in Blender
# on the game's terms, five of each. Renders previews and exports FBX.
import bpy, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
Build, prism, tube, blob = ft["Build"], ft["prism"], ft["tube"], ft["blob"]
TOP, BOTTOM, HALF, INNER = ft["TOP"], ft["BOTTOM"], ft["HALF"], ft["INNER"]
from mathutils import Vector

def rock_body(b, rng, tones, band, slabs=False, tilt=0.0):
    """The block: rock facets on top, the sides banded like strata. Slabbed: the top in a few big tilted plates rather than a fine grid."""
    n = 2 if slabs else 4; grid = {}
    for i in range(n+1):
        for j in range(n+1):
            x = -HALF + 2*HALF*i/n; z = -HALF + 2*HALF*j/n
            edge = i==0 or j==0 or i==n or j==n
            y = TOP if edge else TOP + rng.uniform(0.02, 0.14 if slabs else 0.08) + tilt*(x)
            if not edge: x += rng.uniform(-0.08,0.08); z += rng.uniform(-0.08,0.08)
            grid[(i,j)] = (x,y,z)
    for i in range(n):
        for j in range(n):
            a,bq,c,d = grid[(i,j)], grid[(i,j+1)], grid[(i+1,j+1)], grid[(i+1,j)]
            b.tri(a,bq,c,rng.choice(tones),out=(0,1,0)); b.tri(a,c,d,rng.choice(tones),out=(0,1,0))
    bands = [(TOP, TOP-0.35, band[0]), (TOP-0.35, TOP-0.9, band[1]), (TOP-0.9, BOTTOM, band[2])]
    corners = [(-HALF,-HALF),(HALF,-HALF),(HALF,HALF),(-HALF,HALF)]
    for k in range(4):
        (x0,z0),(x1,z1) = corners[k], corners[(k+1)%4]
        outward = ((x0+x1)*0.5, 0, (z0+z1)*0.5)
        for (y1,y0,col) in bands:
            b.quad((x0,y0,z0),(x1,y0,z1),(x1,y1,z1),(x0,y1,z0), col, out=outward)
    b.quad((-HALF,BOTTOM,-HALF),(-HALF,BOTTOM,HALF),(HALF,BOTTOM,HALF),(HALF,BOTTOM,-HALF), band[2], out=(0,-1,0))

def crack(b, rng, start, angle, length):
    """A crack across the rock: a thin dark strip that wanders."""
    x,z = start; w = 0.02
    pts=[(x+math.cos(angle)*length*t + math.sin(t*6)*0.05, TOP+0.15, z+math.sin(angle)*length*t + math.cos(t*5)*0.04) for t in (0, 0.33, 0.66, 1)]
    for i in range(3):
        p0,p1 = pts[i],pts[i+1]; sx,sz = math.cos(angle+math.pi/2)*w, math.sin(angle+math.pi/2)*w
        b.quad((p0[0]-sx,p0[1],p0[2]-sz),(p0[0]+sx,p0[1],p0[2]+sz),(p1[0]+sx,p1[1],p1[2]+sz),(p1[0]-sx,p1[1],p1[2]-sz), "crack", out=(0,1,0))

def boulder(b, rng, centre, size, lichen=False):
    blob(b, (centre[0], TOP+0.06+size[1]*0.45, centre[1]), size, "lichen2" if lichen else "rock3", "rock2", rng, sub=1, squash=0.35, moss_from=0.5 if lichen else 2.0, patchy=0.4)

def lichen_patch(b, rng, centre, radius):
    ring=[(centre[0]+math.cos(k/7*math.tau)*radius*rng.uniform(0.7,1.05), TOP+0.15, centre[1]+math.sin(k/7*math.tau)*radius*rng.uniform(0.7,1.05)) for k in range(7)]
    b.face(ring, "lichen2", out=(0,1,0))

def shards(b, rng, count, keep, tones):
    """Broken rock: sharp low pyramids and wedges lying on the surface."""
    for _ in range(count):
        for _t in range(30):
            x = rng.uniform(-INNER+0.12, INNER-0.12); z = rng.uniform(-INNER+0.12, INNER-0.12)
            if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
        s = rng.uniform(0.1, 0.24); h = rng.uniform(0.08, 0.2); a = rng.uniform(0, math.tau)
        base = [(x+math.cos(a+k/4*math.tau)*s*rng.uniform(0.6,1.0), TOP+0.12, z+math.sin(a+k/4*math.tau)*s*rng.uniform(0.6,1.0)) for k in range(4)]
        peak = (x+rng.uniform(-0.05,0.05), TOP+0.12+h, z+rng.uniform(-0.05,0.05))
        for k in range(4):
            mid = ((base[k][0]+base[(k+1)%4][0])*0.5 - x, 0.5, (base[k][2]+base[(k+1)%4][2])*0.5 - z)
            b.tri(base[k], base[(k+1)%4], peak, rng.choice(tones), out=mid)
        keep.append((x,z,s+0.05))

def gravel(b, rng, count, keep):
    for _ in range(count):
        x = rng.uniform(-INNER+0.08, INNER-0.08); z = rng.uniform(-INNER+0.08, INNER-0.08)
        s = rng.uniform(0.035, 0.07)
        blob(b, (x, TOP+0.13+s*0.3, z), (s, s*0.6, s*0.85), rng.choice(["gravel","gravel2","scree2"]), "gravel2", rng, sub=0, squash=0.3, moss_from=2.0)

def stone(index):
    rng = random.Random(7000+index); b = Build(); keep=[]
    tones = ["rock","rock","rock2","rock3"]; band = ("rock2","rockdark","rockdark")
    def spot(margin=0.3, r=0.4):
        for _t in range(40):
            x = rng.uniform(-INNER+margin, INNER-margin); z = rng.uniform(-INNER+margin, INNER-margin)
            if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
        keep.append((x,z,r)); return (x,z)
    if index == 0:
        rock_body(b, rng, tones, band, slabs=True, tilt=0.04)
    elif index == 1:
        rock_body(b, rng, tones, band)
        for k in range(3): crack(b, rng, spot(0.2, 0.2), rng.uniform(0, math.tau), rng.uniform(0.6, 1.0))
    elif index == 2:
        rock_body(b, rng, tones, band, slabs=True)
        for k in range(3): lichen_patch(b, rng, spot(0.25, 0.3), rng.uniform(0.16, 0.28))
        crack(b, rng, spot(0.2, 0.2), rng.uniform(0, math.tau), 0.7)
    elif index == 3:
        rock_body(b, rng, tones, band)
        c = spot(0.45, 0.6); boulder(b, rng, c, (rng.uniform(0.38,0.5), 0.36, rng.uniform(0.3,0.42)), lichen=True)
        shards(b, rng, 2, keep, tones)
    else:
        rock_body(b, rng, tones, band, slabs=True, tilt=-0.03)
        shards(b, rng, 5, keep, ["rock2","rock3","rockdark"])
    return b.make("Stone Tile %d" % index)

def scree(index):
    rng = random.Random(7100+index); b = Build(); keep=[]
    tones = ["scree","scree","scree2","scree3"]; band = ("scree2","gravel2","rockdark")
    rock_body(b, rng, tones, band, tilt=rng.uniform(-0.05,0.05))
    shards(b, rng, [3, 5, 2, 7, 4][index], keep, ["scree2","scree3","gravel"])
    gravel(b, rng, [10, 6, 14, 4, 12][index], keep)
    if index == 2:
        c = (rng.uniform(-0.4,0.4), rng.uniform(-0.4,0.4)); boulder(b, rng, c, (0.36, 0.3, 0.3))
    return b.make("Scree Tile %d" % index)

def main():
    scene, cam = ft["setup_scene"]()
    mat = ft["material"](os.path.join(HERE, "TileWorldPalette.png"))
    stones = [stone(i) for i in range(5)]; screes = [scree(i) for i in range(5)]
    for t in stones + screes: t.data.materials.append(mat); print("TILE %s verts %d faces %d" % (t.name, len(t.data.vertices), len(t.data.polygons)))
    for i,t in enumerate(stones): t.location = ((i-2)*2.6, 1.6, 0)
    for i,t in enumerate(screes): t.location = ((i-2)*2.6, -1.6, 0)
    ft["look"](cam, (0, 1.0, 0), 13, 34, 0); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "rock-lineup.png"))
    for t in stones + screes: t.hide_render = True
    rng = random.Random(13); placed = []
    for gx in range(-5, 5):
        for gz in range(-5, 5):
            steep = gz < -1
            src = rng.choice(screes if steep else stones)
            h = (-gz - 2) * 0.5 if steep else rng.choice([0, 0, 0.25, 0.5])
            ob = bpy.data.objects.new("P", src.data); scene.collection.objects.link(ob)
            ob.location = (gx*2.0+1.0, -(gz*2.0+1.0), h); ob.rotation_euler = (0,0,rng.choice([0,1,2,3])*math.pi/2); placed.append(ob)
    ft["look"](cam, (0, 1.6, 0), 19, 30, 25); cam.data.lens = 38
    ft["render"](os.path.join(HERE, "rock-country.png"))
    ft["look"](cam, (0, 1.4, 3), 7.5, 12, 50); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "rock-low.png"))
    for ob in placed: bpy.data.objects.remove(ob)
    for t in stones + screes:
        t.hide_render = False; t.location = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT'); t.select_set(True); bpy.context.view_layer.objects.active = t
        bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, t.name + ".fbx"), use_selection=True, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL', mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
