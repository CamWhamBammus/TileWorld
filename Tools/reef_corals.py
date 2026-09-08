# The big coral for Tile World's reefs: the pieces that stand up off the floor, planted on the
# reef tiles the way trees are planted on the forest floor. Built from the tile script's own
# parts, at the size of something a diver would swim around. Renders them and exports FBX.
import bpy, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "reef_tiles.py")).read().replace("\nmain()\n", "\n")
rt = {"__file__": os.path.join(HERE, "reef_tiles.py"), "__name__": "reef_tiles"}
exec(compile(src, "reef_tiles.py", "exec"), rt)
TILE_GROUND = rt["GROUND"]    # the tiles put their detail on their top; these stand on their own foot
Build, prism, tube, blob = rt["Build"], rt["prism"], rt["tube"], rt["blob"]
brain, branchy, table, seafan, sponge, anemone, polyps = (
    rt["brain"], rt["branchy"], rt["table"], rt["seafan"], rt["sponge"], rt["anemone"], rt["polyps"])
from mathutils import Vector

def foot(b, rng, radius, colour="rock3", shade="rock"):
    """The lump of old reef rock a colony has grown up out of."""
    blob(b, (0, 0.02, 0), (radius, radius*0.5, radius*0.92), colour, shade, rng, sub=1, squash=0.25, moss_from=0.3, patchy=0.3)

def pillars(b, rng, count, height, colour, tip):
    """Pillar coral: fat blunt columns standing straight up off the rock, knobbly at the top."""
    for k in range(count):
        a = k/count*math.tau + rng.uniform(-0.4, 0.4)
        d = rng.uniform(0.05, 0.26)
        x, z = math.cos(a)*d, math.sin(a)*d
        h = height*rng.uniform(0.62, 1.0)
        r = rng.uniform(0.10, 0.15)
        segs = 4
        pts = [(x + math.sin(t*2.2)*0.05, 0.02 + h*t, z + math.cos(t*1.7)*0.05) for t in [s/segs for s in range(segs+1)]]
        tube(b, pts, [r, r*0.98, r*0.92, r*0.82, r*0.7], 6, [colour], cap_start=False, cap_end=False)
        top = pts[-1]
        blob(b, (top[0], top[1] + r*0.2, top[2]), (r*0.8, r*0.6, r*0.8), tip, colour, rng, sub=0, squash=0.6, moss_from=-2)
        for _ in range(2):
            t = rng.uniform(0.45, 0.8); side = rng.uniform(0, math.tau)
            base = (x + math.sin(t*2.2)*0.05, 0.02 + h*t, z + math.cos(t*1.7)*0.05)
            end = (base[0] + math.cos(side)*r*2.4, base[1] + h*0.22, base[2] + math.sin(side)*r*2.4)
            tube(b, [base, ((base[0]+end[0])/2, (base[1]+end[1])/2 + 0.03, (base[2]+end[2])/2), end],
                 [r*0.6, r*0.5, r*0.38], 5, [colour], cap_start=False, cap_end=False)
            blob(b, (end[0], end[1] + r*0.2, end[2]), (r*0.5, r*0.42, r*0.5), tip, colour, rng, sub=0, squash=0.6, moss_from=-2)

