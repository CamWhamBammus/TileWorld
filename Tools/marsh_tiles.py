# Marsh tiles for Tile World: the dark wet flats, which are also the beds of lakes and ponds and
# the floor of the dead woods and the reedbeds. Built in Blender like the forest floor and the
# grass, on the game's terms; five of them. Renders previews and exports FBX.
import bpy, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
Build, prism, tube, blob, cylinder_along = ft["Build"], ft["prism"], ft["tube"], ft["blob"], ft["cylinder_along"]
TOP, BOTTOM, HALF, INNER = ft["TOP"], ft["BOTTOM"], ft["HALF"], ft["INNER"]
from mathutils import Vector

def mud_body(b, rng):
    """The block: wet mud facets on top in three tones, a darker wet band under the rim, earth below."""
    n = 4; grid = {}
    for i in range(n+1):
        for j in range(n+1):
            x = -HALF + 2*HALF*i/n; z = -HALF + 2*HALF*j/n
            edge = i==0 or j==0 or i==n or j==n
            y = TOP if edge else TOP + rng.uniform(0.0, 0.045)
            if not edge: x += rng.uniform(-0.06,0.06); z += rng.uniform(-0.06,0.06)
            grid[(i,j)] = (x,y,z)
    tones = ["mud1","mud1","mud2","mud3","mudwet"]
    for i in range(n):
        for j in range(n):
            a,bq,c,d = grid[(i,j)], grid[(i,j+1)], grid[(i+1,j+1)], grid[(i+1,j)]
            b.tri(a,bq,c,rng.choice(tones),out=(0,1,0)); b.tri(a,c,d,rng.choice(tones),out=(0,1,0))
    bands = [(TOP, TOP-0.2, "mudwet"), (TOP-0.2, 0.1, "earth2"), (0.1, BOTTOM, "earth2")]
    corners = [(-HALF,-HALF),(HALF,-HALF),(HALF,HALF),(-HALF,HALF)]
    for k in range(4):
        (x0,z0),(x1,z1) = corners[k], corners[(k+1)%4]
        outward = ((x0+x1)*0.5, 0, (z0+z1)*0.5)
        for (y1,y0,col) in bands:
            b.quad((x0,y0,z0),(x1,y0,z1),(x1,y1,z1),(x0,y1,z0), col, out=outward)
    b.quad((-HALF,BOTTOM,-HALF),(-HALF,BOTTOM,HALF),(HALF,BOTTOM,HALF),(HALF,BOTTOM,-HALF), "earth2", out=(0,-1,0))

def puddle(b, rng, centre, radius):
    """Standing water: a flat dark polygon lying on the mud, its edge uneven."""
    ring = []
    for k in range(9):
        a = k/9*math.tau; r = radius*rng.uniform(0.7, 1.05)
        ring.append((centre[0]+math.cos(a)*r, TOP+0.062, centre[1]+math.sin(a)*r))
    b.face(ring, "puddle", out=(0,1,0))
    rim = [(centre[0]+(p[0]-centre[0])*1.12, TOP+0.058, centre[1]+(p[2]-centre[1])*1.12) for p in ring]
    b.face(rim, "mudwet", out=(0,1,0))

def sedge(b, rng, at, size=1.0):
    """A sedge tuft: nine tall thin blades, blue-green, leaning every way."""
    x,z = at
    for k in range(9):
        a = k/9*math.tau + rng.uniform(-0.3,0.3)
        h = rng.uniform(0.34, 0.52)*size; lean = rng.uniform(0.1, 0.24); w = 0.022*size
        base = (x + math.cos(a)*0.04, TOP+0.05, z + math.sin(a)*0.04)
        tip = (x + math.cos(a)*lean, TOP+0.05+h, z + math.sin(a)*lean)
        sx, sz = math.cos(a+math.pi/2)*w, math.sin(a+math.pi/2)*w
        col = "sedge" if k % 3 else "sedge2"
        b.quad((base[0]-sx,base[1],base[2]-sz),(base[0]+sx,base[1],base[2]+sz),(tip[0]+sx*0.2,tip[1],tip[2]+sz*0.2),(tip[0]-sx*0.2,tip[1],tip[2]-sz*0.2), col)
        b.quad((tip[0]-sx*0.2,tip[1],tip[2]-sz*0.2),(tip[0]+sx*0.2,tip[1],tip[2]+sz*0.2),(base[0]+sx,base[1],base[2]+sz),(base[0]-sx,base[1],base[2]-sz), col)

def algae(b, rng, centre, radius):
    blob(b, (centre[0], TOP+0.02, centre[1]), (radius, 0.05, radius*rng.uniform(0.7,1.0)), "algae", "algae2", rng, sub=1, squash=0.1, moss_from=0.3)

