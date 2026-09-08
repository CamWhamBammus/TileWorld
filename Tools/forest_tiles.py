# Forest floor tiles for Tile World, built in Blender from arithmetic: five variants on the
# game's own terms (2.2 m footprint on a 2 m grid, body -1.00 to a top at 1.05, flat shaded,
# colour by where the UVs land on one palette). Renders previews and exports FBX.
import bpy, bmesh, math, random, json, os, sys
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
PAL = json.load(open(os.path.join(HERE, "palette.json")))
OUT = HERE

def uv_of(name): return PAL[name]

# ---------------------------------------------------------------- building blocks
class Build:
    """Triangles and quads collected with a palette colour each; flat shaded when made."""
    def __init__(self): self.verts=[]; self.faces=[]; self.uvs=[]
    def face(self, pts, colour, out=None):
        """A face; given which way is out, wound so its normal points that way (Blender's right-handed sense, which is what the icospheres use and what survives the export)."""
        pts = [Vector(p) for p in pts]
        if out is not None:
            n = (pts[1]-pts[0]).cross(pts[2]-pts[0])
            if n.dot(Vector(out)) < 0: pts = list(reversed(pts))
        base=len(self.verts)
        self.verts.extend(pts)
        self.faces.append(tuple(range(base, base+len(pts))))
        self.uvs.append(uv_of(colour))
    def quad(self, a,b,c,d, colour, out=None): self.face([a,b,c,d], colour, out)
    def tri(self, a,b,c, colour, out=None): self.face([a,b,c], colour, out)
    def make(self, name):
        me = bpy.data.meshes.new(name)
        # built with the game's Y up; Blender's up is Z, so turn it a quarter about X on the way in
        me.from_pydata([(v.x, -v.z, v.y) for v in self.verts], [], self.faces)
        me.update()
        uv = me.uv_layers.new(name="UVMap")
        for poly, colour in zip(me.polygons, self.uvs):
            for li in poly.loop_indices: uv.data[li].uv = colour
        for p in me.polygons: p.use_smooth = False
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        return ob

def rot_y(p, a, about=(0,0,0)):
    x,y,z = p[0]-about[0], p[1]-about[1], p[2]-about[2]
    c,s = math.cos(a), math.sin(a)
    return (about[0]+x*c+z*s, about[1]+y, about[2]-x*s+z*c)

def prism(b, centre, radius, height, sides, colour_side, colour_top, taper=1.0, rng=None, jitter=0.0, tilt=(0,0), cap_bottom=False):
    """An n-sided column standing on its base at centre, top possibly narrower; faces flat."""
    cx,cy,cz = centre
    bottom=[]; top=[]
    for i in range(sides):
        a = i/sides*math.tau
        j = (rng.uniform(-jitter, jitter) if rng else 0.0)
        r0 = radius*(1+j); r1 = radius*taper*(1+j)
        bx, bz = cx+math.cos(a)*r0, cz+math.sin(a)*r0
        tx, tz = cx+math.cos(a)*r1 + tilt[0]*height, cz+math.sin(a)*r1 + tilt[1]*height
        bottom.append((bx,cy,bz)); top.append((tx,cy+height,tz))
    for i in range(sides):
        a0 = (i+0.5)/sides*math.tau
        b.quad(bottom[i], bottom[(i+1)%sides], top[(i+1)%sides], top[i], colour_side, out=(math.cos(a0), 0, math.sin(a0)))
    b.face(top, colour_top, out=(0,1,0))
    if cap_bottom: b.face(bottom, colour_side, out=(0,-1,0))
    return top

def cylinder_along(b, p0, p1, radius, sides, colour_side, colour_end, rng=None, jitter=0.0, taper=1.0):
    """A log: a column from p0 to p1 with both ends capped."""
    p0=Vector(p0); p1=Vector(p1); axis=(p1-p0).normalized()
    up = Vector((0,1,0)) if abs(axis.y) < 0.9 else Vector((1,0,0))
    u = axis.cross(up).normalized(); v = axis.cross(u).normalized()
    ring0=[]; ring1=[]
    for i in range(sides):
        a=i/sides*math.tau; j=(rng.uniform(-jitter,jitter) if rng else 0)
        r=radius*(1+j)
        ring0.append(tuple(p0 + (u*math.cos(a)+v*math.sin(a))*r))
        ring1.append(tuple(p1 + (u*math.cos(a)+v*math.sin(a))*r*taper))
    for i in range(sides):
        mid = (Vector(ring0[i]) + Vector(ring1[(i+1)%sides])) * 0.5
        along = (mid - p0).dot(axis)
        b.quad(ring0[i], ring1[i], ring1[(i+1)%sides], ring0[(i+1)%sides], colour_side, out=tuple(mid - (p0 + axis*along)))
    b.face(ring0, colour_end, out=tuple(-axis)); b.face(ring1, colour_end, out=tuple(axis))

