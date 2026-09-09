# A deer for Tile World, modelled and rigged in Blender rather than grown in code: one mesh, a
# skeleton under it, and five animations keyed on the bones -- standing, walking, running,
# grazing, and a stag bellowing. Renders a turntable and a clip of each, and exports FBX with
# the actions baked in.
#
# Built in Blender's own axes for once (+X right, +Y forward, +Z up), because a skeleton is
# easier to reason about in the space it will be posed in.
import bpy, bmesh, math, json, os, sys
from mathutils import Vector, Euler

HERE = os.path.dirname(os.path.abspath(__file__))
PAL = json.load(open(os.path.join(HERE, "palette.json")))
FRAMES = os.environ.get("DEER_FRAMES", os.path.join(HERE, ".frames"))

# ---------------------------------------------------------------- the skeleton
# head, tail, parent. Bones point head to tail; a leg bone points down, so turning
# it about X swings the leg forward and back.
SIDE = 0.155
BONES = [
    ("root",     (0, 0, 0),            (0, 0.25, 0),         None),
    ("hips",     (0, -0.50, 1.02),     (0, -0.14, 1.06),     "root"),
    ("spine",    (0, -0.14, 1.06),     (0, 0.26, 1.07),      "hips"),
    ("chest",    (0, 0.26, 1.07),      (0, 0.52, 1.09),      "spine"),
    ("neck",     (0, 0.52, 1.09),      (0, 0.84, 1.52),      "chest"),
    ("head",     (0, 0.84, 1.52),      (0, 1.14, 1.45),      "neck"),
    ("jaw",      (0, 0.95, 1.44),      (0, 1.13, 1.39),      "head"),
    ("ear_L",    ( 0.085, 0.88, 1.55), ( 0.20, 0.82, 1.68),  "head"),
    ("ear_R",    (-0.085, 0.88, 1.55), (-0.20, 0.82, 1.68),  "head"),
    ("tail",     (0, -0.58, 1.05),     (0, -0.70, 0.90),     "hips"),
]
for s, x in (("L", SIDE), ("R", -SIDE)):
    BONES += [
        ("thighF_" + s, (x*1.02, 0.38, 1.02), (x*1.02, 0.34, 0.60), "chest"),
        ("shinF_" + s,  (x*1.02, 0.34, 0.60), (x*1.02, 0.38, 0.24), "thighF_" + s),
        ("hoofF_" + s,  (x*1.02, 0.38, 0.24), (x*1.02, 0.37, 0.00), "shinF_" + s),
        ("thighB_" + s, (x*0.98, -0.42, 1.02), (x*0.98, -0.28, 0.62), "hips"),
        ("shinB_" + s,  (x*0.98, -0.28, 0.62), (x*0.98, -0.46, 0.34), "thighB_" + s),
        ("hoofB_" + s,  (x*0.98, -0.46, 0.34), (x*0.98, -0.44, 0.00), "shinB_" + s),
    ]

# ---------------------------------------------------------------- the mesh
class Hide:
    """Faces with a palette colour and a bone each, so the skin is rigid: every vertex belongs
    to exactly one bone. On a faceted animal that is what you want -- smooth weights only blur
    the facets, and the whole look is the facets."""
    def __init__(self): self.verts = []; self.faces = []; self.uvs = []; self.bones = []
    def face(self, pts, colour, bone, out=None):
        pts = [Vector(p) for p in pts]
        if out is not None:
            n = (pts[1]-pts[0]).cross(pts[2]-pts[0])
            if n.dot(Vector(out)) < 0: pts = list(reversed(pts))
        base = len(self.verts)
        self.verts.extend(pts); self.bones.extend([bone]*len(pts))
        self.faces.append(tuple(range(base, base + len(pts))))
        self.uvs.append(PAL[colour])
    def quad(self, a, b, c, d, colour, bone, out=None): self.face([a,b,c,d], colour, bone, out)
    def tri(self, a, b, c, colour, bone, out=None): self.face([a,b,c], colour, bone, out)

def ring(centre, radius, sides, axis, up=(0,0,1), squash=1.0):
    """A ring of points round a point, across the given axis."""
    a = Vector(axis).normalized()
    u = a.cross(Vector(up))
    if u.length < 1e-4: u = a.cross(Vector((1,0,0)))
    u.normalize(); v = a.cross(u).normalized()
    return [tuple(Vector(centre) + (u*math.cos(k/sides*math.tau)*radius + v*math.sin(k/sides*math.tau)*radius*squash)) for k in range(sides)]

