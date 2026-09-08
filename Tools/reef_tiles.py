# Coral reef tiles for Tile World: the floor of the warm shallows, seen through the water.
# Five variants built in Blender on the game's terms, like the forest floor and the sand --
# a limestone shelf with a coralline rim, sand channels blown between the heads, brain and
# staghorn and table coral, fans, sponges, anemones, urchins, seagrass and old white rubble.
# Renders previews, dry and as the sea will show them, and exports FBX.
import bpy, math, random, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
Build, prism, tube, blob, cylinder_along = ft["Build"], ft["prism"], ft["tube"], ft["blob"], ft["cylinder_along"]
TOP, BOTTOM, HALF, INNER = ft["TOP"], ft["BOTTOM"], ft["HALF"], ft["INNER"]
from mathutils import Vector

SAND = ["sand1", "sand2", "sand3", "sand1"]
ROCK = ["rock3", "pebble", "rock3", "sandwet"]

# ---------------------------------------------------------------- the block
def reef_body(b, rng, tones, ripple=False):
    """The shelf: a top of sand or worn limestone, a pink line of coralline crust under the
    rim, then rock down to the bottom -- the same sides on all five, so they tile."""
    n = 4; grid = {}
    for i in range(n+1):
        for j in range(n+1):
            x = -HALF + 2*HALF*i/n; z = -HALF + 2*HALF*j/n
            edge = i==0 or j==0 or i==n or j==n
            y = TOP if edge else TOP + (0.045 + 0.03*math.sin(i*2.4 + j*0.7) if ripple else rng.uniform(0.0, 0.05))
            if not edge: x += rng.uniform(-0.06,0.06); z += rng.uniform(-0.06,0.06)
            grid[(i,j)] = (x,y,z)
    for i in range(n):
        for j in range(n):
            a,bq,c,d = grid[(i,j)], grid[(i,j+1)], grid[(i+1,j+1)], grid[(i+1,j)]
            b.tri(a,bq,c,rng.choice(tones),out=(0,1,0)); b.tri(a,c,d,rng.choice(tones),out=(0,1,0))
    bands = [(TOP, TOP-0.08, "crust"), (TOP-0.08, TOP-0.4, "rock3"), (TOP-0.4, 0.0, "rock"), (0.0, BOTTOM, "rockdark")]
    corners = [(-HALF,-HALF),(HALF,-HALF),(HALF,HALF),(-HALF,HALF)]
    for k in range(4):
        (x0,z0),(x1,z1) = corners[k], corners[(k+1)%4]
        outward = ((x0+x1)*0.5, 0, (z0+z1)*0.5)
        for (y1,y0,col) in bands:
            b.quad((x0,y0,z0),(x1,y0,z1),(x1,y1,z1),(x0,y1,z0), col, out=outward)
    b.quad((-HALF,BOTTOM,-HALF),(-HALF,BOTTOM,HALF),(HALF,BOTTOM,HALF),(HALF,BOTTOM,-HALF), "rockdark", out=(0,-1,0))

GROUND = TOP + 0.05   # detail sits on the top, a little above the facets

# ---------------------------------------------------------------- the coral
def brain(b, rng, at, radius, height, colour, groove):
    """A brain coral: a dome whose growth rings wander round it as ridges and grooves."""
    x, z = at; sides = 10; rings = 5
    hub = Vector((x, GROUND, z))
    ring = [[(x + math.cos(k/sides*math.tau)*radius, GROUND, z + math.sin(k/sides*math.tau)*radius) for k in range(sides)]]
    for r in range(1, rings+1):
        t = r/rings
        rr = radius*math.cos(t*math.pi*0.5)**0.65; yy = GROUND + height*math.sin(t*math.pi*0.5)
        wob = 0.03*radius
        ring.append([(x + math.cos(k/sides*math.tau)*(rr + math.sin(k*2.3 + r)*wob), yy + math.sin(k*1.7 + r*2)*height*0.05,
                      z + math.sin(k/sides*math.tau)*(rr + math.sin(k*2.3 + r)*wob)) for k in range(sides)])
    for r in range(rings):
        for k in range(sides):
            quad = [ring[r][k], ring[r][(k+1)%sides], ring[r+1][(k+1)%sides], ring[r+1][k]]
            mid = sum((Vector(p) for p in quad), Vector()) / 4
            # bands round the dome, the edge of each one wandering a facet either way
            band = r + (1 if math.sin(k*2.6 + r*1.3) > 0.55 else 0)
            b.quad(*quad, colour if band % 2 else groove, out=tuple(mid - hub))
    b.face(ring[-1], colour, out=(0,1,0))