def blob(b, centre, size, colour_top, colour_side, rng, sub=1, squash=0.55, moss_from=0.35, patchy=0.0):
    """A lump: an icosphere pushed about by hand, its upper faces the top colour, patchily if asked."""
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=1.0)
    for v in bm.verts:
        v.co.x *= size[0]*(1+rng.uniform(-0.2,0.2)); v.co.y *= size[1]*(1+rng.uniform(-0.15,0.15)); v.co.z *= size[2]*(1+rng.uniform(-0.2,0.2))
        v.co.y *= (squash if v.co.y < 0 else 1.0)
    bm.faces.ensure_lookup_table()
    for f in bm.faces:
        pts=[(centre[0]+v.co.x, centre[1]+v.co.y, centre[2]+v.co.z) for v in f.verts]
        n = f.normal
        top = n.y > moss_from and rng.random() >= patchy
        b.face(pts, colour_top if top else colour_side)
    bm.free()

# ---------------------------------------------------------------- the tile itself
TOP = 1.05; BOTTOM = -1.0; HALF = 1.00; INNER = 0.90   # the body meets its neighbours edge to edge on the 2 m grid: an overlap of flat tops fights for the pixels

def body(b, rng, layer=0.22):
    """The block: earth sides in two bands, a dark humus top layer, the top itself a jittered grid."""
    n = 4
    # the top grid, rim held at TOP, the inside lifted and drifted a little
    grid = {}
    for i in range(n+1):
        for j in range(n+1):
            x = -HALF + 2*HALF*i/n; z = -HALF + 2*HALF*j/n
            edge = i==0 or j==0 or i==n or j==n
            y = TOP if edge else TOP + rng.uniform(0.0, 0.09)
            if not edge: x += rng.uniform(-0.07,0.07); z += rng.uniform(-0.07,0.07)
            grid[(i,j)] = (x,y,z)
    for i in range(n):
        for j in range(n):
            a,bq,c,d = grid[(i,j)], grid[(i,j+1)], grid[(i+1,j+1)], grid[(i+1,j)]
            # two triangles, so the jitter shows as facets
            col = rng.choice(["humus","humus","humus2","earth"])
            col2 = rng.choice(["humus","humus","humus2","earth"])
            b.tri(a,bq,c,col,out=(0,1,0)); b.tri(a,c,d,col2,out=(0,1,0))
    # the sides: the humus band under the rim, then earth, then darker earth to the bottom
    bands = [(TOP, TOP-layer, "earth"), (TOP-layer, 0.1, "earth"), (0.1, BOTTOM, "earth2")]   # earth all the way: a dark band under the rim read as a hole in shade
    corners = [(-HALF,-HALF),(HALF,-HALF),(HALF,HALF),(-HALF,HALF)]
    for k in range(4):
        (x0,z0),(x1,z1) = corners[k], corners[(k+1)%4]
        outward = ((x0+x1)*0.5, 0, (z0+z1)*0.5)
        for (y1,y0,col) in bands:
            b.quad((x0,y0,z0),(x1,y0,z1),(x1,y1,z1),(x0,y1,z0), col, out=outward)
    # the underside
    b.quad((-HALF,BOTTOM,-HALF),(-HALF,BOTTOM,HALF),(HALF,BOTTOM,HALF),(HALF,BOTTOM,-HALF), "earth2", out=(0,-1,0))

