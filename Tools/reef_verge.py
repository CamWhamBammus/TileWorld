# The one edge in the world the mixed ground never covered: where a reef floor stops and the
# sand that fringes it begins. That is not a border between countries but a line of depth --
# coral wants a metre of water over it -- so it gets a series of its own, graded by how deep
# the water is rather than by how near a border. Five tiles, sand into coral.
import bpy, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "blend_tiles.py")).read().replace("\nmain()\n", "\n")
bt = {"__file__": os.path.join(HERE, "blend_tiles.py"), "__name__": "blend_tiles"}
exec(compile(src, "blend_tiles.py", "exec"), bt)
ft, Build, blob, prism = bt["ft"], bt["Build"], bt["blob"], bt["prism"]
TOP, BOTTOM, HALF, INNER, GROUND = bt["TOP"], bt["BOTTOM"], bt["HALF"], bt["INNER"], bt["GROUND"]
FAMILY, blend_body = bt["FAMILY"], bt["blend_body"]

# the reef's floor as a family: worn limestone under a coralline crust, which is what the
# reef tiles themselves are made of
FAMILY["coral"] = dict(
    top=["rock3", "pebble", "crust", "rock3", "sandwet"], band="crust", low="rock",
    strew=lambda b, rng, at: bt["flat_bit"](b, rng, at, ["crust", "coralrose", "alpinecrust"], 0.08, 0.14)
                             if rng.random() < 0.55 else bt["lump"](b, rng, at, ["coralpink", "coralteal"], 0.05, 0.09))

def polyp(b, rng, at, size=1.0):
    """A coral head just starting: too small to be a colony, enough to say what the ground is."""
    s = rng.uniform(0.06, 0.11) * size
    blob(b, (at[0], GROUND + s * 0.35, at[1]), (s, s * 0.8, s * 0.9),
         rng.choice(["coralpink", "coralteal", "coralyellow", "coralviolet"]), "coralrose",
         rng, sub=0, squash=0.35, moss_from=-2)

def weed(b, rng, at, size=1.0):
    """Seagrass, a few blades of it, which is the first thing to take a bare patch."""
    x, z = at
    for k in range(5):
        a = k / 5 * math.tau + rng.uniform(-0.3, 0.3)
        h = rng.uniform(0.18, 0.34) * size
        w = 0.028 * size
        base = (x + math.cos(a) * 0.04, GROUND, z + math.sin(a) * 0.04)
        tip = (base[0] + math.cos(a) * 0.10, GROUND + h, base[2] + math.sin(a) * 0.10)
        sx, sz = math.cos(a + math.pi / 2) * w, math.sin(a + math.pi / 2) * w
        col = "seagrass" if k % 2 else "seagrass2"
        b.quad((base[0]-sx,base[1],base[2]-sz), (base[0]+sx,base[1],base[2]+sz),
               (tip[0]+sx*0.3,tip[1],tip[2]+sz*0.3), (tip[0]-sx*0.3,tip[1],tip[2]-sz*0.3), col)
        b.quad((tip[0]-sx*0.3,tip[1],tip[2]-sz*0.3), (tip[0]+sx*0.3,tip[1],tip[2]+sz*0.3),
               (base[0]+sx,base[1],base[2]+sz), (base[0]-sx,base[1],base[2]-sz), "seagrass2")

def tile(step):
    mix = [0.10, 0.30, 0.50, 0.70, 0.90][step]
    rng = random.Random(6400 + step)
    # rock under it rather than earth: this one is a sea bed, and its sides show
    b = Build(); blend_body(b, rng, "sand", "coral", mix, bottom="rockdark")
    keep = []
    def spots(n, margin=0.18, room=0.26):
        out = []
        for _ in range(n):
            for _t in range(30):
                x = rng.uniform(-INNER+margin, INNER-margin); z = rng.uniform(-INNER+margin, INNER-margin)
                if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
            out.append((x, z)); keep.append((x, z, room))
        return out
    for at in spots(max(0, round((1 - mix) * 4))): bt["shell_bit"](b, rng, at)
    for at in spots(max(0, round(mix * 5))):
        if rng.random() < 0.55: polyp(b, rng, at, size=rng.uniform(0.9, 1.4))
        else: weed(b, rng, at, size=rng.uniform(0.9, 1.3))
    return b.make("Reef Verge %d" % step)

def main():
    scene, cam = ft["setup_scene"]()
    mat = ft["material"](os.path.join(HERE, "TileWorldPalette.png"))
    tiles = [tile(k) for k in range(5)]
    for t in tiles:
        t.data.materials.append(mat)
        print("VERGE %s faces %d" % (t.name, len(t.data.polygons)))
    for i, t in enumerate(tiles): t.location = ((i - 2) * 2.6, 0, 0)
    ft["look"](cam, (0, 1.0, 0), 11.5, 30, 0); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "reef-verge.png"))
    for t in tiles:
        t.location = (0, 0, 0); t.rotation_euler = (0, 0, 0)
        bpy.ops.object.select_all(action='DESELECT'); t.select_set(True); bpy.context.view_layer.objects.active = t
        bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, t.name + ".fbx"), use_selection=True,
                                 axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL',
                                 mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
