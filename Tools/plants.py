# The small standing things of Tile World, built in Blender like the tiles: mushrooms and the fungal
# country's big toadstools, boulders, desert stones, and dead trees. Each on its foot at the origin.
# Renders a line-up and exports FBX.
import bpy, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
Build, prism, tube, blob = ft["Build"], ft["prism"], ft["tube"], ft["blob"]
from mathutils import Vector

def cap(b, rng, centre, r, h, sides, colour, rim="gill", spots=None, spot_colour="whitespot", flat=False):
    cx, cy, cz = centre
    ring = [(cx+math.cos(k/sides*math.tau)*r*(1+rng.uniform(-0.05,0.05)), cy, cz+math.sin(k/sides*math.tau)*r*(1+rng.uniform(-0.05,0.05))) for k in range(sides)]
    peak = (cx, cy+h, cz)
    mid = [(cx+math.cos((k+0.5)/sides*math.tau)*r*0.62, cy+h*0.62, cz+math.sin((k+0.5)/sides*math.tau)*r*0.62) for k in range(sides)]
    for k in range(sides):
        a0 = (k+0.5)/sides*math.tau
        # two facets a side, so the cap is domed rather than a cone
        b.tri(ring[k], ring[(k+1)%sides], mid[k], colour, out=(math.cos(a0), 0.5, math.sin(a0)))
        b.tri(mid[k], mid[(k-1)%sides] if False else mid[k], peak, colour) if False else None
        b.tri(mid[k], peak, mid[(k-1)%sides] if False else (cx+math.cos((k-0.5)/sides*math.tau)*r*0.62, cy+h*0.62, cz+math.sin((k-0.5)/sides*math.tau)*r*0.62), colour, out=(math.cos(k/sides*math.tau), 0.9, math.sin(k/sides*math.tau)))
        b.tri(ring[k], mid[k], (cx+math.cos((k-0.5)/sides*math.tau)*r*0.62, cy+h*0.62, cz+math.sin((k-0.5)/sides*math.tau)*r*0.62), colour, out=(math.cos(k/sides*math.tau), 0.5, math.sin(k/sides*math.tau)))
    b.face(ring, rim, out=(0,-1,0))
    if spots:
        for k in range(spots):
            a = k/spots*math.tau + 0.4; d = r*rng.uniform(0.35, 0.7)
            y = cy + h*(1 - (d/r)**1.4)*0.95 + 0.01
            b.face([(cx+math.cos(a)*d+math.cos(a+q/5*math.tau)*r*0.13, y, cz+math.sin(a)*d+math.sin(a+q/5*math.tau)*r*0.13) for q in range(5)], spot_colour, out=(math.cos(a)*0.5, 1, math.sin(a)*0.5))

def mushroom(index):
    rng = random.Random(1300+index); b = Build()
    kinds = [("redcap", 0.42, 0.24, 6, True), ("browncap", 0.34, 0.16, 5, False), ("purplecap", 0.3, 0.3, 4, False), ("bluecap", 0.36, 0.2, 5, True), ("capcream", 0.28, 0.14, 4, False)]
    colour, r, h, spots, dots = kinds[index]
    stem_h = rng.uniform(0.5, 0.8)
    prism(b, (0, 0, 0), 0.09, stem_h, 7, "stem", "stem", taper=0.85, rng=rng, jitter=0.06, cap_bottom=True)
    cap(b, rng, (0, stem_h, 0), r, h, 8, colour, spots=(spots if dots else 0))
    if index == 4:
        # a cluster: two smaller beside it
        for k in range(2):
            a = k*2.6 + 0.8; d = 0.24
            hh = stem_h*rng.uniform(0.45, 0.7)
            prism(b, (math.cos(a)*d, 0, math.sin(a)*d), 0.05, hh, 6, "stem", "stem", taper=0.85, cap_bottom=True)
            cap(b, rng, (math.cos(a)*d, hh, math.sin(a)*d), r*0.55, h*0.6, 7, colour)
    return b.make("Mushroom %d" % index)

def toadstool(index):
    """The fungal country's big ones: a metre and more, a stem you could lean on."""
    rng = random.Random(1350+index); b = Build()
    colour = ["redcap", "purplecap", "bluecap"][index]
    stem_h = rng.uniform(1.3, 1.9)
    pts = [(0,0,0), (rng.uniform(-0.05,0.05), stem_h*0.5, rng.uniform(-0.05,0.05)), (rng.uniform(-0.08,0.08), stem_h, rng.uniform(-0.08,0.08))]
    tube(b, pts, [0.22, 0.17, 0.15], 8, ["stem","stem","gill"], cap_start=True, cap_end=True)
    cap(b, rng, pts[2], rng.uniform(0.8, 1.05), rng.uniform(0.4, 0.55), 10, colour, spots=(6 if index != 1 else 0))
    return b.make("Toadstool %d" % index)

