# Jungle trees for Tile World: the giants, mostly. A kapok that stands sixteen metres on buttress
# roots with an umbrella crown over everything, a smaller jungle tree under it, a strangler fig, a
# tree fern, a stand of bamboo and a seedling -- each on its own foot at the origin, flat shaded,
# coloured off the one palette. Renders them and exports FBX.
import bpy, bmesh, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
Build, prism, tube, blob = ft["Build"], ft["prism"], ft["tube"], ft["blob"]
from mathutils import Vector

def leaves(b, rng, centre, radius, sub=1, flat=1.0, top="jungle1", side="jungle2", under="jungle3", lit="junglelit"):
    """A mass of leaf: a lump lit on top, dark underneath, with a few faces caught by the sun."""
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=1.0)
    for v in bm.verts:
        v.co.x *= radius*(1+rng.uniform(-0.18,0.18))
        v.co.y *= radius*flat*(1+rng.uniform(-0.14,0.14))
        v.co.z *= radius*(1+rng.uniform(-0.18,0.18))
    for f in bm.faces:
        pts = [(centre[0]+v.co.x, centre[1]+v.co.y, centre[2]+v.co.z) for v in f.verts]
        n = f.normal
        if n.y > 0.5: col = lit if rng.random() < 0.28 else top
        elif n.y < -0.3: col = under
        else: col = side if rng.random() < 0.75 else top
        b.face(pts, col)
    bm.free()

def bole(b, rng, height, base_r, top_r, sides=7, colours=("liana", "liana", "bark2"), lean=(0, 0), wobble=0.10):
    """A trunk: a tube from the foot up, thinner as it goes, wandering a little."""
    segs = 5
    pts = []; radii = []
    for si in range(segs+1):
        t = si/segs
        pts.append((lean[0]*height*t*t + math.sin(t*4.0 + rng.random())*wobble*t,
                    height*t,
                    lean[1]*height*t*t + math.cos(t*3.0)*wobble*t))
        radii.append(base_r + (top_r - base_r)*t)
    tube(b, pts, radii, sides, list(colours), cap_start=True, cap_end=True)
    return pts

def buttress(b, rng, angle, length, height, thick=0.16, colour="liana", shade="bark2"):
    """A buttress root: a fin of wood off the foot of a trunk, tall at the tree and gone at its end."""
    ax, az = math.cos(angle), math.sin(angle)
    px, pz = math.cos(angle+math.pi/2), math.sin(angle+math.pi/2)
    steps = 5; prev = None
    for si in range(steps+1):
        t = si/steps
        w = thick*(1 - t*0.85); h = height*(1 - t)**1.2
        cx, cz = ax*length*t, az*length*t
        ring = [(cx + px*w, -0.05, cz + pz*w), (cx - px*w, -0.05, cz - pz*w),
                (cx - px*w*0.45, h, cz - pz*w*0.45), (cx + px*w*0.45, h, cz + pz*w*0.45)]
        if prev is not None:
            for k in range(4):
                a0, b0 = prev[k], prev[(k+1)%4]
                a1, b1 = ring[k], ring[(k+1)%4]
                mid = (Vector(a0)+Vector(b0)+Vector(a1)+Vector(b1))/4
                b.quad(a0, b0, b1, a1, colour if k != 2 else shade, out=tuple(mid - Vector((cx, mid.y, cz))))
        prev = ring
    b.face(list(reversed(prev)), shade, out=(ax, 0, az))

def limb(b, rng, start, angle, rise, length, radius, colour="liana"):
    """A branch out of the trunk, lifting as it goes."""
    pts = []; radii = []
    for si in range(4):
        t = si/3
        pts.append((start[0] + math.cos(angle)*length*t,
                    start[1] + rise*length*(t*0.75 + t*t*0.25),
                    start[2] + math.sin(angle)*length*t))
        radii.append(radius*(1 - 0.6*t))
    tube(b, pts, radii, 5, [colour, colour, "bark2"], cap_start=False, cap_end=False)
    return pts[-1]