def wet_stones(b, rng, count, keep):
    for _ in range(count):
        for _t in range(30):
            x = rng.uniform(-INNER+0.15, INNER-0.15); z = rng.uniform(-INNER+0.15, INNER-0.15)
            if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
        s = rng.uniform(0.09, 0.17)
        blob(b, (x, TOP+0.05+s*0.3, z), (s, s*0.6, s*0.85), "wetstone", "stone2", rng, sub=0, squash=0.3, moss_from=2.0)
        keep.append((x,z,0.25))

def cracks(b, rng, count):
    """Dried mud: thin dark seams across the top."""
    for _ in range(count):
        x = rng.uniform(-0.8, 0.8); z = rng.uniform(-0.8, 0.8); a = rng.uniform(0, math.tau); l = rng.uniform(0.3, 0.6); w = 0.02
        pts = [(x+math.cos(a)*l*t + math.sin(t*9)*0.03, TOP+0.055, z+math.sin(a)*l*t) for t in (0, 0.5, 1)]
        for i in range(2):
            p0, p1 = pts[i], pts[i+1]
            sx, sz = math.cos(a+math.pi/2)*w, math.sin(a+math.pi/2)*w
            b.quad((p0[0]-sx,p0[1],p0[2]-sz),(p0[0]+sx,p0[1],p0[2]+sz),(p1[0]+sx,p1[1],p1[2]+sz),(p1[0]-sx,p1[1],p1[2]-sz), "mudwet", out=(0,1,0))

def reed_stub(b, rng, at, head=True):
    x,z = at
    h = rng.uniform(0.5, 0.8); lean = rng.uniform(-0.08, 0.08)
    top = (x+lean, TOP+0.05+h, z+lean*0.5)
    tube(b, [(x, TOP+0.05, z), (x+lean*0.5, TOP+0.05+h*0.5, z+lean*0.25), top], [0.02, 0.017, 0.012], 4, ["reedstalk"], cap_start=False, cap_end=not head)
    if head:
        prism(b, (top[0], top[1], top[2]), 0.03, 0.14, 5, "reedhead", "reedhead", taper=0.6)

def tile(index):
    rng = random.Random(4000 + index)
    b = Build()
    mud_body(b, rng)
    keep = []
    def spot(margin=0.2, r=0.3):
        for _t in range(40):
            x = rng.uniform(-INNER+margin, INNER-margin); z = rng.uniform(-INNER+margin, INNER-margin)
            if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
        keep.append((x,z,r)); return (x,z)
    if index == 0:
        c = spot(0.4, 0.5); puddle(b, rng, c, 0.42)
        for _ in range(3): sedge(b, rng, spot(), size=rng.uniform(0.9,1.2))
        wet_stones(b, rng, 1, keep)
    elif index == 1:
        c = spot(0.35, 0.45); algae(b, rng, c, 0.4)
        c2 = spot(0.3, 0.3); algae(b, rng, c2, 0.24)
        wet_stones(b, rng, 3, keep)
        sedge(b, rng, spot(), size=0.9)
    elif index == 2:
        cylinder_along(b, (-0.7, TOP+0.05+0.1, -0.3), (0.65, TOP+0.05+0.1, 0.35), 0.19, 7, "bark2", "woodring", rng=rng, jitter=0.12, taper=0.9)
        keep.append((0,0,0.35))
        for _ in range(2): sedge(b, rng, spot(), size=rng.uniform(0.9,1.1))
        algae(b, rng, spot(0.3, 0.3), 0.22)
    elif index == 3:
        cracks(b, rng, 7)
        for _ in range(4): reed_stub(b, rng, spot(0.25, 0.2), head=rng.random() < 0.6)
        wet_stones(b, rng, 2, keep)
    else:
        puddle(b, rng, spot(0.35, 0.4), 0.3); puddle(b, rng, spot(0.35, 0.4), 0.26)
        for _ in range(2): sedge(b, rng, spot(), size=rng.uniform(0.8,1.0))
        reed_stub(b, rng, spot(0.25, 0.2))
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
            ob.location = (gx*2.0+1.0, -(gz*2.0+1.0), rng.choice([0,0,0,0,0.25])); ob.rotation_euler = (0,0,rng.choice([0,1,2,3])*math.pi/2)
            placed.append(ob)
    ft["look"](cam, (0, 1.2, 0), 15, 34, 25); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "marsh-patch.png"))
    ft["look"](cam, (0.5, 1.3, 0.5), 6.5, 16, 55); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "marsh-low.png"))
    for ob in placed: bpy.data.objects.remove(ob)
    for t in tiles:
        t.hide_render = False; t.location = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT'); t.select_set(True); bpy.context.view_layer.objects.active = t
        bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, t.name + ".fbx"), use_selection=True, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL', mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
