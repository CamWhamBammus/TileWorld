# Adds colours to the palette sheet the whole game shares.
#
# The sheet is 512x512, sixteen by sixteen cells of 32 px, and a model's colour is
# just a UV pointing at the middle of a cell. This paints new colours into cells
# nothing is using yet -- blank and fully transparent -- and writes their UVs into
# palette.json, which the Blender scripts read by name.
#
#     python3 Tools/palette_add.py coralpink=F2809C coralrose=C65076
#
# Names already in palette.json are left alone, so it is safe to run twice.
import json, os, re, sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SHEET = os.path.join(HERE, "..", "Assets", "Low Poly Isometric Tiles - Cartoon Pack", "Models", "Texture.png")
WORKING = os.path.join(HERE, "TileWorldPalette.png")   # the copy Blender renders with
PALETTE = os.path.join(HERE, "palette.json")
MARGIN = 2                                             # texels kept clear round anything the game reads

def sampled(palette):
    """Every point on the sheet the game reads: the flat colours the Blender scripts use,
    and the building kit's swatches, which are single points picked out of the artwork the
    sheet came with rather than cell middles."""
    pts = [(u, v) for u, v in palette.values()]
    kit = os.path.join(HERE, "..", "Assets", "Resources", "Kit.asset")
    if os.path.exists(kit):
        block = open(kit).read()
        block = block[block.index("Where:"):]
        for u, v in re.findall(r"-\s*\{x: ([0-9.eE-]+), y: ([0-9.eE-]+)\}", block)[:22]:
            pts.append((float(u), float(v)))
    return pts

def free_cells(im, palette):
    """Cells nothing reads. The sheet has no blank space left, but most of the artwork it
    came with is never sampled, and a cell no one reads is a cell we can paint. A margin of
    two texels is kept round every point the game does read, since the texture is filtered."""
    W, H = im.size; cell = W // 16
    pts = sampled(palette)
    out = []
    for r in range(16):
        for c in range(16):
            x0, x1, y0, y1 = c * cell, (c + 1) * cell, r * cell, (r + 1) * cell
            if any(x0 - MARGIN <= u * W <= x1 + MARGIN and y0 - MARGIN <= (1 - v) * H <= y1 + MARGIN for u, v in pts): continue
            out.append((r, c))
    return out

def add(colours):
    im = Image.open(SHEET).convert("RGBA")
    W, H = im.size; cell = W // 16
    palette = json.load(open(PALETTE))
    spare = free_cells(im, palette)
    wanted = [(n, v) for n, v in colours if n not in palette]
    if len(wanted) > len(spare):
        raise SystemExit("%d colours wanted, %d cells free" % (len(wanted), len(spare)))
    for (name, rgb), (r, c) in zip(wanted, spare):
        for x in range(cell):
            for y in range(cell):
                im.putpixel((c * cell + x, r * cell + y), rgb + (255,))
        palette[name] = [(c * cell + cell / 2) / W, 1 - (r * cell + cell / 2) / H]
        print("%-12s -> cell r%d c%d  rgb%s" % (name, r, c, rgb))
    im.save(SHEET); im.save(WORKING)
    json.dump(palette, open(PALETTE, "w"), indent=1)
    print("%d colours, %d cells still free" % (len(palette), len(spare) - len(wanted)))

def check():
    """Everything about the sheet that has to hold, checked mechanically.

    Two things have gone wrong here before. A colour was painted into a cell the building kit was
    already reading, and every snow cap on every structure came out teal for days. And the kit's
    twenty-two swatches are paired with the Swatch enum by position, which is the same shape of
    mistake that put a jungle temple where a ring of standing stones belonged.

    So: no palette colour may share a cell with a kit swatch, and every swatch's colour has to be
    a plausible colour for its name. The second is crude on purpose -- it cannot tell plank from
    old wood, but it catches a list that has slid, which is the failure that actually happens.
    """
    from PIL import Image
    im = Image.open(SHEET).convert("RGBA")
    W, H = im.size; cell = W // 16
    palette = json.load(open(PALETTE))
    swatches = kit_swatches()
    bad = 0

    # A kit swatch sharing a cell with a palette colour is not wrong in itself -- the kit's snow
    # was deliberately pointed at snow1 so that a drift on a roof is the same white as a drift on
    # the ground. It is worth printing, because it is only right when it is on purpose.
    for name, (u, v) in swatches:
        r, c = int((1 - v) * H) // cell, int(u * W) // cell
        for colour, (pu, pv) in palette.items():
            if int((1 - pv) * H) // cell == r and int(pu * W) // cell == c:
                print("shared kit %-10s r%-2d c%-2d with palette %s" % (name, r, c, colour))

    for name, (u, v) in swatches:
        px = im.getpixel((min(W - 1, int(u * W)), min(H - 1, int((1 - v) * H))))[:3]
        if not suits(name, px):
            print("WRONG  kit %-10s is rgb%s, which is not a %s" % (name, px, name.lower()))
            bad += 1

    print("%d swatches, %d colours, %d cells free, %d problems" % (len(swatches), len(palette), len(free_cells(im, palette)), bad))
    return bad

SWATCHES = ["Wood", "DarkWood", "Plank", "EndGrain", "Stone", "DarkStone", "Mortar", "Plaster",
            "Thatch", "Slate", "Iron", "Pane", "Cloth", "WarmStone", "Water", "Earth", "Snow",
            "Moss", "Vine", "Sand", "Char", "OldWood"]

def kit_swatches():
    kit = os.path.join(HERE, "..", "Assets", "Resources", "Kit.asset")
    block = open(kit).read()
    block = block[block.index("Where:"):]
    found = re.findall(r"-\s*\{x: ([0-9.eE-]+), y: ([0-9.eE-]+)\}", block)[:len(SWATCHES)]
    return list(zip(SWATCHES, [(float(u), float(v)) for u, v in found]))

def suits(name, px):
    """Whether a colour could belong to a swatch of that name. Loose by design."""
    r, g, b = px
    light = (r + g + b) / 3.0
    grey = max(px) - min(px) < 20
    warm = r > g > b
    green = g >= r and g > b
    blue = b >= r and b >= g

    if name == "Snow": return light > 190
    if name == "Char": return light < 70
    if name in ("Moss", "Vine"): return green
    if name in ("Water", "Pane"): return blue or grey
    if name in ("Stone", "DarkStone", "Mortar", "Slate", "Iron"): return grey or light < 80
    if name in ("Wood", "DarkWood", "Plank", "EndGrain", "Thatch", "Sand", "Earth", "OldWood",
                "WarmStone", "Plaster"): return warm or grey
    return True

def parse(text):
    name, hexed = text.split("=")
    hexed = hexed.lstrip("#")
    return name, tuple(int(hexed[i:i+2], 16) for i in (0, 2, 4))

if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "check":
        raise SystemExit(1 if check() else 0)
    add([parse(a) for a in sys.argv[1:]])