def liana(b, rng, at, drop, leafy=True):
    """A vine hanging off a branch: a thin line down through the air with leaves along it."""
    x, y, z = at
    segs = 4
    sway = rng.uniform(-0.35, 0.35); across = rng.uniform(0, math.tau)
    pts = [(x + math.cos(across)*sway*(si/segs)**2, y - drop*si/segs, z + math.sin(across)*sway*(si/segs)**2) for si in range(segs+1)]
    tube(b, pts, [0.055, 0.050, 0.044, 0.038, 0.028], 4, ["vine", "jungle3"], cap_start=False, cap_end=True)
    if not leafy: return
    # One leaf a segment rather than two, and fewer of those. A giant carried eighteen vines
    # with up to eight leaves each, two-sided: nearly three hundred faces that do nothing for
    # its shape and cost more than its crown.
    for si in range(1, segs+1):
        for _ in range(1):
            if rng.random() > 0.62: continue
            t = (si - rng.random())/segs
            p = (x + math.cos(across)*sway*t*t, y - drop*t, z + math.sin(across)*sway*t*t)
            a = rng.uniform(0, math.tau); L = rng.uniform(0.28, 0.48)
            tip = (p[0] + math.cos(a)*L, p[1] - L*0.45, p[2] + math.sin(a)*L)
            w = 0.13
            sx, sz = math.cos(a+math.pi/2)*w, math.sin(a+math.pi/2)*w
            q = [(p[0]-sx*0.25, p[1], p[2]-sz*0.25), (p[0]+sx*0.25, p[1], p[2]+sz*0.25),
                 (tip[0]+sx, tip[1], tip[2]+sz), (tip[0]-sx, tip[1], tip[2]-sz)]
            b.quad(*q, "vine" if rng.random() < 0.7 else "jungle1"); b.quad(q[3], q[2], q[1], q[0], "jungle3")

def fronds(b, rng, at, count, length, colour="frond"):
    """A crown of big ribbed leaves, arching out and down."""
    x, y, z = at
    for k in range(count):
        a = k/count*math.tau + rng.uniform(-0.2, 0.2)
        L = length*rng.uniform(0.82, 1.12); w = 0.20*L/1.4
        segs = 4; pts = []
        for si in range(segs+1):
            t = si/segs
            pts.append((x + math.cos(a)*L*t, y + L*(0.42*math.sin(t*2.1) - 0.55*t*t*t), z + math.sin(a)*L*t))
        for si in range(segs):
            p0, p1 = pts[si], pts[si+1]
            t0 = 1 - si/segs*0.5; t1 = 1 - (si+1)/segs*0.72
            sx, sz = math.cos(a+math.pi/2)*w, math.sin(a+math.pi/2)*w
            q = [(p0[0]-sx*t0,p0[1],p0[2]-sz*t0), (p0[0]+sx*t0,p0[1],p0[2]+sz*t0),
                 (p1[0]+sx*t1,p1[1],p1[2]+sz*t1), (p1[0]-sx*t1,p1[1],p1[2]-sz*t1)]
            b.quad(*q, colour if si < 2 else "jungle2"); b.quad(q[3], q[2], q[1], q[0], "jungle3")