def litter(b, rng, count, keep_out=()):
    """Fallen leaves: small raised facets scattered on the top, ochre, rust and pale."""
    placed = 0; tries = 0
    while placed < count and tries < count*20:
        tries += 1
        x = rng.uniform(-INNER+0.1, INNER-0.1); z = rng.uniform(-INNER+0.1, INNER-0.1)
        if any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep_out): continue
        s = rng.uniform(0.09, 0.16); a = rng.uniform(0, math.tau)
        col = rng.choice(["litter","litter","litter2","litter3"])
        y = TOP + 0.10 + rng.uniform(0, 0.03)
        if rng.random() < 0.6:
            # a leaf: a kite of two triangles, one tip lifted
            tip = (x+math.cos(a)*s*1.4, y+0.04, z+math.sin(a)*s*1.4)
            l = (x+math.cos(a+2.2)*s, y, z+math.sin(a+2.2)*s); r = (x+math.cos(a-2.2)*s, y, z+math.sin(a-2.2)*s)
            tail = (x-math.cos(a)*s*0.5, y, z-math.sin(a)*s*0.5)
            b.tri(tail, l, tip, col, out=(0,1,0)); b.tri(tail, tip, r, col, out=(0,1,0))
        else:
            # a rounder leaf: a five-sided fan, one edge lifted
            ring = [(x+math.cos(a+k/5*math.tau)*s*0.8, y + (0.035 if k == 0 else 0.0), z+math.sin(a+k/5*math.tau)*s*0.8) for k in range(5)]
            b.face(ring, col, out=(0,1,0))
        placed += 1

def leaf_pile(b, rng, centre, radius):
    """A drift of leaves against something: a low lump in the litter's own colours."""
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0)
    for v in bm.verts:
        v.co.x *= radius*(1+rng.uniform(-0.25,0.25)); v.co.y *= radius*0.35*(1+rng.uniform(-0.2,0.2)); v.co.z *= radius*(1+rng.uniform(-0.25,0.25))
        if v.co.y < 0: v.co.y *= 0.1
    for f in bm.faces:
        pts=[(centre[0]+v.co.x, TOP+0.06+v.co.y, centre[1]+v.co.z) for v in f.verts]
        b.face(pts, rng.choice(["litter","litter2","litter3","humus"]))
    bm.free()

def moss_patch(b, rng, centre, radius, height=0.07):
    """A soft hump of moss: a lump, two greens across its facets, sunk into the ground."""
    blob(b, (centre[0], TOP+0.04, centre[1]), (radius, height*1.6, radius*rng.uniform(0.75,1.0)), "moss", "moss2", rng, sub=1, squash=0.15, moss_from=-0.2)

def pebbles(b, rng, count, keep_out=()):
    for _ in range(count):
        for _t in range(20):
            x = rng.uniform(-INNER+0.15, INNER-0.15); z = rng.uniform(-INNER+0.15, INNER-0.15)
            if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep_out): break
        s = rng.uniform(0.07, 0.13)
        blob(b, (x, TOP+0.10+s*0.35, z), (s, s*0.7, s*0.85), "pebble", "stone2", rng, sub=0, squash=0.4, moss_from=2.0)

def mushroom(b, rng, at, size=1.0):
    x,z = at; h = 0.16*size; r = 0.11*size
    prism(b, (x, TOP+0.10, z), 0.035*size, h, 6, "stem", "stem", taper=0.9)
    # the cap: a cone with a flat underside, red with a cream rim
    cap = []
    for i in range(7):
        a = i/7*math.tau; cap.append((x+math.cos(a)*r, TOP+0.10+h, z+math.sin(a)*r))
    peak = (x, TOP+0.10+h+0.09*size, z)
    for i in range(7):
        a0 = (i+0.5)/7*math.tau
        b.tri(cap[i], cap[(i+1)%7], peak, "capred" if i%3 else "capcream", out=(math.cos(a0), 0.7, math.sin(a0)))
    b.face(cap, "capcream", out=(0,-1,0))