def coral(index):
    rng = random.Random(9000+index)
    b = Build()
    if index == 0:      # a brain coral the size of a boulder
        foot(b, rng, 0.52)
        brain(b, rng, (0, 0), 0.62, 0.46, "coralviolet", "coralplum")
        polyps(b, rng, 5, [(0, 0, 0.55)], colours=("coralpink", "coralteal"))
        name = "Brain Coral"
    elif index == 1:    # a thicket of staghorn
        foot(b, rng, 0.42)
        branchy(b, rng, (0, 0), 2.9, "coralpink", "anemtip", arms=6)
        name = "Staghorn Coral"
    elif index == 2:    # a table held out flat over the floor
        foot(b, rng, 0.34)
        table(b, rng, (0, 0), 0.95, 0.72, "coralteal", "coralteal2")
        polyps(b, rng, 3, [(0, 0, 0.3)])
        name = "Table Coral"
    elif index == 3:    # a fan across the current
        foot(b, rng, 0.30)
        seafan(b, rng, (0, 0), 1.75, "coralorange", "coralamber")
        name = "Sea Fan"
    elif index == 4:    # a barrel sponge
        foot(b, rng, 0.30)
        sponge(b, rng, (0, 0), 0.34, 0.95, "coralrose", "coralplum")
        polyps(b, rng, 3, [(0, 0, 0.4)], colours=("coralyellow", "coralteal"))
        name = "Barrel Sponge"
    else:               # pillar coral, the tallest of them
        foot(b, rng, 0.44)
        pillars(b, rng, 5, 1.5, "coralyellow", "anemtip")
        name = "Pillar Coral"
    return b.make(name)

def run():
    ft = rt["ft"]
    scene, cam = ft["setup_scene"]()
    sun = bpy.data.objects["Sun"]
    mat = ft["material"](os.path.join(HERE, "TileWorldPalette.png"))
    rt["GROUND"] = 0.0
    corals = [coral(i) for i in range(6)]
    rt["GROUND"] = TILE_GROUND
    for c in corals:
        c.data.materials.append(mat)
        print("CORAL %-16s verts %4d faces %4d height %.2f" % (c.name, len(c.data.vertices), len(c.data.polygons), max(v.co.z for v in c.data.vertices)))

    xs = [-4.6, -2.6, -0.4, 1.7, 3.5, 5.4]
    for c, x in zip(corals, xs): c.location = (x, 0, 0)
    ft["look"](cam, (0.4, 0.9, 0), 12.5, 12, 0); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "coral-lineup.png"))

    # the reef itself: the tiles on the grid with coral stood on some of them
    tiles = [rt["tile"](i) for i in range(5)]
    for t in tiles: t.data.materials.append(mat); t.hide_render = True
    for c in corals: c.hide_render = True
    rng = random.Random(5); placed = []
    for gx in range(-5, 5):
        for gz in range(-5, 5):
            ob = bpy.data.objects.new("P", rng.choice(tiles).data); scene.collection.objects.link(ob)
            lift = rng.choice([0, 0, 0, 0.25, -0.25, 0.5])
            ob.location = (gx*2.0+1.0, -(gz*2.0+1.0), lift); ob.rotation_euler = (0, 0, rng.choice([0,1,2,3])*math.pi/2)
            placed.append(ob)
            if rng.random() < 0.30:
                c = rng.choice(corals)
                cc = bpy.data.objects.new("C", c.data); scene.collection.objects.link(cc)
                cc.location = (ob.location.x + rng.uniform(-0.45,0.45), ob.location.y + rng.uniform(-0.45,0.45), lift + 1.05)
                cc.rotation_euler = (0, 0, rng.uniform(0, math.tau))
                s = rng.uniform(0.75, 1.15); cc.scale = (s, s, s)
                placed.append(cc)
    rt["sea"](scene, sun, True)
    ft["look"](cam, (0, 1.6, 0), 17, 18, 26); cam.data.lens = 38
    ft["render"](os.path.join(HERE, "reef-scene.png"))
    ft["look"](cam, (0, 1.6, 2), 9.0, 8, 55); cam.data.lens = 42
    ft["render"](os.path.join(HERE, "reef-scene-low.png"))
    rt["sea"](scene, sun, False)
    ft["look"](cam, (0, 1.6, 0), 17, 18, 26); cam.data.lens = 38
    ft["render"](os.path.join(HERE, "reef-scene-dry.png"))
    for ob in placed: bpy.data.objects.remove(ob)

    for c in corals:
        c.hide_render = False; c.location = (0,0,0); c.rotation_euler = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT'); c.select_set(True); bpy.context.view_layer.objects.active = c
        bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, c.name + ".fbx"), use_selection=True,
                                 axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL',
                                 mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

run()
