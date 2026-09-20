# Checks the handful of numbers that are written down in both languages and have to agree.
#
# Every tile in the world is built by a Blender script here in Tools, and the game assumes the shape
# those scripts produce: where the top of a tile is, how deep its body goes, how wide it is. Change
# TOP in forest_tiles.py and nothing errors -- the tiles come out a different height, the player walks
# through the floor or hovers over it, and the number that would have told you is in a C# file.
#
#     python3 Tools/shapes.py
import os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.join(HERE, "..")

def python_geometry():
    text = open(os.path.join(HERE, "forest_tiles.py")).read()
    found = {}
    for name in ("TOP", "BOTTOM", "HALF", "INNER"):
        m = re.search(r"\b%s = (-?[\d.]+)" % name, text)
        if m: found[name] = float(m.group(1))
    return found

def csharp(path, pattern):
    text = open(os.path.join(PROJECT, path)).read()
    m = re.search(pattern, text)
    return float(m.group(1)) if m else None

def main():
    py = python_geometry()
    missing = [n for n in ("TOP", "BOTTOM", "HALF") if n not in py]
    if missing:
        print("CANNOT READ  forest_tiles.py has no %s" % ", ".join(missing))
        return 1

    checks = [
        ("a tile's top is the ground you walk on",
         py["TOP"],
         csharp("Assets/Scripts/World/WorldHeight.cs", r"BaseSurfaceY = ([\d.]+)f"),
         "forest_tiles.py TOP", "WorldHeight.BaseSurfaceY"),

        ("a tile's body is as deep as the fill blocks laid under it",
         round(py["TOP"] - py["BOTTOM"], 4),
         csharp("Assets/Scripts/World/Chunk.cs", r"FillDepth = ([\d.]+)f"),
         "forest_tiles.py TOP minus BOTTOM", "Chunk.FillDepth"),

        ("a tile is as wide as the grid it is laid on",
         py["HALF"] * 2,
         csharp("Assets/Scripts/World/WorldGrid.cs", r"TileSize = (\d+)"),
         "forest_tiles.py HALF doubled", "WorldGrid.TileSize"),
    ]

    bad = 0
    for what, mine, theirs, a, b in checks:
        if theirs is None:
            print("CANNOT READ  %s (%s)" % (b, what)); bad += 1
        elif abs(mine - theirs) > 0.0005:
            print("DISAGREE     %s is %g but %s is %g -- %s" % (a, mine, b, theirs, what)); bad += 1

    print("%d shapes checked, %d problems" % (len(checks), bad))
    return bad

if __name__ == "__main__":
    raise SystemExit(1 if main() else 0)