def fern(b, rng, at, fronds=5, size=1.0):
    x,z = at
    for k in range(fronds):
        a = k/fronds*math.tau + rng.uniform(-0.3,0.3)
        length = rng.uniform(0.42, 0.58)*size; w = 0.11*size
        segs = 3
        prev = (x, TOP+0.12, z)
        for s in range(segs):
            t0 = s/segs; t1 = (s+1)/segs
            def at_t(t): return (x+math.cos(a)*length*t, TOP+0.12 + 0.55*length*math.sin(t*math.pi*0.9), z+math.sin(a)*length*t)
            p0 = at_t(t0); p1 = at_t(t1)
            side = (math.cos(a+math.pi/2)*w*(1-t0*0.7), 0, math.sin(a+math.pi/2)*w*(1-t0*0.7))
            side1 = (math.cos(a+math.pi/2)*w*(1-t1*0.7), 0, math.sin(a+math.pi/2)*w*(1-t1*0.7))
            col = "fern" if s < 2 else "fern2"
            b.quad((p0[0]-side[0],p0[1],p0[2]-side[2]), (p0[0]+side[0],p0[1],p0[2]+side[2]), (p1[0]+side1[0],p1[1],p1[2]+side1[2]), (p1[0]-side1[0],p1[1],p1[2]-side1[2]), col)
            # and the same face the other way up, so it shows from below too
            b.quad((p1[0]-side1[0],p1[1],p1[2]-side1[2]), (p1[0]+side1[0],p1[1],p1[2]+side1[2]), (p0[0]+side[0],p0[1],p0[2]+side[2]), (p0[0]-side[0],p0[1],p0[2]-side[2]), "fern2")

def tube(b, pts, radii, sides, colours, cap_start=True, cap_end=True):
    """A tube through points, one ring at each, the frame carried from ring to ring so it never twists."""
    P = [Vector(p) for p in pts]
    rings = []
    u = None
    for i in range(len(P)):
        axis = (P[min(i+1, len(P)-1)] - P[max(i-1, 0)]).normalized()
        if u is None:
            ref = Vector((0,1,0)) if abs(axis.y) < 0.9 else Vector((1,0,0))
            u = axis.cross(ref).normalized()
        else:
            u = (u - axis * u.dot(axis)).normalized()
        v = axis.cross(u).normalized()
        rings.append([tuple(P[i] + (u*math.cos(k/sides*math.tau) + v*math.sin(k/sides*math.tau)) * radii[i]) for k in range(sides)])
    for i in range(len(P)-1):
        for k in range(sides):
            mid = (Vector(rings[i][k]) + Vector(rings[i+1][(k+1)%sides])) * 0.5
            b.quad(rings[i][k], rings[i+1][k], rings[i+1][(k+1)%sides], rings[i][(k+1)%sides], colours[k % len(colours)], out=tuple(mid - (P[i]+P[i+1])*0.5))
    if cap_start: b.face(rings[0], colours[-1], out=tuple(P[0]-P[1]))
    if cap_end: b.face(rings[-1], colours[-1], out=tuple(P[-1]-P[-2]))

def root(b, rng, start, angle, length, radius):
    """A root ridge: a bent tube that rises out of the ground and dives back in at its end."""
    segs = 5
    x,z = start
    pts=[]; radii=[]
    for s in range(segs+1):
        t = s/segs
        a = angle + math.sin(t*3.0 + 0.5)*0.4
        px = x + math.cos(a)*length*t; pz = z + math.sin(a)*length*t
        py = TOP + 0.04 + radius*1.1*math.sin(t*math.pi)**0.6 + 0.03*math.sin(t*7.0)
        pts.append((px,py,pz)); radii.append(radius*(1.0-0.55*t))
    tube(b, pts, radii, 6, ["bark","bark","bark2"])

def twig(b, rng, at, angle, length):
    x,z = at
    pts=[(x+math.cos(angle)*length*t, TOP+0.10+0.02+0.03*math.sin(t*math.pi), z+math.sin(angle)*length*t) for t in (0, 0.5, 1)]
    tube(b, pts, [0.018, 0.016, 0.012], 4, ["twig","bark2"])

def tuft(b, rng, at, size=1.0):
    """A tuft of forest grass: five blades, each a thin bent quad, dark green."""
    x,z = at
    for k in range(5):
        a = k/5*math.tau + rng.uniform(-0.4,0.4)
        h = rng.uniform(0.22, 0.34)*size; lean = rng.uniform(0.08, 0.16)
        w = 0.028*size
        base = (x + math.cos(a)*0.03, TOP+0.10, z + math.sin(a)*0.03)
        tip = (x + math.cos(a)*lean, TOP+0.10+h, z + math.sin(a)*lean)
        sx, sz = math.cos(a+math.pi/2)*w, math.sin(a+math.pi/2)*w
        col = "grassdark" if k%2 else "fern2"
        b.quad((base[0]-sx,base[1],base[2]-sz),(base[0]+sx,base[1],base[2]+sz),(tip[0]+sx*0.3,tip[1],tip[2]+sz*0.3),(tip[0]-sx*0.3,tip[1],tip[2]-sz*0.3), col)
        b.quad((tip[0]-sx*0.3,tip[1],tip[2]-sz*0.3),(tip[0]+sx*0.3,tip[1],tip[2]+sz*0.3),(base[0]+sx,base[1],base[2]+sz),(base[0]-sx,base[1],base[2]-sz), col)

