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

def blend_pairs():
    """The mixed-ground pair order, which is written down in four places and read as one.

    blend_tiles.py builds the tiles and names the files; BlendSet.cs and BlendDrySet.cs import them
    in their own order; Chunk.cs maps a pair index to a category with a hand-written table. Nothing
    pairs those up, and the index formula in Chunk trusts all three silently.
    """
    chunk = open(os.path.join(PROJECT, "Assets", "Scripts", "World", "Chunk.cs")).read()
    table = re.search(r"BlendCategory\s*=\s*\{([^}]*)\}", chunk)
    per = re.search(r"VariantsPerCategory = (\d+)", chunk)
    if not table or not per: return 0
    step = int(per.group(1))
    categories = [int(n) for n in re.findall(r"\b(\d+)\b", re.sub(r"//[^\n]*", "", table.group(1)))]

    order = re.search(r"ORDER = \[([^\]]*)\]", open(os.path.join(HERE, "blend_tiles.py")).read())
    if not order: return 0
    families = [w.strip().strip("\"'") for w in order.group(1).split(",") if w.strip()]
    pairs = [(a, b) for i, a in enumerate(families) for b in families[i + 1:]]

    if len(pairs) != len(categories):
        print("DISAGREE   blend_tiles.py makes %d pairs but Chunk.BlendCategory has %d entries"
              % (len(pairs), len(categories)))
        return 1

    # what id each series actually got, from the editor tools' own generated names
    names = {}
    for tool in ("BlendSet.cs", "BlendDrySet.cs"):
        path = os.path.join(PROJECT, "Assets", "Editor", tool)
        if not os.path.exists(path): continue
        text = open(path).read()
        first = re.search(r"First\w*Id = (\d+)", text)
        listed = re.search(r"Order = \{([^}]*)\}", text)
        steps = re.search(r"(?:Variants|Steps) = (\d+)", text)
        if not (first and listed and steps): continue
        those = [w.strip().strip('"') for w in listed.group(1).split(",") if w.strip()]
        at = int(first.group(1)); width = int(steps.group(1))
        if tool == "BlendSet.cs":
            for i, a in enumerate(those):
                for b in those[i + 1:]:
                    names[(a.lower(), b.lower())] = at; at += width
        else:
            for a in those:
                names[tuple(sorted((a.lower(), "dry")))] = at; at += width

    bad = 0
    for pair, category in zip(pairs, categories):
        key = tuple(sorted(pair)) if tuple(sorted(pair)) in names else pair
        if key not in names: continue
        if names[key] != category * step:
            print("DISAGREE   %s-%s is category %d (ids from %d) but its tiles were imported at %d"
                  % (pair[0], pair[1], category, category * step, names[key]))
            bad += 1
    return bad

# The pack's own bands, which we import but never ask for: its five grass bands and its stone.
# They stay in the library because the definitions are cheap and removing them renumbers nothing.
KNOWN_SPARE = set(range(0, 25)) | set(range(30, 35))

def category_bases(spare):
    """The four numbers every editor tool and the chunk have to agree on, and never say out loud.

    A tool writes its ids as FirstSomethingId + series * Variants + variant. The chunk reads them
    back as category * VariantsPerCategory + variant. So each tool's Variants has to be the chunk's
    VariantsPerCategory, each FirstId has to land on a category boundary, and no category may sit
    past the end of the table. Set Variants to 6 in one tool and its second series lands on top of
    the next tool's first, which is how the reef and the fill blocks collided.
    """
    chunk = open(os.path.join(PROJECT, "Assets", "Scripts", "World", "Chunk.cs")).read()
    per = re.search(r"VariantsPerCategory = (\d+)", chunk)
    total = re.search(r"Categories = (\d+)", chunk)
    if not per: return 0
    step = int(per.group(1))
    bad = 0

    for path in sorted(glob.glob(os.path.join(PROJECT, "Assets", "Editor", "*.cs"))):
        text = open(path).read()
        tool = os.path.basename(path)
        width = re.search(r"public const int [^;]*?(?:Variants|Steps) = (\d+)", text)
        if width and int(width.group(1)) != step:
            print("DISAGREE   %s makes %s tiles a series but Chunk asks for %d at a time"
                  % (tool, width.group(1), step)); bad += 1
        for name, first in re.findall(r"public const int (?:\w+ = \d+, )?(\w*First\w*Id) = (\d+)", text):
            if int(first) % step:
                print("DISAGREE   %s says %s = %s, which is not the start of a band of %d"
                      % (tool, name, first, step)); bad += 1

    if total:
        for name, value in re.findall(r"(\w+Category) = (\d+)", chunk):
            if int(value) >= int(total.group(1)):
                print("DISAGREE   Chunk.%s = %s but there are only %s categories"
                      % (name, value, total.group(1))); bad += 1

    # the fill blocks under cliffs, named twice
    fill = os.path.join(PROJECT, "Assets", "Editor", "FillSet.cs")
    if os.path.exists(fill):
        theirs = dict(re.findall(r"(\w+)Id = (\d+)", open(fill).read()))
        for name, value in re.findall(r"Fill(\w+)Id = (\d+)", chunk):
            if name in theirs and theirs[name] != value:
                print("DISAGREE   FillSet.cs builds the %s block as %s but Chunk asks for %s"
                      % (name.lower(), theirs[name], value)); bad += 1

    loose = sorted(i for i in spare if i not in KNOWN_SPARE)
    if loose:
        print("DISAGREE   %s were imported but no category can ask for them"
              % ", ".join(str(i) for i in loose)); bad += 1
    return bad

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

    bad += blend_pairs()
    bad += category_bases(spare)

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
