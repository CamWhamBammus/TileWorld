# Snow tiles for Tile World: the snowfields' own ground, five variants built in Blender on the game's
# terms -- deep snow with drifts, snow over rock that shows through, a frozen puddle, a snow-laden
# shrub, a line of tracks -- white with blue in the shade. Renders previews and exports FBX.
import bpy, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
Build, prism, tube, blob = ft["Build"], ft["prism"], ft["tube"], ft["blob"]
TOP, BOTTOM, HALF, INNER = ft["TOP"], ft["BOTTOM"], ft["HALF"], ft["INNER"]
from mathutils import Vector

def snow_body(b, rng, puddle=None, rock=None):
    """The block: a soft top of snow facets, white with blue in the hollows; a snow band over frosted rock down the sides."""
    n = 4; grid = {}
    for i in range(n+1):
        for j in range(n+1):
            x = -HALF + 2*HALF*i/n; z = -HALF + 2*HALF*j/n
            edge = i==0 or j==0 or i==n or j==n
            y = TOP if edge else TOP + rng.uniform(0.02, 0.12)
            if not edge: x += rng.uniform(-0.06,0.06); z += rng.uniform(-0.06,0.06)
            if puddle is not None and not edge and (x-puddle[0])**2 + (z-puddle[1])**2 < puddle[2]**2: y = TOP - 0.02
            grid[(i,j)] = (x,y,z)
    for i in range(n):
        for j in range(n):
            a,bq,c,d = grid[(i,j)], grid[(i,j+1)], grid[(i+1,j+1)], grid[(i+1,j)]
            for tri in ((a,bq,c),(a,c,d)):
                cx = sum(p[0] for p in tri)/3; cz = sum(p[2] for p in tri)/3; cy = sum(p[1] for p in tri)/3
                if puddle is not None and (cx-puddle[0])**2 + (cz-puddle[1])**2 < (puddle[2]*0.85)**2: col = rng.choice(["ice","ice","ice2"])
                elif rock is not None and (cx-rock[0])**2 + (cz-rock[1])**2 < rock[2]**2: col = rng.choice(["frostrock","frostrock2"])
                else: col = rng.choice(["snow1","snow1","snow2","snow3"]) if cy > TOP + 0.05 else rng.choice(["snow2","snow3","snowshade"])
                b.tri(*tri, col, out=(0,1,0))
    bands = [(TOP, TOP-0.32, "snow2"), (TOP-0.32, TOP-0.9, "frostrock"), (TOP-0.9, BOTTOM, "frostrock2")]
    corners = [(-HALF,-HALF),(HALF,-HALF),(HALF,HALF),(-HALF,HALF)]
    for k in range(4):
        (x0,z0),(x1,z1) = corners[k], corners[(k+1)%4]
        outward = ((x0+x1)*0.5, 0, (z0+z1)*0.5)
        for (y1,y0,col) in bands:
            b.quad((x0,y0,z0),(x1,y0,z1),(x1,y1,z1),(x0,y1,z0), col, out=outward)
    b.quad((-HALF,BOTTOM,-HALF),(-HALF,BOTTOM,HALF),(HALF,BOTTOM,HALF),(HALF,BOTTOM,-HALF), "frostrock2", out=(0,-1,0))

def drift(b, rng, centre, size):
    blob(b, (centre[0], TOP+0.04, centre[1]), size, "snow1", "snow3", rng, sub=1, squash=0.1, moss_from=0.3)

def frosted_rock(b, rng, centre, size):
    blob(b, (centre[0], TOP+0.06+size[1]*0.4, centre[1]), size, "snow1", "frostrock", rng, sub=1, squash=0.35, moss_from=0.55)

def shrub(b, rng, at):
    """A bare shrub with snow caught on its top twigs."""
    x,z = at
    for k in range(7):
        a = k/7*math.tau + rng.uniform(-0.4,0.4); h = rng.uniform(0.22, 0.4); lean = rng.uniform(0.1, 0.24)
        pts = [(x, TOP+0.06, z), (x+math.cos(a)*lean*0.5, TOP+0.06+h*0.55, z+math.sin(a)*lean*0.5), (x+math.cos(a)*lean, TOP+0.06+h, z+math.sin(a)*lean)]
        tube(b, pts, [0.018, 0.013, 0.008], 3, ["shrubdark","shrubdark"], cap_start=False)
        blob(b, (pts[2][0], pts[2][1]+0.02, pts[2][2]), (0.05, 0.03, 0.05), "snow1", "snow2", rng, sub=0, squash=0.5, moss_from=-1)