# ---------------------------------------------------------------- the five
def tile(index):
    rng = random.Random(1000+index)
    b = Build()
    body(b, rng)
    keep = []
    if index == 0:
        moss_patch(b, rng, (0.35, -0.3), 0.34); keep.append((0.35,-0.3,0.4))
        leaf_pile(b, rng, (-0.5, 0.45), 0.36); keep.append((-0.5,0.45,0.4))
        pebbles(b, rng, 3, keep)
        for at in [(-0.6,-0.6),(0.7,0.6)]: tuft(b, rng, at)
        for k in range(3): twig(b, rng, (rng.uniform(-0.8,0.8), rng.uniform(-0.8,0.8)), rng.uniform(0,math.tau), rng.uniform(0.25,0.45))
        litter(b, rng, 22, keep)
    elif index == 1:
        for k in range(4):
            a = k/4*math.tau + rng.uniform(-0.5,0.5)
            root(b, rng, (rng.uniform(-0.25,0.25), rng.uniform(-0.25,0.25)), a, rng.uniform(0.75,0.95), rng.uniform(0.07,0.10))
        moss_patch(b, rng, (0.55, 0.6), 0.2)
        for at in [(-0.7,0.55),(0.65,-0.65)]: tuft(b, rng, at)
        litter(b, rng, 16)
    elif index == 2:
        cylinder_along(b, (-0.75, TOP+0.10+0.2, -0.45), (0.7, TOP+0.10+0.2, 0.25), 0.21, 8, "bark", "woodring", rng=rng, jitter=0.12, taper=0.85)
        keep.append((0,0,0.35))
        leaf_pile(b, rng, (-0.25, 0.45), 0.3); keep.append((-0.25,0.45,0.35))
        for k, at in enumerate([(0.45,-0.55),(0.62,-0.42),(-0.55,0.62)]): mushroom(b, rng, at, size=rng.uniform(0.8,1.25))
        pebbles(b, rng, 2, keep+[(0.5,-0.5,0.3),(-0.55,0.62,0.25)])
        tuft(b, rng, (-0.72,-0.7))
        litter(b, rng, 14, keep)
    elif index == 3:
        stump_top = prism(b, (-0.3, TOP+0.08, 0.2), 0.3, 0.38, 8, "bark", "wood", taper=0.92, rng=rng, jitter=0.1)
        # the rings: a smaller darker ring and a pale heart laid on the cut
        for r, col in ((0.19, "woodring"), (0.09, "wood")):
            b.face([(-0.3+math.cos(k/8*math.tau)*r, TOP+0.08+0.38+0.006, 0.2+math.sin(k/8*math.tau)*r) for k in range(8)], col, out=(0,1,0))
        # a flare of three surface roots at the foot
        for k in range(3):
            a = k/3*math.tau + 0.4
            root(b, rng, (-0.3+math.cos(a)*0.22, 0.2+math.sin(a)*0.22), a, 0.45, 0.07)
        keep.append((-0.3,0.2,0.55))
        for at in [(0.55,-0.5),(0.6,0.55),(-0.65,-0.55)]: fern(b, rng, at, size=rng.uniform(0.9,1.15))
        for k in range(2): twig(b, rng, (rng.uniform(0.1,0.8), rng.uniform(-0.2,0.2)), rng.uniform(0,math.tau), 0.3)
        litter(b, rng, 12, keep+[(0.55,-0.5,0.3),(0.6,0.55,0.3),(-0.65,-0.55,0.3)])
    else:
        blob(b, (0.15, TOP+0.06+0.2, -0.1), (0.6, 0.4, 0.5), "moss", "stone", rng, sub=2, squash=0.3, moss_from=0.4, patchy=0.35)
        keep.append((0.15,-0.1,0.75))
        moss_patch(b, rng, (-0.7, 0.6), 0.2)
        pebbles(b, rng, 3, keep)
        for at in [(-0.72,-0.6),(0.72,0.7)]: tuft(b, rng, at)
        for k in range(2): twig(b, rng, (rng.uniform(-0.85,-0.4), rng.uniform(-0.8,0.3)), rng.uniform(0,math.tau), 0.35)
        litter(b, rng, 16, keep)
    ob = b.make("Forest Tile %d" % index)
    return ob