def tube(h, points, radii, sides, colour, bones, squash=1.0, cap_start=True, cap_end=True, ends=None, under=None):
    """A limb or a body: rings along a line of points, each ring's vertices owned by one bone."""
    rings = []
    for i, p in enumerate(points):
        nxt = points[min(i+1, len(points)-1)]; prv = points[max(i-1, 0)]
        axis = Vector(nxt) - Vector(prv)
        if axis.length < 1e-5: axis = Vector((0, 0, 1))
        rings.append(ring(p, radii[i], sides, axis, squash=squash))
    for i in range(len(points)-1):
        for k in range(sides):
            a, b = rings[i][k], rings[i][(k+1)%sides]
            c, d = rings[i+1][(k+1)%sides], rings[i+1][k]
            mid = (Vector(a)+Vector(b)+Vector(c)+Vector(d))/4
            spine = (Vector(points[i]) + Vector(points[i+1]))/2
            # the pale underside is the same barrel, not a plate laid under it: a plate shows
            # its edge from the side and reads as something stuck on
            paler = under is not None and (mid - spine).z < -radii[i]*0.35
            h.quad(a, b, c, d, under if paler else colour, bones[i], out=tuple(mid - spine))
    if cap_start: h.face(rings[0], ends or colour, bones[0], out=tuple(Vector(points[0]) - Vector(points[1])))
    if cap_end: h.face(rings[-1], ends or colour, bones[-1], out=tuple(Vector(points[-1]) - Vector(points[-2])))
    return rings

def leg(h, front, side):
    """One leg, three segments, each owned by its own bone and tapering as it goes down."""
    s = "L" if side > 0 else "R"
    tag = "F" if front else "B"
    joints = [b for b in BONES if b[0].endswith("_" + s) and b[0][-3] == tag or (b[0].startswith(("thigh","shin","hoof")) and b[0].endswith(tag + "_" + s))]
    names = [n % (tag, s) for n in ("thigh%s_%s", "shin%s_%s", "hoof%s_%s")]
    pts = []
    for n in names:
        head = next(b[1] for b in BONES if b[0] == n)
        pts.append(head)
    pts.append(next(b[2] for b in BONES if b[0] == names[-1]))
    radii = [0.105, 0.070, 0.045, 0.035] if front else [0.125, 0.078, 0.046, 0.036]
    tube(h, pts, radii, 6, "hide", [names[0], names[1], names[2], names[2]], cap_start=False, cap_end=False)
    # the hoof itself, dark and blunt, and standing on the ground rather than through it
    top = (pts[3][0], pts[3][1], pts[3][2] + 0.075)
    tube(h, [top, (pts[3][0], pts[3][1] + 0.015, pts[3][2] + 0.005)], [0.040, 0.032], 6, "hoofdark", [names[2], names[2]])

