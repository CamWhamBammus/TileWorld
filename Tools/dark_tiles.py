# The dark grounds of Tile World, two sets from one script: the fungal floor -- purple-brown loam
# with toadstools, glowing caps, a spore puff, a fairy ring -- and the dead woods' floor -- grey ash
# and charred wood, dead leaves, a fallen bough, old bones. Five of each, built in Blender on the
# game's terms. Renders previews and exports FBX.
import bpy, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
Build, prism, tube, blob, pebbles, twig, cylinder_along = ft["Build"], ft["prism"], ft["tube"], ft["blob"], ft["pebbles"], ft["twig"], ft["cylinder_along"]
TOP, BOTTOM, HALF, INNER = ft["TOP"], ft["BOTTOM"], ft["HALF"], ft["INNER"]
from mathutils import Vector

def ground(b, rng, tones, band):
    n = 4; grid = {}
    for i in range(n+1):
        for j in range(n+1):
            x = -HALF + 2*HALF*i/n; z = -HALF + 2*HALF*j/n
            edge = i==0 or j==0 or i==n or j==n
            y = TOP if edge else TOP + rng.uniform(0.0, 0.07)
            if not edge: x += rng.uniform(-0.06,0.06); z += rng.uniform(-0.06,0.06)
            grid[(i,j)] = (x,y,z)
    for i in range(n):
        for j in range(n):
            a,bq,c,d = grid[(i,j)], grid[(i,j+1)], grid[(i+1,j+1)], grid[(i+1,j)]
            b.tri(a,bq,c,rng.choice(tones),out=(0,1,0)); b.tri(a,c,d,rng.choice(tones),out=(0,1,0))
    bands = [(TOP, TOP-0.2, band[0]), (TOP-0.2, 0.1, band[1]), (0.1, BOTTOM, band[2])]
    corners = [(-HALF,-HALF),(HALF,-HALF),(HALF,HALF),(-HALF,HALF)]
    for k in range(4):
        (x0,z0),(x1,z1) = corners[k], corners[(k+1)%4]
        outward = ((x0+x1)*0.5, 0, (z0+z1)*0.5)
        for (y1,y0,col) in bands:
            b.quad((x0,y0,z0),(x1,y0,z1),(x1,y1,z1),(x0,y1,z0), col, out=outward)
    b.quad((-HALF,BOTTOM,-HALF),(-HALF,BOTTOM,HALF),(HALF,BOTTOM,HALF),(HALF,BOTTOM,-HALF), band[2], out=(0,-1,0))

def toadstool(b, rng, at, size=1.0, cap="redcap", stem="stem", spots=True):
    x,z = at; h = 0.22*size; r = 0.15*size
    prism(b, (x, TOP+0.08, z), 0.04*size, h, 6, stem, stem, taper=0.85)
    ring = [(x+math.cos(k/8*math.tau)*r, TOP+0.08+h, z+math.sin(k/8*math.tau)*r) for k in range(8)]
    peak = (x, TOP+0.08+h+0.13*size, z)
    for k in range(8):
        a0 = (k+0.5)/8*math.tau
        b.tri(ring[k], ring[(k+1)%8], peak, cap, out=(math.cos(a0), 0.7, math.sin(a0)))
    b.face(ring, "capcream", out=(0,-1,0))
    if spots:
        for k in range(4):
            a = k/4*math.tau + 0.5; d = r*0.55
            b.face([(x+math.cos(a+q/4*math.tau)*0.028*size + math.cos(a)*d, TOP+0.08+h+0.13*size*(1-d/r)*0.9+0.008, z+math.sin(a+q/4*math.tau)*0.028*size + math.sin(a)*d) for q in range(4)], "whitespot", out=(math.cos(a), 1.2, math.sin(a)))