def branchy(b, rng, at, size, colour, tip, arms=5, forks=True):
    """Staghorn: a knot on the floor with arms out of it, each one paler at its ends."""
    x, z = at
    blob(b, (x, GROUND + 0.03*size, z), (0.14*size, 0.10*size, 0.14*size), colour, colour, rng, sub=0, squash=0.4, moss_from=-2)
    def arm(x0, z0, y0, angle, lean, length, radius, depth):
        x1 = x0 + math.cos(angle)*lean*length; z1 = z0 + math.sin(angle)*lean*length; y1 = y0 + length
        mid = ((x0+x1)*0.5 + rng.uniform(-0.02,0.02), (y0+y1)*0.5, (z0+z1)*0.5)
        tube(b, [(x0,y0,z0), (mid[0],mid[1],mid[2]), (x1,y1,z1)], [radius, radius*0.8, radius*0.6], 4, [colour], cap_start=False, cap_end=False)
        if depth > 0 and forks:
            for s in (-1, 1):
                arm(x1, z1, y1, angle + s*rng.uniform(0.5,1.0), lean + 0.35, length*0.55, radius*0.6, depth-1)
        else:
            tube(b, [(x1,y1,z1), (x1 + math.cos(angle)*lean*length*0.4, y1 + length*0.35, z1 + math.sin(angle)*lean*length*0.4)],
                 [radius*0.6, radius*0.25], 4, [tip], cap_start=False)
    for k in range(arms):
        a = k/arms*math.tau + rng.uniform(-0.3, 0.3)
        arm(x + math.cos(a)*0.05, z + math.sin(a)*0.05, GROUND + 0.04*size, a, rng.uniform(0.25,0.5), rng.uniform(0.20,0.30)*size, 0.035*size, 1)

def table(b, rng, at, radius, height, colour, shade):
    """Table coral: a broad plate with a real edge to it, held out on a narrow stalk."""
    x, z = at; sides = 11
    prism(b, (x, GROUND - 0.02, z), radius*0.20, height, 6, shade, shade, taper=0.85)
    lip = GROUND + height
    lean = (rng.uniform(-0.09, 0.09), rng.uniform(-0.09, 0.09))
    thick = max(0.03, radius*0.10)
    def at_edge(k):
        a = k/sides*math.tau; rr = radius*rng.uniform(0.92, 1.08)
        px, pz = math.cos(a)*rr, math.sin(a)*rr
        return (x + px, lip + px*lean[0] + pz*lean[1], z + pz)
    outer = [at_edge(k) for k in range(sides)]
    under = [(p[0], p[1] - thick, p[2]) for p in outer]
    inner = [(x + (p[0]-x)*0.45, p[1] + radius*0.10, z + (p[2]-z)*0.45) for p in outer]
    for k in range(sides):
        a0 = (k+0.5)/sides*math.tau
        b.quad(outer[k], outer[(k+1)%sides], inner[(k+1)%sides], inner[k], colour, out=(0,1,0))
        b.quad(outer[k], outer[(k+1)%sides], under[(k+1)%sides], under[k], shade, out=(math.cos(a0), 0, math.sin(a0)))
        b.quad(under[k], under[(k+1)%sides], inner[(k+1)%sides], inner[k], shade, out=(0,-1,0))
    b.face(inner, colour, out=(0,1,0))
