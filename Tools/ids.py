# Checks the tile ids, which nothing else does.
#
# A tile definition is an asset with a blockID in it; the chunk asks for ids by number and the library
# hands back the definition with that number. Nothing anywhere asserts that the numbers line up, and
# they have collided before: the reef's five tiles were given 95 to 99, which the fill blocks under
# cliffs already had, and the fills had to be moved twice.
#
#     python3 Tools/ids.py          list what is there and say what is wrong
import glob, json, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.join(HERE, "..")
DEFS = os.path.join(PROJECT, "Assets", "ScriptableObjects")
LIBRARY = os.path.join(DEFS, "TileLibrary.asset")

def definitions():
    """Every tile definition on disk: its id, its file, and the guid the library would use."""
    out = []
    for path in sorted(glob.glob(os.path.join(DEFS, "T*.asset"))):
        text = open(path).read()
        found = re.search(r"blockID: (-?\d+)", text)
        if not found: continue
        meta = path + ".meta"
        guid = None
        if os.path.exists(meta):
            g = re.search(r"^guid: ([0-9a-f]+)", open(meta).read(), re.M)
            guid = g.group(1) if g else None
        out.append((int(found.group(1)), os.path.basename(path), guid))
    return out

def ranges():
    """What each editor tool says its ids are, from its own constants."""
    out = {}
    for path in sorted(glob.glob(os.path.join(PROJECT, "Assets", "Editor", "*.cs"))):
        text = open(path).read()
        for name, first in re.findall(r"public const int (\w*First\w*Id) = (\d+)", text):
            count = re.search(r"(?:Variants|Steps) = (\d+)", text)
            out.setdefault(os.path.basename(path), []).append((name, int(first), int(count.group(1)) if count else None))
        for a, b in re.findall(r"public const int (\w+Id) = (\d+), \w+Id = (\d+)", text) and [] or []:
            pass
        pair = re.search(r"public const int (\w+Id) = (\d+), (\w+Id) = (\d+);", text)
        if pair:
            out.setdefault(os.path.basename(path), []).append((pair.group(1), int(pair.group(2)), 1))
            out.setdefault(os.path.basename(path), []).append((pair.group(3), int(pair.group(4)), 1))
    return out

def main():
    defs = definitions()
    ids = [i for i, _, _ in defs]
    library = open(LIBRARY).read() if os.path.exists(LIBRARY) else ""
    bad = 0

    seen = {}
    for i, name, _ in defs:
        if i in seen:
            print("DUPLICATE  blockID %d in both %s and %s" % (i, seen[i], name)); bad += 1
        seen[i] = name

    orphans = [name for _, name, guid in defs if guid and guid not in library]
    for name in orphans:
        print("ORPHAN     %s is not in the library, so the chunk can never be given it" % name); bad += 1

    for tool, entries in sorted(ranges().items()):
        for name, first, count in entries:
            if count is None: continue
            missing = [first + k for k in range(count) if first + k not in seen]
            if missing:
                print("MISSING    %s says %s = %d for %d, but %s have no definition"
                      % (tool, name, first, count, missing)); bad += 1

    # Ids that exist but that the chunk has no way to select. They are not a fault, but they are a
    # trap: the reef was given 95 to 99 partly because nothing said which numbers were spoken for.
    chunk = open(os.path.join(PROJECT, "Assets", "Scripts", "World", "Chunk.cs")).read()
    per = re.search(r"VariantsPerCategory = (\d+)", chunk)
    used = set()
    if per:
        step = int(per.group(1))
        for name, value in re.findall(r"(\w*Category) = (\d+)", chunk):
            for k in range(step): used.add(int(value) * step + k)
        for name, value in re.findall(r"(Fill\w*Id) = (\d+)", chunk): used.add(int(value))
        for value in re.findall(r"FillRockId = (\d+)", chunk): used.add(int(value))
        for value in re.findall(r"BlendCategory\s*=\s*{([^}]*)}", chunk):
            for n in re.findall(r"\d+", value):
                for k in range(step): used.add(int(n) * step + k)
    spare = sorted(i for i in ids if i not in used) if used else []
    if spare:
        runs2, start2 = [], spare[0]
        for a, b in zip(spare, spare[1:] + [None]):
            if b != a + 1: runs2.append((start2, a)); start2 = b
        print("spare      %d definitions no category can ask for: %s"
              % (len(spare), ", ".join("%d" % a if a == b else "%d-%d" % (a, b) for a, b in runs2)))

    ids.sort()
    runs, start = [], ids[0] if ids else 0
    for a, b in zip(ids, ids[1:] + [None]):
        if b != a + 1:
            runs.append((start, a)); start = b
    print("%d definitions: %s" % (len(ids), ", ".join("%d" % a if a == b else "%d-%d" % (a, b) for a, b in runs)))
    print("%d problems" % bad)
    return bad

if __name__ == "__main__":
    raise SystemExit(1 if main() else 0)
