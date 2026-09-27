# Measures how far every piece of every ground tile actually reaches, and says when one runs off
# its own block. Run under Blender, one tile script at a time:
#
#     for f in Tools/*_tiles.py Tools/reef_verge.py; do
#         blender -b -P Tools/overhang.py -- "$(basename "$f")"
#     done
#
# A tile is a block two metres across: HALF either side of its middle in x and z, and nothing on
# it should reach past that, because the tile beside it starts there. Nothing in the game can
# see this -- a mesh is instanced and knows nothing about its neighbours -- so a crack, a decal
# or a stone that overhangs is drawn lying across whatever was laid next to it, and the only way
# to find it is to measure. Two sets had it: the barren rock's cracks reached 1.23 and the
# summit's splits 1.62.
#
# It reports the y range too. A tile's body top runs to about TOP, so a piece whose lowest point
# is well above that is floating -- the fault the scree's chips had, four to sixteen centimetres
# clear of the rock they were supposed to be lying on.
import bpy, sys, os

HERE = os.path.dirname(os.path.abspath(__file__))
name = sys.argv[sys.argv.index("--") + 1]

src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
HALF, TOP = ft["HALF"], ft["TOP"]

# Build the set. The scripts render and export as they go; that costs a few seconds and is the
# price of measuring what is actually shipped rather than a second copy of the maths.
path = os.path.join(HERE, name)
exec(compile(open(path).read(), name, "exec"), {"__file__": path, "__name__": "__main__"})

SLACK = 0.005          # a millimetre or two of float noise is not an overhang

# Local coordinates, and the game's axes rather than Blender's. Build.make turns the mesh a
# quarter about X on the way in -- `me.from_pydata([(v.x, -v.z, v.y) for v in self.verts], ...)`
# -- so the game's two horizontal axes are Blender's x and y, and the game's up is Blender's z.
# Measured the other way round, every tile in the game reads as overhanging by its own height,
# which is how this tool read on its first run.
worst = []
for ob in bpy.data.objects:
    if ob.type != "MESH" or not ob.data.vertices: continue
    out = max(max(abs(v.co.x), abs(v.co.y)) for v in ob.data.vertices)
    low = min(v.co.z for v in ob.data.vertices)
    high = max(v.co.z for v in ob.data.vertices)
    worst.append((out, low, high, ob.name))

# A couple of these scripts build trees beside their tiles. A tree is planted on a tile rather
# than laid as one, and a canopy is meant to reach over its neighbours, so it is not measured.
# Nothing laid as ground reaches two metres up: the tallest detail on any tile in the game is the
# reef's coral head at 1.83, and the shortest tree is a sapling at 2.37.
PLANTED = 2.0

worst.sort(reverse=True)
bad = 0
laid = 0

for out, low, high, obname in worst:
    if high >= PLANTED: continue
    laid += 1
    if out > HALF + SLACK:
        print("OVERHANG   %-22s reaches %.3f, and the block stops at %.2f" % (obname, out, HALF))
        bad += 1

tiles = [w for w in worst if w[2] < PLANTED]
print("%s: %d tiles laid, widest reach %.3f, from %.2f to %.2f high, %d over the edge"
      % (name, laid, max((w[0] for w in tiles), default=0),
         min((w[1] for w in tiles), default=0), max((w[2] for w in tiles), default=0), bad))