def seafan(b, rng, at, size, colour, rib):
    """A sea fan: a rounded blade standing across the current, built as a mesh in rings and
    sectors with a few cells left out, so the water shows through it the way it should."""
    x, z = at; a = rng.uniform(0, math.tau)
    ax, az = math.cos(a), math.sin(a)
    W, H = 0.86*size, 0.98*size
    base = GROUND + 0.08
    spread = 1.30; n = 12; rings = [0.16, 0.42, 0.68, 0.87, 1.0]
    lobe = [1 - 0.13*abs(math.sin((k/n - 0.5)*2*spread*4.6)) - 0.09*((k/n - 0.5)*2*spread)**2 for k in range(n+1)]
    def place(i, k):
        th = (k/n - 0.5)*2*spread
        r = rings[i]*(lobe[k] if i == len(rings)-1 else 1.0)*rng.uniform(0.985, 1.015)
        return (x + ax*math.sin(th)*r*W, base + math.cos(th)*r*H, z + az*math.sin(th)*r*W)
    grid = [[place(i, k) for k in range(n+1)] for i in range(len(rings))]
    for i in range(len(rings)-1):
        for k in range(n):
            if i > 0 and rng.random() < 0.16: continue          # a hole worn through
            quad = [grid[i][k], grid[i][k+1], grid[i+1][k+1], grid[i+1][k]]
            col = rib if rng.random() < 0.22 else colour
            b.quad(*quad, col); b.quad(quad[3], quad[2], quad[1], quad[0], col)
    for k in range(n):
        b.tri((x, GROUND, z), grid[0][k], grid[0][k+1], rib)
        b.tri((x, GROUND, z), grid[0][k+1], grid[0][k], rib)
    prism(b, (x, GROUND - 0.02, z), 0.06*size, 0.10*size, 5, rib, rib, taper=0.6)
def sponge(b, rng, at, radius, height, colour, mouth):
    """A barrel sponge: a bulging vase with a ragged rim and a dark hollow down it."""
    x, z = at; sides = 9
    def ring(r, y, ragged=0.0):
        return [(x + math.cos(k/sides*math.tau)*r*(1 + math.sin(k*2.1)*0.06),
                 y + (math.sin(k*1.9)*ragged),
                 z + math.sin(k/sides*math.tau)*r*(1 + math.sin(k*2.1)*0.06)) for k in range(sides)]
    steps = [(0.62, 0.0), (0.98, 0.42), (0.88, 0.78), (1.0, 1.0)]
    hoops = [ring(radius*f, GROUND + height*t, 0.03 if t == 1.0 else 0.0) for f, t in steps]
    for h in range(len(hoops)-1):
        for k in range(sides):
            a0 = (k+0.5)/sides*math.tau
            b.quad(hoops[h][k], hoops[h][(k+1)%sides], hoops[h+1][(k+1)%sides], hoops[h+1][k], colour, out=(math.cos(a0), 0, math.sin(a0)))
    lip = hoops[-1]
    inner = [(x + (p[0]-x)*0.66, p[1] - 0.03, z + (p[2]-z)*0.66) for p in lip]
    floor = [(x + (p[0]-x)*0.5, GROUND + height*0.4, z + (p[2]-z)*0.5) for p in lip]
    for k in range(sides):
        a0 = (k+0.5)/sides*math.tau
        b.quad(lip[k], lip[(k+1)%sides], inner[(k+1)%sides], inner[k], mouth, out=(0,1,0))
        b.quad(inner[k], inner[(k+1)%sides], floor[(k+1)%sides], floor[k], mouth, out=(-math.cos(a0), 0, -math.sin(a0)))
    b.face(floor, mouth, out=(0,1,0))