def glowcap(b, rng, at, size=1.0):
    x,z = at; h = 0.16*size
    prism(b, (x, TOP+0.08, z), 0.02*size, h, 5, "glowstem", "glowstem")
    blob(b, (x, TOP+0.08+h+0.03, z), (0.06*size, 0.05*size, 0.06*size), "glowcap", "glowcap", rng, sub=0, squash=0.5, moss_from=-1)

def puffball(b, rng, at, size=1.0):
    x,z = at
    blob(b, (x, TOP+0.08+0.07*size, z), (0.11*size, 0.09*size, 0.11*size), "capcream", "spore", rng, sub=1, squash=0.6, moss_from=0.2)

def fungal(index):
    rng = random.Random(9000+index); b = Build(); keep=[]
    ground(b, rng, ["loam","loam","loam2","loam3"], ("loam2","earth2","earth2"))
    def spot(margin=0.2, r=0.3):
        for _t in range(40):
            x = rng.uniform(-INNER+margin, INNER-margin); z = rng.uniform(-INNER+margin, INNER-margin)
            if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
        keep.append((x,z,r)); return (x,z)
    if index == 0:
        for _ in range(3): toadstool(b, rng, spot(), size=rng.uniform(0.8,1.4))
        ft["litter"](b, rng, 6, keep)
    elif index == 1:
        # a fairy ring
        n = 7; c = (rng.uniform(-0.15,0.15), rng.uniform(-0.15,0.15))
        for k in range(n):
            a = k/n*math.tau; toadstool(b, rng, (c[0]+math.cos(a)*0.62, c[1]+math.sin(a)*0.62), size=rng.uniform(0.55,0.8), cap=rng.choice(["redcap","darkcap"]), spots=False)
        keep.append((c[0],c[1],0.85))
    elif index == 2:
        for _ in range(5): glowcap(b, rng, spot(0.15, 0.2), size=rng.uniform(0.8,1.3))
        ft["moss_patch"](b, rng, spot(0.3, 0.4), 0.28)
    elif index == 3:
        for _ in range(2): puffball(b, rng, spot(0.25, 0.3), size=rng.uniform(0.9,1.5))
        toadstool(b, rng, spot(), size=1.1, cap="darkcap", spots=False)
        ft["litter"](b, rng, 6, keep)
    else:
        cylinder_along(b, (-0.6, TOP+0.08+0.16, 0.3), (0.65, TOP+0.08+0.16, -0.35), 0.17, 7, "rot", "rot2", rng=rng, jitter=0.12, taper=0.9); keep.append((0,0,0.35))
        for _ in range(4): glowcap(b, rng, (rng.uniform(-0.5,0.5), rng.uniform(-0.05,0.05)+0.28), size=0.8)
        for _ in range(2): toadstool(b, rng, spot(), size=0.9, cap="darkcap", spots=False)
    return b.make("Fungal Tile %d" % index)

def bough(b, rng, at, angle, length):
    x,z = at
    pts=[(x+math.cos(angle)*length*t + math.sin(t*4)*0.06, TOP+0.06+0.07+0.03*math.sin(t*math.pi), z+math.sin(angle)*length*t) for t in (0, 0.33, 0.66, 1)]
    tube(b, pts, [0.075, 0.065, 0.05, 0.03], 6, ["char","bark2"])
    for k in range(2):
        a2 = angle + rng.uniform(-1.2, 1.2); p = Vector(pts[1+k])
        tube(b, [tuple(p), tuple(p + Vector((math.cos(a2)*0.3, 0.12, math.sin(a2)*0.3)))], [0.03, 0.012], 4, ["char"], cap_start=False)

def bones(b, rng, at, angle):
    x,z = at
    for k in range(4):
        d = (k-1.5)*0.12
        p0 = (x + math.cos(angle+math.pi/2)*d, TOP+0.075, z + math.sin(angle+math.pi/2)*d)
        p1 = (p0[0]+math.cos(angle)*rng.uniform(0.28,0.4), TOP+0.075+0.02, p0[2]+math.sin(angle)*rng.uniform(0.28,0.4))
        tube(b, [p0, p1], [0.022, 0.016], 4, ["bone"])