def build():
    h = Hide()
    # ---- body: a barrel from rump to chest, deepest at the shoulder
    spine_pts = [(0, -0.62, 1.00), (0, -0.42, 1.06), (0, -0.14, 1.07), (0, 0.16, 1.07), (0, 0.42, 1.08), (0, 0.58, 1.06)]
    spine_r   = [0.150, 0.215, 0.225, 0.220, 0.205, 0.155]
    spine_b   = ["hips", "hips", "spine", "spine", "chest", "chest"]
    tube(h, spine_pts, spine_r, 8, "hide", spine_b, squash=1.16, ends="hide2", under="belly")
    # ---- neck and head
    neck = next(b for b in BONES if b[0] == "neck"); head = next(b for b in BONES if b[0] == "head")
    tube(h, [neck[1], ((neck[1][0]+neck[2][0])/2, (neck[1][1]+neck[2][1])/2, (neck[1][2]+neck[2][2])/2), neck[2]],
         [0.155, 0.120, 0.098], 7, "hide", ["chest", "neck", "neck"], cap_start=False, cap_end=False)
    H = Vector(head[1]); T = Vector(head[2]); along = T - H
    tube(h, [tuple(H), tuple(H + along*0.35), tuple(H + along*0.72), tuple(T)],
         [0.100, 0.092, 0.062, 0.042], 6, "hide", ["head"]*4, cap_start=False, cap_end=True, ends="hoofdark")
    # jaw, so the mouth can open when it calls
    jaw = next(b for b in BONES if b[0] == "jaw")
    tube(h, [jaw[1], jaw[2]], [0.070, 0.040], 5, "hide2", ["jaw", "jaw"], cap_end=True)
    # eyes
    eye = H + along*0.40 + Vector((0, 0, 0.018))
    for x in (0.072, -0.072):
        h.face([(x, eye.y - 0.020, eye.z + 0.012), (x, eye.y + 0.020, eye.z + 0.006),
                (x, eye.y + 0.014, eye.z - 0.024), (x, eye.y - 0.024, eye.z - 0.018)], "hoofdark", "head", out=(x, 0.3, 0))
    # ears
    for name in ("ear_L", "ear_R"):
        b = next(x for x in BONES if x[0] == name)
        base, tip = Vector(b[1]), Vector(b[2])
        across = (tip - base).cross(Vector((0, 1, 0))).normalized() * 0.055
        for s in (1, -1):
            h.tri(tuple(base + across*s), tuple(base - across*s*0.2), tuple(tip), "hide", name)
            h.tri(tuple(base - across*s*0.2), tuple(base + across*s), tuple(tip), "belly", name)
    # antlers: a beam off each side with three tines
    for name, x in (("head", 0.075), ("head", -0.075)):
        base = H + along*0.16 + Vector((x, 0, 0.070))
        beam = [base, base + Vector((x*1.5, -0.12, 0.18)), base + Vector((x*2.1, -0.22, 0.40)),
                base + Vector((x*2.3, -0.16, 0.60))]
        tube(h, [tuple(p) for p in beam], [0.030, 0.024, 0.019, 0.012], 5, "antler", ["head"]*4)
        for t, reach in ((1, 0.18), (2, 0.22)):
            tip = beam[t] + Vector((x*0.7, 0.20, reach))
            tube(h, [tuple(beam[t]), tuple(tip)], [0.018, 0.008], 4, "antler", ["head", "head"])
    # tail
    tl = next(b for b in BONES if b[0] == "tail")
    tube(h, [tl[1], tl[2]], [0.060, 0.028], 5, "hide2", ["tail", "tail"], cap_end=True, ends="belly")
    # legs
    for front in (True, False):
        for side in (1, -1): leg(h, front, side)
    return h