def anemone(b, rng, at, size):
    """An anemone: a squat foot with a crown of leaning tentacles, pale at the ends."""
    x, z = at
    prism(b, (x, GROUND, z), 0.09*size, 0.07*size, 6, "coralrose", "coralrose", taper=1.15)
    for k in range(9):
        a = k/9*math.tau + rng.uniform(-0.2, 0.2)
        lean = rng.uniform(0.5, 1.0)
        prism(b, (x + math.cos(a)*0.06*size, GROUND + 0.06*size, z + math.sin(a)*0.06*size), 0.022*size,
              rng.uniform(0.13, 0.20)*size, 3, "coralrose", "anemtip", taper=0.35,
              tilt=(math.cos(a)*lean, math.sin(a)*lean))

def urchin(b, rng, at, size):
    """A sea urchin: a dark button with spines out of it every way."""
    x, z = at; c = (x, GROUND + 0.06*size, z)
    blob(b, c, (0.09*size, 0.07*size, 0.09*size), "urchin", "urchin", rng, sub=0, squash=0.6, moss_from=-2)
    for k in range(9):
        a = k/9*math.tau + rng.uniform(-0.25, 0.25); up = rng.uniform(0.35, 1.0)
        d = Vector((math.cos(a)*(1-up*0.55), up, math.sin(a)*(1-up*0.55))).normalized()
        end = Vector(c) + d*rng.uniform(0.13, 0.20)*size
        cylinder_along(b, (c[0]+d.x*0.05, c[1]+d.y*0.05, c[2]+d.z*0.05), tuple(end), 0.016*size, 3, "urchin", "urchin", taper=0.25)

def seagrass(b, rng, at, size=1.0, blades=9):
    """Seagrass: broad blades bending the one way, as if the swell were leaning on them."""
    x, z = at; drift = rng.uniform(0, math.tau)
    for k in range(blades):
        a = k/blades*math.tau + rng.uniform(-0.3, 0.3)
        h = rng.uniform(0.34, 0.58)*size; w = 0.035*size
        bend = rng.uniform(0.18, 0.34)
        base = (x + math.cos(a)*0.05, GROUND, z + math.sin(a)*0.05)
        mid = (base[0] + math.cos(drift)*bend*0.35, GROUND + h*0.6, base[2] + math.sin(drift)*bend*0.35)
        tip = (base[0] + math.cos(drift)*bend, GROUND + h, base[2] + math.sin(drift)*bend)
        sx, sz = math.cos(a+math.pi/2)*w, math.sin(a+math.pi/2)*w
        col = "seagrass" if k % 3 else "seagrass2"
        for (p0, p1, t0, t1) in ((base, mid, 1.0, 0.8), (mid, tip, 0.8, 0.35)):
            q = [(p0[0]-sx*t0,p0[1],p0[2]-sz*t0), (p0[0]+sx*t0,p0[1],p0[2]+sz*t0),
                 (p1[0]+sx*t1,p1[1],p1[2]+sz*t1), (p1[0]-sx*t1,p1[1],p1[2]-sz*t1)]
            b.quad(*q, col); b.quad(q[3], q[2], q[1], q[0], "seagrass2")