def tree(index):
    rng = random.Random(2000+index)
    b = Build()

    if index in (0, 1):
        # The giants. One stands clear of everything; the other is the one under it.
        big = index == 0
        height = rng.uniform(15.5, 17.5) if big else rng.uniform(10.0, 12.0)
        base_r = 0.62 if big else 0.40
        top_r = 0.30 if big else 0.22
        roots = 6 if big else 4
        for k in range(roots):
            a = k/roots*math.tau + rng.uniform(-0.25, 0.25)
            buttress(b, rng, a, rng.uniform(2.0, 2.9) if big else rng.uniform(1.1, 1.5),
                     rng.uniform(2.6, 3.6) if big else rng.uniform(1.3, 1.9),
                     thick=0.30 if big else 0.19)
        spine = bole(b, rng, height, base_r, top_r, sides=8 if big else 7,
                     lean=(rng.uniform(-0.02, 0.02), rng.uniform(-0.02, 0.02)), wobble=0.12)
        crown = spine[-1]

        # the umbrella: branches out near the top, then a wide flat mass of leaf over them
        arms = 6 if big else 5
        tips = []
        for k in range(arms):
            a = k/arms*math.tau + rng.uniform(-0.3, 0.3)
            at = (crown[0], crown[1] - rng.uniform(0.4, 2.0), crown[2])
            tips.append(limb(b, rng, at, a, rng.uniform(0.30, 0.55),
                             rng.uniform(2.6, 4.2) if big else rng.uniform(1.7, 2.6),
                             0.16 if big else 0.11))
        spread = 4.2 if big else 2.7
        leaves(b, rng, (crown[0], crown[1] + (1.0 if big else 0.7), crown[2]), spread, sub=2, flat=0.62)
        for k in range(4 if big else 3):
            a = k/4*math.tau + 0.5
            d = spread*rng.uniform(0.45, 0.75)
            leaves(b, rng, (crown[0] + math.cos(a)*d, crown[1] + rng.uniform(0.4, 1.6), crown[2] + math.sin(a)*d),
                   spread*rng.uniform(0.42, 0.60), sub=1, flat=0.70)
        for t in tips:
            leaves(b, rng, (t[0], t[1] + 0.5, t[2]), (2.2 if big else 1.5)*rng.uniform(0.85, 1.15), sub=1, flat=0.72)
        # a second, smaller tier lower down, so the shape is not one disc
        if big:
            for k in range(3):
                a = k/3*math.tau + 0.7
                at = (crown[0], height*rng.uniform(0.55, 0.68), crown[2])
                t = limb(b, rng, at, a, 0.35, rng.uniform(1.8, 2.6), 0.11)
                leaves(b, rng, (t[0], t[1] + 0.4, t[2]), rng.uniform(1.3, 1.8), sub=1, flat=0.6)

        for t in tips:
            for _ in range(2 if big else 1):
                liana(b, rng, (t[0] + rng.uniform(-1.0, 1.0), t[1] - 0.5, t[2] + rng.uniform(-1.0, 1.0)),
                      rng.uniform(2.5, 6.0) if big else rng.uniform(1.8, 3.5))
        name = "Jungle Giant" if big else "Jungle Tree"

    elif index == 2:
        # a strangler: a host trunk inside a cage of roots run down it
        height = rng.uniform(8.5, 10.5)
        spine = bole(b, rng, height, 0.34, 0.20, sides=6, colours=("bark2", "liana", "bark2"), wobble=0.16)
        for k in range(7):
            a = k/7*math.tau + rng.uniform(-0.2, 0.2)
            pts = []
            for si in range(5):
                t = si/4
                r = 0.40 + 0.16*math.sin(t*5.0 + k)
                y = height*0.78*(1 - t)
                pts.append((math.cos(a + t*0.9)*r*(1 + t*0.5), y, math.sin(a + t*0.9)*r*(1 + t*0.5)))
            tube(b, pts, [0.07, 0.075, 0.08, 0.09, 0.11], 4, ["liana", "vine"], cap_start=False, cap_end=False)
        crown = spine[-1]
        for k in range(5):
            a = k/5*math.tau + rng.uniform(-0.3, 0.3)
            t = limb(b, rng, (crown[0], crown[1] - rng.uniform(0.3, 1.4), crown[2]), a, 0.45, rng.uniform(1.5, 2.3), 0.10)
            leaves(b, rng, (t[0], t[1] + 0.45, t[2]), rng.uniform(1.4, 1.9), sub=1, flat=0.6)
        leaves(b, rng, (crown[0], crown[1] + 0.7, crown[2]), 2.4, sub=2, flat=0.5)
        for _ in range(5):
            liana(b, rng, (rng.uniform(-1.6, 1.6), height*rng.uniform(0.62, 0.85), rng.uniform(-1.6, 1.6)), rng.uniform(1.8, 4.0))
        name = "Strangler Fig"

    elif index == 3:
        # a tree fern: a slim scaly stem under a crown of fronds
        height = rng.uniform(3.0, 4.2)
        spine = bole(b, rng, height, 0.14, 0.10, sides=6, colours=("liana", "bark2", "liana"), wobble=0.05)
        top = spine[-1]
        fronds(b, rng, (top[0], top[1], top[2]), 9, rng.uniform(1.5, 1.9))
        fronds(b, rng, (top[0], top[1] - 0.25, top[2]), 5, rng.uniform(1.0, 1.3), colour="jungle2")
        name = "Tree Fern"

    elif index == 4:
        # a stand of bamboo: jointed canes, leaves only near the top
        for k in range(9):
            a = rng.uniform(0, math.tau); d = rng.uniform(0.05, 0.55)
            x, z = math.cos(a)*d, math.sin(a)*d
            h = rng.uniform(4.5, 7.5); r = rng.uniform(0.055, 0.085)
            joints = max(4, int(h / 0.9))
            lean = (rng.uniform(-0.05, 0.05), rng.uniform(-0.05, 0.05))
            for j in range(joints):
                y0 = h*j/joints; y1 = h*(j+1)/joints
                p0 = (x + lean[0]*y0*y0*0.2, y0, z + lean[1]*y0*y0*0.2)
                p1 = (x + lean[0]*y1*y1*0.2, y1, z + lean[1]*y1*y1*0.2)
                ft["cylinder_along"](b, p0, p1, r*(1 - 0.35*j/joints), 5,
                                     "bamboo" if j % 2 else "bamboo2", "bamboo2", rng=rng, jitter=0.04)
            for _ in range(5):
                t = rng.uniform(0.55, 1.0)
                at = (x + lean[0]*(h*t)**2*0.2, h*t, z + lean[1]*(h*t)**2*0.2)
                aa = rng.uniform(0, math.tau); L = rng.uniform(0.35, 0.6); w = 0.05
                tip = (at[0] + math.cos(aa)*L, at[1] + L*rng.uniform(-0.2, 0.35), at[2] + math.sin(aa)*L)
                sx, sz = math.cos(aa+math.pi/2)*w, math.sin(aa+math.pi/2)*w
                q = [(at[0]-sx*0.3, at[1], at[2]-sz*0.3), (at[0]+sx*0.3, at[1], at[2]+sz*0.3),
                     (tip[0]+sx*0.2, tip[1], tip[2]+sz*0.2), (tip[0]-sx*0.2, tip[1], tip[2]-sz*0.2)]
                b.quad(*q, "bamboo"); b.quad(q[3], q[2], q[1], q[0], "bamboo2")
        name = "Bamboo"

    else:
        # a seedling on the floor, waiting for a gap in the canopy
        height = rng.uniform(1.6, 2.3)
        spine = bole(b, rng, height, 0.07, 0.045, sides=5, colours=("jungle2", "liana"), wobble=0.06)
        top = spine[-1]
        fronds(b, rng, (top[0], top[1], top[2]), 6, rng.uniform(0.7, 0.95))
        leaves(b, rng, (top[0], top[1] + 0.2, top[2]), 0.5, sub=1, flat=0.7)
        name = "Jungle Sapling"

    return b.make(name)