def tracks(b, rng, start, angle, count):
    """A line of tracks across the snow: small shaded pits, left and right."""
    x,z = start; side = (math.cos(angle+math.pi/2), math.sin(angle+math.pi/2))
    for k in range(count):
        t = k*0.32; s = 0.06 if k % 2 else -0.06
        cx = x + math.cos(angle)*t + side[0]*s; cz = z + math.sin(angle)*t + side[1]*s
        ring = [(cx+math.cos(q/5*math.tau)*0.045, TOP+0.125, cz+math.sin(q/5*math.tau)*0.06) for q in range(5)]
        b.face(ring, "track", out=(0,1,0))

def buried_log(b, rng, p0, p1, radius):
    ft["cylinder_along"](b, (p0[0], TOP+0.05+radius*0.3, p0[1]), (p1[0], TOP+0.05+radius*0.3, p1[1]), radius, 7, "shrubdark", "bark2", rng=rng, jitter=0.1, taper=0.9)
    blob(b, ((p0[0]+p1[0])*0.5, TOP+0.05+radius*0.9, (p0[1]+p1[1])*0.5), (abs(p1[0]-p0[0])*0.5+0.1, 0.06, abs(p1[1]-p0[1])*0.5+0.16), "snow1", "snow2", rng, sub=1, squash=0.1, moss_from=-1)

def tile(index):
    rng = random.Random(8000+index); b = Build(); keep = []
    def spot(margin=0.25, r=0.35):
        for _t in range(40):
            x = rng.uniform(-INNER+margin, INNER-margin); z = rng.uniform(-INNER+margin, INNER-margin)
            if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
        keep.append((x,z,r)); return (x,z)
    if index == 0:
        snow_body(b, rng)
        for _ in range(2): drift(b, rng, spot(0.35, 0.5), (rng.uniform(0.4,0.55), 0.14, rng.uniform(0.3,0.45)))
    elif index == 1:
        r = (rng.uniform(-0.3,0.3), rng.uniform(-0.3,0.3), 0.55)
        snow_body(b, rng, rock=r)
        frosted_rock(b, rng, (r[0], r[1]), (0.34, 0.26, 0.3)); keep.append((r[0],r[1],0.6))
        drift(b, rng, spot(0.3, 0.4), (0.3, 0.1, 0.24))
    elif index == 2:
        p = (rng.uniform(-0.3,0.3), rng.uniform(-0.3,0.3), 0.5)
        snow_body(b, rng, puddle=p); keep.append((p[0],p[1],0.6))
        shrub(b, rng, spot(0.25, 0.3))
    elif index == 3:
        snow_body(b, rng)
        shrub(b, rng, spot(0.3, 0.35)); shrub(b, rng, spot(0.3, 0.35))
        frosted_rock(b, rng, spot(0.25, 0.3), (0.16, 0.12, 0.14))
    else:
        snow_body(b, rng)
        a = rng.uniform(0, math.tau)
        tracks(b, rng, (-math.cos(a)*0.8, -math.sin(a)*0.8), a, 6)
        buried_log(b, rng, (0.55, -0.7), (0.7, 0.4), 0.14)
    return b.make("Snow Tile %d" % index)

def main():
    scene, cam = ft["setup_scene"]()
    mat = ft["material"](os.path.join(HERE, "TileWorldPalette.png"))
    tiles = [tile(i) for i in range(5)]
    for t in tiles: t.data.materials.append(mat); print("TILE %s verts %d faces %d" % (t.name, len(t.data.vertices), len(t.data.polygons)))
    for i,t in enumerate(tiles): t.location = ((i-2)*2.6, 0, 0)
    ft["look"](cam, (0, 1.0, 0), 11.5, 30, 0); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "snow-lineup.png"))
    for t in tiles: t.hide_render = True
    rng = random.Random(17); placed = []
    for gx in range(-4, 4):
        for gz in range(-4, 4):
            ob = bpy.data.objects.new("P", rng.choice(tiles).data); scene.collection.objects.link(ob)
            h = round(0.8*math.sin(gx*0.5)*math.cos(gz*0.4)*2)/2*0.25 + rng.choice([0,0,0,0.25])
            ob.location = (gx*2.0+1.0, -(gz*2.0+1.0), h); ob.rotation_euler = (0, 0, rng.choice([0,1,2,3])*math.pi/2)
            placed.append(ob)
    ft["look"](cam, (0, 1.2, 0), 15.5, 34, 28); cam.data.lens = 38
    ft["render"](os.path.join(HERE, "snow-patch.png"))
    ft["look"](cam, (0, 1.3, 1), 7.5, 12, 55); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "snow-low.png"))
    for ob in placed: bpy.data.objects.remove(ob)
    for t in tiles:
        t.hide_render = False; t.location = (0,0,0); t.rotation_euler = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT'); t.select_set(True); bpy.context.view_layer.objects.active = t
        bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, t.name + ".fbx"), use_selection=True, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL', mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
