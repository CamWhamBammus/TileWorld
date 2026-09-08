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

def parse(text):
    name, hexed = text.split("=")
    hexed = hexed.lstrip("#")
    return name, tuple(int(hexed[i:i+2], 16) for i in (0, 2, 4))

if __name__ == "__main__":
    add([parse(a) for a in sys.argv[1:]])
