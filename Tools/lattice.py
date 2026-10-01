# Checks that a threshold with a wander on it can actually move a tile.
#
# The ground is terraced. A tile's top is BaseSurfaceY plus a whole number of StepHeight, so every
# height and every depth in the world is an exact multiple of a quarter metre, and the slope --
# the rise to a neighbour, divided by the span that counts as fully steep -- can only take the six
# values that makes. A rule written as `X + ripple * Y` is meant to break its own line up so it
# does not follow a contour; but if the band it sweeps, X plus or minus half of Y, contains none
# of the values the quantity can take, the noise cannot move a single tile anywhere and the rule
# comes out as a line ruled on an exact step.
#
# Two have been found this way. The desert's outcrop swept 0.23 to 0.37 while the slope only ever
# reads 0, 0.21, 0.42, 0.63, 0.83 or 1, so its edge was a contour on "a half-metre step to a
# neighbour". The reef's fringe put every boundary it drew exactly midway between two rungs of
# depth, an eighth of a metre from either, where its wander of eleven hundredths could not reach.
#
#     python3 Tools/lattice.py
import os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
CHUNK = os.path.join(HERE, "..", "Assets", "Scripts", "World", "Chunk.cs")
HEIGHT = os.path.join(HERE, "..", "Assets", "Scripts", "World", "WorldHeight.cs")

def consts(text, known=None):
    """Every `const float NAME = <arithmetic>;`, evaluated against what is already known. Seeded
    with the constants of the files read before it, or one written in terms of another -- which
    most of them now are, on purpose -- evaluates to nothing and is skipped in silence."""
    known = dict(known or {})
    for name, body in re.findall(r"const (?:float|int) (\w+) = ([^;=]+);", text):
        expr = body.strip()
        if expr.endswith("f"): expr = expr[:-1]
        expr = re.sub(r"(\d)f\b", r"\1", expr)
        expr = re.sub(r"\bWorldHeight\.(\w+)\b", r"\1", expr)
        try: known[name] = float(eval(expr, {"__builtins__": {}}, known))
        except Exception: pass
    return known

def main():
    text = open(CHUNK).read()
    known = consts(open(HEIGHT).read())
    known = consts(text, known)

    step = known.get("StepHeight")
    span = known.get("SlopeSpan")
    if step is None or span is None:
        print("lattice: cannot find StepHeight or SlopeSpan"); return 1

    # The two lattices, and which one each wander is measured against. A band has to be checked
    # against its OWN lattice: tested against either, a slope threshold is excused by the depth
    # lattice, which has a point every quarter metre and so forgives any band wider than that --
    # the first cut of this check passed on the very rule it was written for.
    #
    # The table is the one thing here not read out of the source, so every wander found in the
    # source must appear in it or this fails: a new wander is unclassified until somebody says
    # what it is drawn on.
    slopes = sorted({min(1.0, k * step / span) for k in range(0, 8)})
    rungs = [k * step for k in range(0, 40)]

    ON = {
        "SteepWander": ("the slope", slopes),   # steep > ScreeLine / OutcropLine
        "DeepWander":  ("a depth", rungs),      # underBy - (DeepWater + ...)
        "ReefWander":  ("a depth", rungs),      # ReefLineAt, against underBy
        "ReedWander":  ("a height", rungs),     # ReedLineAt, against -underBy
    }

    bad = 0
    looked = 0
    seen = set()

    # ReefLineAt and ReedLineAt write theirs with a Perlin lookup inline rather than `ripple`,
    # so they are named here as well; the table above still has to classify them.
    pairs = re.findall(r"\b(\w+) \+ ripple \* (\w+)\b", text)
    pairs += [("ReefDepth", "ReefWander"), ("ReedWet", "ReedWander")]

    for mid, wander in pairs:
        if (mid, wander) in seen: continue
        seen.add((mid, wander))

        # Not skipped in silence: a threshold this cannot work out is one it cannot check.
        if mid not in known or wander not in known:
            print("DISAGREE   %s + ripple * %s cannot be worked out from the source, so nothing"
                  " here can say whether its wander reaches the ground" % (mid, wander))
            bad += 1
            continue

        looked += 1
        lo, hi = known[mid] - known[wander] / 2, known[mid] + known[wander] / 2

        if wander not in ON:
            print("DISAGREE   %s is a wander nothing here says what it is drawn on" % wander)
            bad += 1
            continue

        what, lattice = ON[wander]

        if any(lo < v < hi for v in lattice): continue

        print("DISAGREE   %s + ripple * %s sweeps %.3f to %.3f, and %s never takes a value there,"
              " so the wander cannot move one tile" % (mid, wander, lo, hi, what))
        bad += 1

    print("%d wanders" % looked)
    return bad

if __name__ == "__main__":
    raise SystemExit(1 if main() else 0)