# ---------------------------------------------------------------- rig and skin
def make(h, name="Deer"):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in h.verts], [], h.faces)
    me.update()
    uv = me.uv_layers.new(name="UVMap")
    for poly, colour in zip(me.polygons, h.uvs):
        for li in poly.loop_indices: uv.data[li].uv = colour
    for p in me.polygons: p.use_smooth = False
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)

    arm_data = bpy.data.armatures.new("DeerRig")
    arm = bpy.data.objects.new("DeerRig", arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    made = {}
    for bname, headp, tailp, parent in BONES:
        eb = arm_data.edit_bones.new(bname)
        eb.head = headp; eb.tail = tailp
        eb.use_connect = False
        made[bname] = eb
    for bname, _, _, parent in BONES:
        if parent: made[bname].parent = made[parent]
    bpy.ops.object.mode_set(mode='OBJECT')

    for bname, _, _, _ in BONES: ob.vertex_groups.new(name=bname)
    for i, bname in enumerate(h.bones):
        ob.vertex_groups[bname].add([i], 1.0, 'REPLACE')
    ob.parent = arm
    mod = ob.modifiers.new("Armature", 'ARMATURE'); mod.object = arm
    for pb in arm.pose.bones: pb.rotation_mode = 'XYZ'
    return ob, arm

# ---------------------------------------------------------------- the animations
def key(arm, bone, frame, rx=0.0, ry=0.0, rz=0.0, loc=None):
    pb = arm.pose.bones[bone]
    pb.rotation_euler = Euler((math.radians(rx), math.radians(ry), math.radians(rz)), 'XYZ')
    pb.keyframe_insert("rotation_euler", frame=frame)
    if loc is not None:
        pb.location = Vector(loc)
        pb.keyframe_insert("location", frame=frame)

def action(arm, name):
    act = bpy.data.actions.new(name)
    if arm.animation_data is None: arm.animation_data_create()
    arm.animation_data.action = act
    return act

def cycle(arm, name, frames, pose):
    """Keys every frame from a function of the phase, which is the simplest thing that works
    and costs nothing at export: the FBX is baked anyway. Blender 5 moved f-curves inside
    action layers and slots, so nothing here touches them -- the default interpolation is what
    is wanted and every frame is keyed regardless."""
    act = action(arm, name)
    for f in range(frames + 1):
        pose(f / frames, f + 1)
    return act

def walk_pose(arm, t, f, stride=26.0, knee=30.0, bounce=0.035, duty=0.62, bound=False, lean=0.0):
    """A gait, built the way a gait actually works rather than as a sine.

    A leg has two halves. In stance the hoof is on the ground and the body travels over it, so
    the thigh turns back at a steady rate and the joints stay straight. In swing the leg comes
    off and is thrown forward in the time that is left, folding at the joint on the way so the
    hoof clears the ground. Swinging a leg with one sine does neither: the foot floats through
    the middle of the stride and the whole thing skates."""
    def one(tag, side, phase, fold_sign):
        a = (t + phase) % 1.0
        if a < duty:
            u = a / duty
            hip = stride * (1.0 - 2.0 * u)      # planted: turns back at a steady rate
            fold = 0.0
            lift = 0.0
        else:
            u = (a - duty) / (1.0 - duty)
            hip = -stride + 2.0 * stride * u    # thrown forward
            fold = math.sin(u * math.pi) * knee
            lift = math.sin(u * math.pi)
        key(arm, "thigh%s_%s" % (tag, side), f, rx=hip)
        key(arm, "shin%s_%s" % (tag, side), f, rx=fold * fold_sign)
        key(arm, "hoof%s_%s" % (tag, side), f, rx=-fold * fold_sign * 0.5 + lift * 6.0)
        return lift
    # a deer folds its front leg back at the knee and its hind leg forward at the hock
    if bound:
        # both front together, then both hind together, which is what a deer does at speed
        one("F", "L", 0.00, -1.0); one("F", "R", 0.04, -1.0)
        one("B", "L", 0.42,  1.0); one("B", "R", 0.46,  1.0)
    else:
        one("F", "L", 0.00, -1.0); one("B", "R", 0.10, 1.0)
        one("F", "R", 0.50, -1.0); one("B", "L", 0.60, 1.0)
    rise = math.sin(t * math.tau * (1 if bound else 2)) * bounce
    key(arm, "root", f, rx=lean, loc=(0, 0, rise))
    key(arm, "spine", f, rx=math.sin(t * math.tau * (1 if bound else 2)) * (7.0 if bound else 2.0))
    key(arm, "chest", f, rx=-math.sin(t * math.tau * (1 if bound else 2)) * (9.0 if bound else 2.5))
    key(arm, "neck", f, rx=6.0 * math.sin(t * math.tau) - 4.0)
    key(arm, "head", f, rx=-5.0 * math.sin(t * math.tau) + 3.0)
    key(arm, "tail", f, ry=math.sin(t * math.tau * 2) * 8.0)
    key(arm, "ear_L", f, rz=math.sin(t * math.tau * 3) * 5.0)
    key(arm, "ear_R", f, rz=-math.sin(t * math.tau * 3 + 1.0) * 5.0)

def animations(arm):
    made = []

    # standing: breathing, an ear going, the tail now and then
    def idle(t, f):
        breath = math.sin(t * math.tau) 
        key(arm, "root", f, loc=(0, 0, breath * 0.008))
        key(arm, "spine", f, rx=breath * 1.2)
        key(arm, "chest", f, rx=-breath * 1.4)
        key(arm, "neck", f, rx=-2.0 + math.sin(t * math.tau + 0.7) * 2.2)
        key(arm, "head", f, rx=1.5 - math.sin(t * math.tau + 0.7) * 1.8, rz=math.sin(t * math.tau * 0.5) * 6.0)
        flick = max(0.0, math.sin(t * math.tau * 3 - 1.2)) ** 8
        key(arm, "ear_L", f, rz=flick * 34.0, rx=-flick * 12.0)
        key(arm, "ear_R", f, rz=-max(0.0, math.sin(t * math.tau * 3 + 0.4)) ** 8 * 30.0)
        key(arm, "tail", f, ry=math.sin(t * math.tau * 2 + 0.3) * 6.0)
        for tag in ("F", "B"):
            for s in ("L", "R"):
                key(arm, "thigh%s_%s" % (tag, s), f, rx=0); key(arm, "shin%s_%s" % (tag, s), f, rx=0)
                key(arm, "hoof%s_%s" % (tag, s), f, rx=0)
        key(arm, "jaw", f, rx=0)
    made.append(cycle(arm, "Idle", 72, idle))

    def walking(t, f): walk_pose(arm, t, f, stride=22.0, knee=34.0, bounce=0.022, duty=0.64); key(arm, "jaw", f, rx=0)
    made.append(cycle(arm, "Walk", 32, walking))

    def running(t, f):
        walk_pose(arm, t, f, stride=40.0, knee=78.0, bounce=0.10, duty=0.34, bound=True, lean=-4.0)
        key(arm, "neck", f, rx=10.0 * math.sin(t * math.tau) - 12.0)
        key(arm, "head", f, rx=-8.0 * math.sin(t * math.tau) + 8.0)
        key(arm, "tail", f, rx=-38.0, ry=math.sin(t * math.tau * 2) * 10.0)
        key(arm, "jaw", f, rx=6.0)
    made.append(cycle(arm, "Run", 22, running))

    # Grazing: head down and chewing, then up for a look. The neck is taken to -62 rather than
    # far enough to put the nose in the grass, because past about -100 the chain folds through
    # itself and the head comes out upside down under the chest. Measured, not guessed: see
    # the table in the handbook.
    def grazing(t, f):
        down = min(1.0, max(0.0, (t - 0.06) * 6.0)) * (1.0 - min(1.0, max(0.0, (t - 0.78) * 6.0)))
        key(arm, "neck", f, rx=-62.0 * down)
        key(arm, "head", f, rx=-6.0 * down + math.sin(t * math.tau * 9) * 2.5 * down)
        key(arm, "jaw", f, rx=(7.0 + 7.0 * math.sin(t * math.tau * 9)) * down)
        key(arm, "root", f, loc=(0, 0, -0.035 * down))
        key(arm, "spine", f, rx=-4.0 * down)
        key(arm, "tail", f, ry=math.sin(t * math.tau * 2) * 7.0)
        key(arm, "ear_L", f, rz=12.0 * down); key(arm, "ear_R", f, rz=-12.0 * down)
        # the front feet step apart to let the head down, which is what a deer does
        for s in ("L", "R"):
            key(arm, "thighF_%s" % s, f, rx=(9.0 if s == "L" else -3.0) * down)
            key(arm, "shinF_%s" % s, f, rx=-4.0 * down); key(arm, "hoofF_%s" % s, f, rx=0)
            key(arm, "thighB_%s" % s, f, rx=-4.0 * down); key(arm, "shinB_%s" % s, f, rx=0)
            key(arm, "hoofB_%s" % s, f, rx=0)
    made.append(cycle(arm, "Graze", 96, grazing))

    # the bellow: weight back, nose up, mouth open, and it shakes with it
    def bellow(t, f):
        up = min(1.0, max(0.0, (t - 0.10) * 5.0)) * (1.0 - min(1.0, max(0.0, (t - 0.72) * 4.0)))
        shake = math.sin(t * math.tau * 14) * up
        key(arm, "root", f, rx=-3.0 * up, loc=(0, -0.03 * up, 0.01 * up))
        key(arm, "spine", f, rx=5.0 * up)
        key(arm, "chest", f, rx=8.0 * up)
        key(arm, "neck", f, rx=42.0 * up + shake * 1.2)
        key(arm, "head", f, rx=20.0 * up + shake * 2.0)
        key(arm, "jaw", f, rx=(26.0 + 6.0 * math.sin(t * math.tau * 6)) * up)
        key(arm, "tail", f, ry=shake * 6.0)
        key(arm, "ear_L", f, rz=-18.0 * up); key(arm, "ear_R", f, rz=18.0 * up)
        for tag in ("F", "B"):
            for s in ("L", "R"):
                key(arm, "thigh%s_%s" % (tag, s), f, rx=(-4.0 if tag == "F" else 5.0) * up)
                key(arm, "shin%s_%s" % (tag, s), f, rx=0); key(arm, "hoof%s_%s" % (tag, s), f, rx=0)
    made.append(cycle(arm, "Bellow", 84, bellow))
    return made

# ---------------------------------------------------------------- scene and film
def scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "BLENDER_WORKBENCH"):
        try: sc.render.engine = engine; break
        except Exception: pass
    sc.render.resolution_x = 960; sc.render.resolution_y = 600
    sc.render.fps = 24
    world = bpy.data.worlds.new("World"); sc.world = world; world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (0.60, 0.70, 0.82, 1); bg.inputs[1].default_value = 1.0
    sun_data = bpy.data.lights.new("Sun", 'SUN'); sun_data.energy = 3.4; sun_data.angle = math.radians(5)
    sun = bpy.data.objects.new("Sun", sun_data); sc.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(48), math.radians(10), math.radians(-52))
    cam_data = bpy.data.cameras.new("Camera"); cam_data.lens = 55
    cam = bpy.data.objects.new("Camera", cam_data); sc.collection.objects.link(cam); sc.camera = cam
    # the ground, so it does not float
    bpy.ops.mesh.primitive_plane_add(size=24, location=(0, 0, 0))
    floor = bpy.context.active_object
    m = bpy.data.materials.new("Ground"); m.use_nodes = True
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.42, 0.46, 0.32, 1)
    m.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.95
    floor.data.materials.append(m)
    return sc, cam