def clam(b, rng, at, size=1.0):
    """A giant clam: two ribbed shells standing open on their hinge, a bright lip between them."""
    x, z = at; a = rng.uniform(0, math.tau)
    ax, az = math.cos(a), math.sin(a); px, pz = math.cos(a+math.pi/2), math.sin(a+math.pi/2)
    mantle = rng.choice(["coralviolet", "coralteal", "coralpink"])
    L = 0.24*size; R = 0.30*size; lean = math.radians(52)
    hub = (x, GROUND + 0.01, z)
    for side in (1, -1):
        dx, dy, dz = px*side*math.sin(lean), math.cos(lean), pz*side*math.sin(lean)
        rim = []
        for k in range(6):
            u = (k/5 - 0.5)*2
            r = R*(1 - u*u*0.45)
            rim.append((x + ax*u*L + dx*r, GROUND + 0.01 + dy*r, z + az*u*L + dz*r))
        ends = [(x - ax*L, GROUND + 0.01, z - az*L), (x + ax*L, GROUND + 0.01, z + az*L)]
        out = (dx, dy, dz)
        b.tri(hub, ends[0], rim[0], "bone", out=out)
        for k in range(5):
            b.tri(hub, rim[k], rim[k+1], "shell" if k % 2 else "bone", out=out)
        b.tri(hub, rim[5], ends[1], "bone", out=out)
        under = [ends[0]] + rim + [ends[1]]
        b.face(list(reversed(under)), "sanddark", out=(-out[0], -out[1], -out[2]))
    lip = []
    for k in range(7):
        u = (k/6 - 0.5)*2
        lip.append((x + ax*u*L*0.92, GROUND + 0.05 + (0.05 if k % 2 else 0.015)*size, z + az*u*L*0.92))
    for k in range(6):
        for side in (1, -1):
            b.quad(lip[k], lip[k+1],
                   (lip[k+1][0]+px*0.07*size*side, lip[k+1][1]-0.02*size, lip[k+1][2]+pz*0.07*size*side),
                   (lip[k][0]+px*0.07*size*side, lip[k][1]-0.02*size, lip[k][2]+pz*0.07*size*side), mantle, out=(0,1,0))
def crust_patch(b, rng, centre, radius):
    """Coralline crust: a flat pink stain over the rock, its edge ragged."""
    n = 7
    rim = [(centre[0]+math.cos(k/n*math.tau)*radius*rng.uniform(0.5,1.2), GROUND + 0.004,
            centre[1]+math.sin(k/n*math.tau)*radius*rng.uniform(0.5,1.2)) for k in range(n)]
    b.face(rim, "crust", out=(0,1,0))
def polyps(b, rng, count, keep, colours=("coralpink","coralorange","coralyellow","coralviolet","coralteal")):
    """Small coral: pinhead colonies over the rock, a cheap way to make the floor busy."""
    for _ in range(count):
        for _t in range(20):
            x = rng.uniform(-INNER+0.12, INNER-0.12); z = rng.uniform(-INNER+0.12, INNER-0.12)
            if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
        s = rng.uniform(0.05, 0.10); col = rng.choice(colours)
        blob(b, (x, GROUND + s*0.3, z), (s, s*0.75, s*0.9), col, col, rng, sub=0, squash=0.35, moss_from=-2)

def rubble(b, rng, count, keep):
    """Old coral broken off and gone white: sticks and chips of it lying about."""
    for _ in range(count):
        for _t in range(20):
            x = rng.uniform(-INNER+0.12, INNER-0.12); z = rng.uniform(-INNER+0.12, INNER-0.12)
            if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
        a = rng.uniform(0, math.tau)
        if rng.random() < 0.55:
            l = rng.uniform(0.10, 0.20); r = rng.uniform(0.025, 0.04)
            cylinder_along(b, (x-math.cos(a)*l, GROUND+r, z-math.sin(a)*l), (x+math.cos(a)*l, GROUND+r*1.2, z+math.sin(a)*l),
                           r, 4, "bone", "shell", rng=rng, jitter=0.15, taper=0.9)
        else:
            s = rng.uniform(0.05, 0.09)
            blob(b, (x, GROUND+s*0.3, z), (s, s*0.5, s*0.8), "bone", "shell", rng, sub=0, squash=0.3, moss_from=-2)

def shell_bit(b, rng, at):
    x, z = at; a = rng.uniform(0, math.tau); s = rng.uniform(0.06, 0.09)
    col = rng.choice(["shell", "shellpink"])
    hinge = (x - math.cos(a)*s*0.6, GROUND + s*0.35, z - math.sin(a)*s*0.6)
    rim = [(x + math.cos(a + (k-2)*0.5)*s, GROUND, z + math.sin(a + (k-2)*0.5)*s) for k in range(5)]
    for k in range(4): b.tri(hinge, rim[k], rim[k+1], col, out=(0,1,0))
    b.face([hinge] + rim, "sanddark", out=(0,-1,0))

