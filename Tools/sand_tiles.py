# Sand tiles for Tile World: two sets from one script. Beach sand -- shells, driftwood, dune grass,
# a starfish, a wet patch -- for the shores and the shallows; desert sand -- wind ripples, a cracked
# pan, a sandstone rock, dry scrub, a scatter of stones -- for the deserts. Built in Blender like
# the rest, on the game's terms; five of each. Renders previews and exports FBX.
import bpy, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
Build, prism, tube, blob, cylinder_along = ft["Build"], ft["prism"], ft["tube"], ft["blob"], ft["cylinder_along"]
TOP, BOTTOM, HALF, INNER = ft["TOP"], ft["BOTTOM"], ft["HALF"], ft["INNER"]
from mathutils import Vector

def sand_body(b, rng, tones, band, ripples=False):
    """The block: sand facets on top, a band of the same under the rim, darker sand below. Rippled if asked: the inner rows lifted in waves."""
    n = 4; grid = {}
    for i in range(n+1):
        for j in range(n+1):
            x = -HALF + 2*HALF*i/n; z = -HALF + 2*HALF*j/n
            edge = i==0 or j==0 or i==n or j==n
            y = TOP if edge else TOP + (0.05 + 0.035*math.sin(i*2.2 + j*0.6) if ripples else rng.uniform(0.0, 0.035))
            if not edge: x += rng.uniform(-0.05,0.05); z += rng.uniform(-0.05,0.05)
            grid[(i,j)] = (x,y,z)
    for i in range(n):
        for j in range(n):
            a,bq,c,d = grid[(i,j)], grid[(i,j+1)], grid[(i+1,j+1)], grid[(i+1,j)]
            b.tri(a,bq,c,rng.choice(tones),out=(0,1,0)); b.tri(a,c,d,rng.choice(tones),out=(0,1,0))
    bands = [(TOP, TOP-0.3, band), (TOP-0.3, BOTTOM, "sanddark")]
    corners = [(-HALF,-HALF),(HALF,-HALF),(HALF,HALF),(-HALF,HALF)]
    for k in range(4):
        (x0,z0),(x1,z1) = corners[k], corners[(k+1)%4]
        outward = ((x0+x1)*0.5, 0, (z0+z1)*0.5)
        for (y1,y0,col) in bands:
            b.quad((x0,y0,z0),(x1,y0,z1),(x1,y1,z1),(x0,y1,z0), col, out=outward)
    b.quad((-HALF,BOTTOM,-HALF),(-HALF,BOTTOM,HALF),(HALF,BOTTOM,HALF),(HALF,BOTTOM,-HALF), "sanddark", out=(0,-1,0))

def shell(b, rng, at):
    """A shell: a fan of five facets rising to a hinge, pale or pink."""
    x,z = at; a = rng.uniform(0, math.tau); s = rng.uniform(0.07, 0.11)
    col = rng.choice(["shell","shell","shellpink"])
    hinge = (x - math.cos(a)*s*0.6, TOP+0.06+s*0.35, z - math.sin(a)*s*0.6)
    rim = [(x + math.cos(a + (k-2)*0.5)*s, TOP+0.06, z + math.sin(a + (k-2)*0.5)*s) for k in range(5)]
    for k in range(4): b.tri(hinge, rim[k], rim[k+1], col, out=(0,1,0))
    b.face(list(reversed(rim)) + [hinge], "sanddark", out=(0,-1,0)) if False else None
    b.face([hinge] + rim, "shellpink" if col == "shell" else "shell", out=(0,-1,0))

def starfish(b, rng, at):
    x,z = at; a0 = rng.uniform(0, math.tau); r = 0.13
    centre = (x, TOP+0.075, z)
    for k in range(5):
        a = a0 + k/5*math.tau
        tip = (x + math.cos(a)*r, TOP+0.062, z + math.sin(a)*r)
        l = (x + math.cos(a-0.35)*r*0.35, TOP+0.07, z + math.sin(a-0.35)*r*0.35); rr = (x + math.cos(a+0.35)*r*0.35, TOP+0.07, z + math.sin(a+0.35)*r*0.35)
        b.tri(centre, l, tip, "starfish", out=(0,1,0)); b.tri(centre, tip, rr, "starfish", out=(0,1,0))
        b.tri(centre, rr, (x + math.cos(a+0.63)*r*0.35, TOP+0.07, z + math.sin(a+0.63)*r*0.35), "starfish", out=(0,1,0))

def driftwood(b, rng, at, angle, length):
    x,z = at
    pts = [(x+math.cos(angle)*length*t + math.sin(t*5)*0.04, TOP+0.05+0.06+0.02*math.sin(t*math.pi), z+math.sin(angle)*length*t) for t in (0, 0.33, 0.66, 1)]
    tube(b, pts, [0.06, 0.055, 0.045, 0.025], 6, ["driftwood","driftwood","driftwood2"])