def material():
    mat = bpy.data.materials.new("TileWorld Tiles")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes; links = mat.node_tree.links
    bsdf = nodes.get("Principled BSDF"); bsdf.inputs["Roughness"].default_value = 0.88
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(HERE, "TileWorldPalette.png"))
    tex.interpolation = 'Closest'
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return mat

def aim(cam, at, distance, elevation, azimuth):
    e = math.radians(elevation); a = math.radians(azimuth)
    target = Vector(at)
    cam.location = target + Vector((math.cos(e)*math.sin(a)*distance, -math.cos(e)*math.cos(a)*distance, math.sin(e)*distance))
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()

def film(sc, name, frames):
    """This Blender has no video encoder in it and its Python has no PIL, so the frames go out
    as stills and Tools/gif.py stitches them afterwards with the system Python."""
    into = os.path.join(FRAMES, name)
    os.makedirs(into, exist_ok=True)
    sc.frame_start = 1; sc.frame_end = frames
    sc.render.resolution_x = 640; sc.render.resolution_y = 400
    sc.render.image_settings.file_format = 'PNG'
    sc.render.filepath = os.path.join(into, "f_")
    bpy.ops.render.render(animation=True)
    sc.render.resolution_x = 960; sc.render.resolution_y = 600
    return into

def main():
    sc, cam = scene()
    ob, arm = make(build())
    ob.data.materials.append(material())
    print("DEER verts %d faces %d bones %d" % (len(ob.data.vertices), len(ob.data.polygons), len(arm.data.bones)))

    acts = animations(arm)

    # a still of the model, and one from the front
    arm.animation_data.action = None
    for pb in arm.pose.bones: pb.rotation_euler = Euler((0,0,0)); pb.location = Vector((0,0,0))
    sc.render.image_settings.file_format = 'PNG'
    aim(cam, (0, 0, 0.90), 6.2, 12, 38); sc.render.filepath = os.path.join(HERE, "deer-model.png"); bpy.ops.render.render(write_still=True)
    aim(cam, (0, 0, 0.90), 5.8, 8, 90); sc.render.filepath = os.path.join(HERE, "deer-side.png"); bpy.ops.render.render(write_still=True)

    shots = {"Idle": (5.6, 10, 40), "Walk": (6.0, 8, 78), "Run": (6.6, 8, 74), "Graze": (5.2, 6, 72), "Bellow": (5.4, 8, 52)}
    for act in acts:
        arm.animation_data.action = act
        d, e, a = shots[act.name]
        aim(cam, (0, 0, 0.80), d, e, a)
        frames = int(act.frame_range[1])
        film(sc, act.name.lower(), frames)
        print("FILM %s %d frames -> %s" % (act.name, frames, os.path.join(FRAMES, act.name.lower())))

    # the mesh and every action, on the game's axes
    arm.animation_data.action = acts[0]
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True); arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, "Deer.fbx"), use_selection=True,
                             axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL',
                             mesh_smooth_type='OFF', add_leaf_bones=False,
                             bake_anim=True, bake_anim_use_all_actions=True, bake_anim_step=1.0)
    print("DONE")

main()