def starfish(b, rng, at):
    x, z = at; a0 = rng.uniform(0, math.tau); r = 0.13
    centre = (x, GROUND + 0.03, z)
    for k in range(5):
        a = a0 + k/5*math.tau
        tip = (x + math.cos(a)*r, GROUND + 0.015, z + math.sin(a)*r)
        l = (x + math.cos(a-0.35)*r*0.35, GROUND + 0.025, z + math.sin(a-0.35)*r*0.35)
        rr = (x + math.cos(a+0.35)*r*0.35, GROUND + 0.025, z + math.sin(a+0.35)*r*0.35)
        b.tri(centre, l, tip, "coralamber", out=(0,1,0)); b.tri(centre, tip, rr, "coralamber", out=(0,1,0))
        b.tri(centre, rr, (x + math.cos(a+0.63)*r*0.35, GROUND+0.025, z + math.sin(a+0.63)*r*0.35), "coralamber", out=(0,1,0))

# ---------------------------------------------------------------- the five
def tile(index):
    rng = random.Random(7000+index)
    b = Build(); keep = []
    def spots(n, margin=0.2, room=0.34):
        out = []
        for _ in range(n):
            for _t in range(30):
                x = rng.uniform(-INNER+margin, INNER-margin); z = rng.uniform(-INNER+margin, INNER-margin)
                if not any((x-kx)**2+(z-kz)**2 < kr*kr for (kx,kz,kr) in keep): break
            out.append((x,z)); keep.append((x,z,room))
        return out

    if index == 0:
        # the sand channel blown between the heads: rippled, nearly bare
        reef_body(b, rng, SAND, ripple=True)
        starfish(b, rng, spots(1, 0.3)[0])
        for at in spots(2, 0.25): shell_bit(b, rng, at)
        brain(b, rng, spots(1, 0.35, 0.5)[0], 0.24, 0.17, "coralteal", "coralteal2")
        rubble(b, rng, 3, keep)
        polyps(b, rng, 3, keep)
    elif index == 1:
        # the garden: the busy one, a head of brain coral and a stand of staghorn
        reef_body(b, rng, ROCK)
        brain(b, rng, spots(1, 0.45, 0.88)[0], 0.36, 0.28, "coralviolet", "coralplum")
        branchy(b, rng, spots(1, 0.42, 0.72)[0], 1.15, "coralpink", "anemtip")
        for at in spots(4, 0.18, 0.22): crust_patch(b, rng, at, rng.uniform(0.09, 0.15))
        polyps(b, rng, 8, keep)
    elif index == 2:
        # the shelf: worn rock, crust over it, urchins and a fan out of a crack
        reef_body(b, rng, ROCK)
        for at in spots(5, 0.16, 0.20): crust_patch(b, rng, at, rng.uniform(0.09, 0.16))
        seafan(b, rng, spots(1, 0.45, 0.72)[0], 0.66, "coralviolet", "coralplum")
        table(b, rng, spots(1, 0.45, 0.7)[0], 0.32, 0.26, "coralyellow", "coralamber")
        for at in spots(2, 0.25, 0.3): urchin(b, rng, at, rng.uniform(0.9, 1.3))
        anemone(b, rng, spots(1, 0.3)[0], 1.1)
        polyps(b, rng, 5, keep)
        rubble(b, rng, 2, keep)
    elif index == 3:
        # the seagrass bed: sand held together by grass, a sponge and a clam in it
        reef_body(b, rng, SAND)
        for at in spots(4, 0.25, 0.34): seagrass(b, rng, at, size=rng.uniform(0.9, 1.3))
        sponge(b, rng, spots(1, 0.35, 0.45)[0], 0.17, 0.36, "coralorange", "coralamber")
        clam(b, rng, spots(1, 0.35, 0.45)[0], 1.15)
        shell_bit(b, rng, spots(1, 0.2)[0])
        polyps(b, rng, 3, keep)
    else:
        # the old ground: coral long dead and gone white, with new colour coming back over it
        reef_body(b, rng, ["sand2", "rock3", "sand1", "rock"])
        branchy(b, rng, spots(1, 0.4, 0.55)[0], 1.0, "bone", "shell", arms=4)
        rubble(b, rng, 7, keep)
        urchin(b, rng, spots(1, 0.25, 0.3)[0], 1.0)
        for at in spots(3, 0.18, 0.22): crust_patch(b, rng, at, rng.uniform(0.08, 0.14))
        polyps(b, rng, 5, keep, colours=("coralpink", "coralrose", "coralteal"))
        seagrass(b, rng, spots(1, 0.25)[0], size=0.8, blades=6)
    return b.make("Reef Tile %d" % index)