def boulder(index):
    rng = random.Random(1400+index); b = Build()
    size = [(0.7,0.55,0.6), (0.9,0.5,0.8), (0.55,0.6,0.5), (1.0,0.7,0.7)][index]
    blob(b, (0, size[1]*0.55, 0), size, "moss" if index == 3 else "stone", "stone" if index != 2 else "stone2", rng, sub=1 if index < 3 else 2, squash=0.45, moss_from=0.5, patchy=0.4)
    if index == 1:
        # split: a second lump leaning against it
        blob(b, (size[0]*0.8, 0.25, 0.1), (0.35, 0.3, 0.3), "stone", "stone2", rng, sub=1, squash=0.5, moss_from=2)
    return b.make("Boulder %d" % index)

def stone(index):
    rng = random.Random(1450+index); b = Build()
    size = [(0.5,0.35,0.45), (0.35,0.4,0.3), (0.6,0.3,0.4)][index]
    blob(b, (0, size[1]*0.5, 0), size, "sandstone", "sandstone2", rng, sub=1, squash=0.4, moss_from=0.6, patchy=0.5)
    return b.make("Desert Stone %d" % index)

def dead_tree(index):
    rng = random.Random(1500+index); b = Build()
    h = rng.uniform(2.6, 3.6)
    pts=[]; radii=[]
    lean = (rng.uniform(-0.08,0.08), rng.uniform(-0.08,0.08))
    for s in range(5):
        t = s/4
        pts.append((lean[0]*h*t*t + math.sin(t*5)*0.06*t, h*t, lean[1]*h*t*t)); radii.append(0.16 + (0.04-0.16)*t)
    tube(b, pts, radii, 6, ["deadwood","deadwood2","deadwood"], cap_start=True, cap_end=True)
    for k in range(4 + index):
        t = 0.4 + 0.55*k/(4+index); a = k*2.3 + rng.uniform(-0.5,0.5)
        base = Vector(pts[min(4, int(t*4))]) + Vector((0, (t*4 - int(t*4)) * h/4, 0))
        d = Vector((math.cos(a), 0.55 + rng.uniform(-0.2,0.3), math.sin(a))).normalized()
        length = rng.uniform(0.5, 1.1)*(1.2 - t*0.6)
        bp = [tuple(base), tuple(base + d*length*0.5 + Vector((0,0.1*length,0))), tuple(base + d*length + Vector((0,0.25*length,0)))]
        tube(b, bp, [0.06, 0.04, 0.015], 4, ["deadwood2","deadwood"], cap_start=False)
        a2 = a + rng.uniform(-1.2, 1.2); tip = Vector(bp[2])
        tube(b, [tuple(tip), tuple(tip + Vector((math.cos(a2)*0.3, 0.3, math.sin(a2)*0.3)))], [0.015, 0.006], 3, ["deadwood2"], cap_start=False)
    return b.make("Dead Tree %d" % index)

def main():
    scene, cam = ft["setup_scene"]()
    mat = ft["material"](os.path.join(HERE, "TileWorldPalette.png"))
    things = [mushroom(i) for i in range(5)] + [toadstool(i) for i in range(3)] + [boulder(i) for i in range(4)] + [stone(i) for i in range(3)] + [dead_tree(i) for i in range(3)]
    for t in things: t.data.materials.append(mat); print("THING %s verts %d faces %d height %.2f" % (t.name, len(t.data.vertices), len(t.data.polygons), max(v.co.z for v in t.data.vertices)))
    # the line-up in two rows: small things in front, tall behind
    front = things[0:5] + things[8:15]; back = things[5:8] + things[15:18]
    for i,t in enumerate(front): t.location = ((i-5.5)*1.6, 2.5, 0)
    for i,t in enumerate(back): t.location = ((i-2.5)*3.4, -2.5, 0)
    ft["look"](cam, (0, 1.2, 0), 24, 22, 0); cam.data.lens = 35
    ft["render"](os.path.join(HERE, "plants-lineup.png"))
    for t in things:
        t.location = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT'); t.select_set(True); bpy.context.view_layer.objects.active = t
        bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, t.name + ".fbx"), use_selection=True, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL', mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