def marram(b, rng, at, size=1.0):
    """Dune grass: eleven thin pale blades, tall and leaning with the wind."""
    x,z = at; lean_a = rng.uniform(0, math.tau)
    for k in range(11):
        a = k/11*math.tau + rng.uniform(-0.25,0.25)
        h = rng.uniform(0.34, 0.56)*size; lean = rng.uniform(0.12, 0.28); w = 0.02*size
        base = (x + math.cos(a)*0.05, TOP+0.05, z + math.sin(a)*0.05)
        tip = (x + math.cos(a)*lean*0.5 + math.cos(lean_a)*lean*0.5, TOP+0.05+h, z + math.sin(a)*lean*0.5 + math.sin(lean_a)*lean*0.5)
        sx, sz = math.cos(a+math.pi/2)*w, math.sin(a+math.pi/2)*w
        col = "marram" if k % 3 else "marram2"
        b.quad((base[0]-sx,base[1],base[2]-sz),(base[0]+sx,base[1],base[2]+sz),(tip[0]+sx*0.2,tip[1],tip[2]+sz*0.2),(tip[0]-sx*0.2,tip[1],tip[2]-sz*0.2), col)
        b.quad((tip[0]-sx*0.2,tip[1],tip[2]-sz*0.2),(tip[0]+sx*0.2,tip[1],tip[2]+sz*0.2),(base[0]+sx,base[1],base[2]+sz),(base[0]-sx,base[1],base[2]-sz), col)

def wet_patch(b, rng, centre, radius):
    ring = [(centre[0]+math.cos(k/8*math.tau)*radius*rng.uniform(0.75,1.05), TOP+0.056, centre[1]+math.sin(k/8*math.tau)*radius*rng.uniform(0.75,1.05)) for k in range(8)]
    b.face(ring, "sandwet", out=(0,1,0))

def pan(b, rng, centre, radius):
    """A cracked pan: a pale plate with dark seams across it."""
    ring = [(centre[0]+math.cos(k/8*math.tau)*radius*rng.uniform(0.8,1.05), TOP+0.056, centre[1]+math.sin(k/8*math.tau)*radius*rng.uniform(0.8,1.05)) for k in range(8)]
    b.face(ring, "pan", out=(0,1,0))
    for _ in range(5):
        a = rng.uniform(0, math.tau); l = radius*rng.uniform(0.5,0.9); w = 0.014
        x0 = centre[0] + math.cos(a+math.pi)*l*0.3; z0 = centre[1] + math.sin(a+math.pi)*l*0.3
        pts = [(x0+math.cos(a)*l*t + math.sin(t*7)*0.02, TOP+0.06, z0+math.sin(a)*l*t) for t in (0, 0.5, 1)]
        for i in range(2):
            p0, p1 = pts[i], pts[i+1]; sx, sz = math.cos(a+math.pi/2)*w, math.sin(a+math.pi/2)*w
            b.quad((p0[0]-sx,p0[1],p0[2]-sz),(p0[0]+sx,p0[1],p0[2]+sz),(p1[0]+sx,p1[1],p1[2]+sz),(p1[0]-sx,p1[1],p1[2]-sz), "sanddark", out=(0,1,0))

def sandstone(b, rng, centre, size):
    blob(b, (centre[0], TOP+0.04+size[1]*0.5, centre[1]), size, "sandstone", "sandstone2", rng, sub=1, squash=0.35, moss_from=0.35)

def scrub(b, rng, at):
    """Dry scrub: a knot of bare twigs out of the sand."""
    x,z = at
    for k in range(6):
        a = k/6*math.tau + rng.uniform(-0.4,0.4); h = rng.uniform(0.18, 0.34); lean = rng.uniform(0.1, 0.22)
        pts = [(x, TOP+0.05, z), (x+math.cos(a)*lean*0.5, TOP+0.05+h*0.55, z+math.sin(a)*lean*0.5), (x+math.cos(a)*lean, TOP+0.05+h, z+math.sin(a)*lean)]
        tube(b, pts, [0.016, 0.012, 0.007], 3, ["scrub","branch"], cap_start=False)

def stones(b, rng, count, keep, col=("sandstone2","stone2")):
    for _ in range(count):
        for _t in range(30):
            x = rng.uniform(-INNER+0.15, INNER-0.15); z = rng.uniform(-INNER+0.15, INNER-0.15)
            if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
        s = rng.uniform(0.06, 0.12)
        blob(b, (x, TOP+0.05+s*0.3, z), (s, s*0.6, s*0.85), col[0], col[1], rng, sub=0, squash=0.3, moss_from=2.0)
        keep.append((x,z,0.2))