# ---------------------------------------------------------------- previews
def sea(scene, sun, on):
    """The sea's own light: a blue-green room and a cooler sun, so the colours can be judged wet."""
    bg = scene.world.node_tree.nodes.get("Background")
    if on:
        bg.inputs[0].default_value = (0.06, 0.30, 0.36, 1); bg.inputs[1].default_value = 1.5
        sun.data.energy = 2.2; sun.data.color = (0.72, 0.94, 1.0)
    else:
        bg.inputs[0].default_value = (0.62, 0.72, 0.84, 1); bg.inputs[1].default_value = 1.0
        sun.data.energy = 3.2; sun.data.color = (1, 1, 1)

def main():
    scene, cam = ft["setup_scene"]()
    sun = bpy.data.objects["Sun"]
    mat = ft["material"](os.path.join(HERE, "TileWorldPalette.png"))
    tiles = [tile(i) for i in range(5)]
    for t in tiles:
        t.data.materials.append(mat)
        print("TILE %s verts %d faces %d" % (t.name, len(t.data.vertices), len(t.data.polygons)))

    for i, t in enumerate(tiles): t.location = ((i-2)*2.6, 0, 0)
    ft["look"](cam, (0, 1.0, 0), 11.5, 30, 0); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "reef-lineup.png"))

    for i, t in enumerate(tiles):
        for o in tiles: o.hide_render = (o is not t)
        ft["look"](cam, (t.location.x, 1.2, 0), 4.4, 26, 35); cam.data.lens = 45
        ft["render"](os.path.join(HERE, "reef-tile%d.png" % i))
    for o in tiles: o.hide_render = False

    rng = random.Random(11); placed = []
    for gx in range(-4, 4):
        for gz in range(-4, 4):
            ob = bpy.data.objects.new("P", rng.choice(tiles).data); scene.collection.objects.link(ob)
            ob.location = (gx*2.0+1.0, -(gz*2.0+1.0), rng.choice([0, 0, 0, 0.25, -0.25, 0.5]))
            ob.rotation_euler = (0, 0, rng.choice([0,1,2,3])*math.pi/2)
            placed.append(ob)
    for t in tiles: t.hide_render = True
    ft["look"](cam, (0, 1.2, 0), 15.5, 34, 28); cam.data.lens = 38
    ft["render"](os.path.join(HERE, "reef-patch.png"))
    ft["look"](cam, (0, 1.4, 1), 7.5, 13, 55); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "reef-low.png"))
    sea(scene, sun, True)
    ft["look"](cam, (0, 1.4, 1), 8.0, 16, 55); cam.data.lens = 40
    ft["render"](os.path.join(HERE, "reef-wet.png"))
    ft["look"](cam, (0, 1.2, 0), 15.5, 34, 28); cam.data.lens = 38
    ft["render"](os.path.join(HERE, "reef-wet-patch.png"))
    sea(scene, sun, False)
    for ob in placed: bpy.data.objects.remove(ob)

    for t in tiles:
        t.hide_render = False; t.location = (0,0,0); t.rotation_euler = (0,0,0)
        bpy.ops.object.select_all(action='DESELECT'); t.select_set(True); bpy.context.view_layer.objects.active = t
        bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, t.name + ".fbx"), use_selection=True,
                                 axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL',
                                 mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("DONE")

main()