# ---------------------------------------------------------------- scene, materials, renders
def material(palette_path):
    mat = bpy.data.materials.new("TileWorld Tiles")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes; links = mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Roughness"].default_value = 0.85
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(palette_path)
    tex.interpolation = 'Closest'
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return mat

def setup_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "BLENDER_WORKBENCH"):
        try: scene.render.engine = engine; break
        except Exception: pass
    scene.render.resolution_x = 1600; scene.render.resolution_y = 1000
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("World"); scene.world = world; world.use_nodes = True
    bg = world.node_tree.nodes.get("Background"); bg.inputs[0].default_value = (0.62, 0.72, 0.84, 1); bg.inputs[1].default_value = 1.0
    sun_data = bpy.data.lights.new("Sun", 'SUN'); sun_data.energy = 3.2; sun_data.angle = math.radians(4)
    sun = bpy.data.objects.new("Sun", sun_data); scene.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(42), math.radians(12), math.radians(-40))
    cam_data = bpy.data.cameras.new("Camera"); cam_data.lens = 45
    cam = bpy.data.objects.new("Camera", cam_data); scene.collection.objects.link(cam); scene.camera = cam
    return scene, cam

def look(cam, at, distance, elevation_deg, azimuth_deg):
    # at is given as (x, height, depth) the game's way; Blender has height on Z
    e = math.radians(elevation_deg); a = math.radians(azimuth_deg)
    target = Vector((at[0], -at[2], at[1]))
    pos = target + Vector((math.cos(e)*math.sin(a)*distance, -math.cos(e)*math.cos(a)*distance, math.sin(e)*distance))
    cam.location = pos
    cam.rotation_euler = (target - pos).to_track_quat('-Z', 'Y').to_euler()

def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)

def main():
    scene, cam = setup_scene()
    mat = material(os.path.join(HERE, "TileWorldPalette.png"))
    tiles = [tile(i) for i in range(5)]
    for t in tiles: t.data.materials.append(mat)
    for i,t in enumerate(tiles):
        print("TILE %d verts %d faces %d" % (i, len(t.data.vertices), len(t.data.polygons)))

    # ---- the line-up: five tiles side by side, from the front and above
    for i,t in enumerate(tiles): t.location = ((i-2)*2.6, 0, 0)
    look(cam, (0, 1.0, 0), 11.5, 30, 0); cam.data.lens = 40
    render(os.path.join(OUT, "lineup.png"))

    # ---- each one close, from a low three-quarter view
    for i,t in enumerate(tiles):
        for o in tiles: o.hide_render = (o is not t)
        look(cam, (t.location.x, 1.15, 0), 4.6, 28, 35); cam.data.lens = 45
        render(os.path.join(OUT, "tile%d.png" % i))
    for o in tiles: o.hide_render = False

    # ---- a patch: six by six on the grid with quarter turns, as the world would lay them
    rng = random.Random(7)
    patch = []
    for gx in range(-3, 3):
        for gz in range(-3, 3):
            src = rng.choice(tiles)
            ob = bpy.data.objects.new("P", src.data); scene.collection.objects.link(ob)
            ob.location = (gx*2.0+1.0, -(gz*2.0+1.0), rng.choice([0, 0, 0, 0.25, -0.25]))
            ob.rotation_euler = (0, 0, rng.choice([0, 1, 2, 3])*math.pi/2)
            patch.append(ob)
    for t in tiles: t.hide_render = True
    look(cam, (0, 1.2, 0), 15.5, 38, 25); cam.data.lens = 40
    render(os.path.join(OUT, "patch.png"))
    look(cam, (0, 1.2, 0), 6.5, 22, 60); cam.data.lens = 40
    render(os.path.join(OUT, "patch-low.png"))
    for ob in patch: bpy.data.objects.remove(ob)
    for t in tiles: t.hide_render = False

    # ---- the meshes, exported on the game's axes, one file each
    for i,t in enumerate(tiles):
        t.location = (0,0,0); t.rotation_euler = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT'); t.select_set(True); bpy.context.view_layer.objects.active = t
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "Forest Tile %d.fbx" % i), use_selection=True, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL', mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