def main():
    scene, cam = ft["setup_scene"]()
    mat = ft["material"](os.path.join(HERE, "TileWorldPalette.png"))
    trees = [tree(i) for i in range(6)]
    for t in trees:
        t.data.materials.append(mat)
        print("TREE %-16s verts %5d faces %5d height %.1f" % (t.name, len(t.data.vertices), len(t.data.polygons), max(v.co.z for v in t.data.vertices)))

    xs = [-13.5, -5.0, 1.5, 7.0, 10.5, 13.5]
    for t, x in zip(trees, xs): t.location = (x, 0, 0)
    ft["look"](cam, (-1.0, 9.0, 0), 46, 10, 0); cam.data.lens = 38
    ft["render"](os.path.join(HERE, "jungle-trees.png"))

    # the wood: the floor tiles with the trees standing on them
    jt = {"__file__": os.path.join(HERE, "jungle_tiles.py"), "__name__": "jungle_tiles"}
    exec(compile(open(os.path.join(HERE, "jungle_tiles.py")).read().replace("\nmain()\n", "\n"), "jungle_tiles.py", "exec"), jt)
    tiles = [jt["tile"](i) for i in range(5)]
    for t in tiles: t.data.materials.append(mat); t.hide_render = True
    for t in trees: t.hide_render = True
    rng = random.Random(19); placed = []
    for gx in range(-7, 7):
        for gz in range(-7, 7):
            ob = bpy.data.objects.new("P", rng.choice(tiles).data); scene.collection.objects.link(ob)
            lift = rng.choice([0, 0, 0, 0.25, -0.25])
            ob.location = (gx*2.0+1.0, -(gz*2.0+1.0), lift)
            ob.rotation_euler = (0, 0, rng.choice([0,1,2,3])*math.pi/2)
            placed.append(ob)
            roll = rng.random()
            which = 0 if roll < 0.05 else 1 if roll < 0.16 else 2 if roll < 0.22 else 3 if roll < 0.34 else 4 if roll < 0.42 else 5 if roll < 0.60 else -1
            if which >= 0:
                tr = bpy.data.objects.new("T", trees[which].data); scene.collection.objects.link(tr)
                tr.location = (ob.location.x + rng.uniform(-0.5, 0.5), ob.location.y + rng.uniform(-0.5, 0.5), lift + 1.05)
                tr.rotation_euler = (0, 0, rng.uniform(0, math.tau))
                s = rng.uniform(0.85, 1.15); tr.scale = (s, s, s)
                placed.append(tr)
    ft["look"](cam, (0, 6.0, 0), 34, 16, 26); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "jungle-wood.png"))
    ft["look"](cam, (0, 2.0, 2), 13, 4, 55); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "jungle-under.png"))
    for ob in placed: bpy.data.objects.remove(ob)

    for t in trees:
        t.hide_render = False; t.location = (0,0,0); t.rotation_euler = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT'); t.select_set(True); bpy.context.view_layer.objects.active = t
        bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, t.name + ".fbx"), use_selection=True,
                                 axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL',
                                 mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