def dead(index):
    rng = random.Random(9100+index); b = Build(); keep=[]
    ground(b, rng, ["ash","ash","ash2","ash3"], ("ash2","earth2","earth2"))
    def spot(margin=0.2, r=0.3):
        for _t in range(40):
            x = rng.uniform(-INNER+margin, INNER-margin); z = rng.uniform(-INNER+margin, INNER-margin)
            if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
        keep.append((x,z,r)); return (x,z)
    if index == 0:
        for _ in range(3): twig(b, rng, spot(0.15, 0.2), rng.uniform(0, math.tau), rng.uniform(0.3,0.5))
        ft["litter"](b, rng, 10, keep)
    elif index == 1:
        c = spot(0.5, 0.6); bough(b, rng, (c[0]-0.5, c[1]), rng.uniform(-0.4,0.4), 1.1)
        ft["litter"](b, rng, 6, keep)
    elif index == 2:
        prism(b, (rng.uniform(-0.3,0.3), TOP+0.06, rng.uniform(-0.3,0.3)), 0.26, rng.uniform(0.3,0.55), 8, "char", "ash2", taper=0.8, rng=rng, jitter=0.2); keep.append((0,0,0.6))
        ft["litter"](b, rng, 8, keep)
    elif index == 3:
        bones(b, rng, spot(0.35, 0.4), rng.uniform(0, math.tau))
        blob(b, spot(0.25, 0.3) + (0,), (0.12, 0.09, 0.14), "bone", "bone", rng, sub=1, squash=0.4, moss_from=-1) if False else None
        s0 = spot(0.25, 0.3); blob(b, (s0[0], TOP+0.06+0.05, s0[1]), (0.12, 0.09, 0.14), "bone", "bone", rng, sub=1, squash=0.4, moss_from=-1)
        pebbles(b, rng, 2, keep)
    else:
        for _ in range(2): toadstool(b, rng, spot(), size=0.8, cap="darkcap", stem="darkcap", spots=False)
        for _ in range(2): twig(b, rng, spot(0.15, 0.2), rng.uniform(0, math.tau), 0.35)
        ft["litter"](b, rng, 8, keep)
    return b.make("Dead Tile %d" % index)

def main():
    scene, cam = ft["setup_scene"]()
    mat = ft["material"](os.path.join(HERE, "TileWorldPalette.png"))
    fung = [fungal(i) for i in range(5)]; dd = [dead(i) for i in range(5)]
    for t in fung + dd: t.data.materials.append(mat); print("TILE %s verts %d faces %d" % (t.name, len(t.data.vertices), len(t.data.polygons)))
    for i,t in enumerate(fung): t.location = ((i-2)*2.6, 1.6, 0)
    for i,t in enumerate(dd): t.location = ((i-2)*2.6, -1.6, 0)
    ft["look"](cam, (0, 1.0, 0), 13, 34, 0); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "dark-lineup.png"))
    for t in fung + dd: t.hide_render = True
    rng = random.Random(41); placed = []
    for gx in range(-4, 4):
        for gz in range(-4, 4):
            src = rng.choice(fung if gz >= 0 else dd)
            ob = bpy.data.objects.new("P", src.data); scene.collection.objects.link(ob)
            ob.location = (gx*2.0+1.0, -(gz*2.0+1.0), rng.choice([0,0,0,0.25])); ob.rotation_euler = (0,0,rng.choice([0,1,2,3])*math.pi/2); placed.append(ob)
    ft["look"](cam, (0, 1.3, 2), 7.5, 12, 55); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "fungal-low.png"))
    ft["look"](cam, (0, 1.3, -3), 7.5, 12, 125); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "dead-low.png"))
    for ob in placed: bpy.data.objects.remove(ob)
    for t in fung + dd:
        t.hide_render = False; t.location = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT'); t.select_set(True); bpy.context.view_layer.objects.active = t
        bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, t.name + ".fbx"), use_selection=True, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL', mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