def beach(index):
    rng = random.Random(5000 + index); b = Build()
    sand_body(b, rng, ["sand1","sand1","sand2","sand3"], "sand2")
    keep = []
    def spot(margin=0.2, r=0.3):
        for _t in range(40):
            x = rng.uniform(-INNER+margin, INNER-margin); z = rng.uniform(-INNER+margin, INNER-margin)
            if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
        keep.append((x,z,r)); return (x,z)
    if index == 0:
        for _ in range(4): shell(b, rng, spot(0.15, 0.18))
        stones(b, rng, 2, keep, ("pebble","stone2"))
    elif index == 1:
        marram(b, rng, spot(0.35, 0.45), size=rng.uniform(1.0,1.2)); marram(b, rng, spot(0.3, 0.4), size=0.9)
        shell(b, rng, spot(0.15, 0.18))
    elif index == 2:
        c = spot(0.5, 0.55); driftwood(b, rng, (c[0]-0.5, c[1]), rng.uniform(-0.4,0.4), 1.1)
        for _ in range(2): shell(b, rng, spot(0.15, 0.18))
        stones(b, rng, 1, keep, ("pebble","stone2"))
    elif index == 3:
        w = spot(0.4, 0.5); wet_patch(b, rng, w, 0.42)
        starfish(b, rng, spot(0.25, 0.25))
        for _ in range(2): shell(b, rng, spot(0.15, 0.18))
    else:
        c = spot(0.35, 0.45); sandstone(b, rng, c, (0.3, 0.2, 0.24)); keep.append((c[0],c[1],0.45))
        marram(b, rng, spot(0.3, 0.4), size=0.85)
        stones(b, rng, 2, keep, ("pebble","stone2"))
    return b.make("Beach Tile %d" % index)

def desert(index):
    rng = random.Random(6000 + index); b = Build()
    sand_body(b, rng, ["desert1","desert1","desert2","desert3"], "desert2", ripples=index in (0, 3))
    keep = []
    def spot(margin=0.2, r=0.3):
        for _t in range(40):
            x = rng.uniform(-INNER+margin, INNER-margin); z = rng.uniform(-INNER+margin, INNER-margin)
            if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
        keep.append((x,z,r)); return (x,z)
    if index == 0:
        stones(b, rng, 2, keep)
    elif index == 1:
        c = spot(0.4, 0.5); pan(b, rng, c, 0.46)
        stones(b, rng, 2, keep)
    elif index == 2:
        c = spot(0.4, 0.55); sandstone(b, rng, c, (rng.uniform(0.34,0.44), 0.32, rng.uniform(0.28,0.36))); keep.append((c[0],c[1],0.6))
        stones(b, rng, 3, keep)
    elif index == 3:
        scrub(b, rng, spot(0.3, 0.35)); scrub(b, rng, spot(0.3, 0.35))
        stones(b, rng, 1, keep)
    else:
        stones(b, rng, 6, keep)
        scrub(b, rng, spot(0.3, 0.35))
    return b.make("Desert Tile %d" % index)

def main():
    scene, cam = ft["setup_scene"]()
    mat = ft["material"](os.path.join(HERE, "TileWorldPalette.png"))
    beaches = [beach(i) for i in range(5)]; deserts = [desert(i) for i in range(5)]
    for t in beaches + deserts: t.data.materials.append(mat); print("TILE %s verts %d faces %d" % (t.name, len(t.data.vertices), len(t.data.polygons)))
    for i,t in enumerate(beaches): t.location = ((i-2)*2.6, 1.6, 0)
    for i,t in enumerate(deserts): t.location = ((i-2)*2.6, -1.6, 0)
    ft["look"](cam, (0, 1.0, 0), 13, 34, 0); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "sand-lineup.png"))
    for t in beaches + deserts: t.hide_render = True
    rng = random.Random(3); placed = []
    for gx in range(-4, 4):
        for gz in range(-4, 4):
            src = rng.choice(beaches if gz < 0 else deserts)
            ob = bpy.data.objects.new("P", src.data); scene.collection.objects.link(ob)
            ob.location = (gx*2.0+1.0, -(gz*2.0+1.0), rng.choice([0,0,0,0.25]) if gz >= 0 else 0); ob.rotation_euler = (0,0,rng.choice([0,1,2,3])*math.pi/2)
            placed.append(ob)
    ft["look"](cam, (0, 1.2, 0), 15, 34, 25); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "sand-patch.png"))
    ft["look"](cam, (0.5, 1.3, -4), 6.5, 16, 55); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "sand-low.png"))
    for ob in placed: bpy.data.objects.remove(ob)
    for t in beaches + deserts:
        t.hide_render = False; t.location = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT'); t.select_set(True); bpy.context.view_layer.objects.active = t
        bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, t.name + ".fbx"), use_selection=True, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL', mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
