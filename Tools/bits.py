# Checks that a hash still has bits left where the code reaches for them.
#
# Everything in the world that is scattered -- where a plant stands off the middle of its tile,
# which way it faces, how tall it is -- is taken from one hash, a different slice of it for each
# question. The slice is written as (roll >> 23) % 1000: shift the bits you have already used out
# of the way, then take a number out of what is left. Shift 23 of a 32-bit hash and nine bits are
# left, which is 511 at most, so the % 1000 does nothing and the answer can never be more than
# half of what the code asks for. The plant is scattered to one side of its tile and never the
# other, and it looks like a field planted in rows.
#
# Nothing errors. The number is in range, it is just never the top half of it. Two places in the
# game had this and both had been there since the day they were written.
#
#     python3 Tools/bits.py
import glob, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.join(HERE, "..")

# How much headroom a slice should have over the range it is asked for. At the same size the top
# value is unreachable; a few times over and the low numbers still come up oftener than the high
# ones, because the wrap lands part way. Eight is a bias under a percent.
MARGIN = 8

def width_of(text, name, before):
    """Whether the thing being shifted is signed, which costs it a bit. Nearest declaration above."""
    last = None
    for m in re.finditer(r"\b(uint|int)\s+" + re.escape(name) + r"\b\s*[=;,)]", text[:before]):
        last = m.group(1)
    if last is None: return None
    return 32 if last == "uint" else 31

def main():
    bad = 0
    looked = 0

    roots = [os.path.join(PROJECT, "Assets", "Scripts"), os.path.join(PROJECT, "Assets", "Editor")]
    files = []
    for root in roots:
        files += sorted(glob.glob(os.path.join(root, "**", "*.cs"), recursive=True))

    for path in files:
        text = open(path).read()
        for m in re.finditer(r"\(\s*([A-Za-z_]\w*)\s*>>\s*(\d+)\s*\)\s*%\s*(\d+)", text):
            name, shift, span = m.group(1), int(m.group(2)), int(m.group(3))
            width = width_of(text, name, m.start())
            if width is None: continue            # not a local we can size; say nothing rather than guess

            looked += 1
            reach = 1 << max(0, width - shift)
            line = text[:m.start()].count("\n") + 1
            where = os.path.relpath(path, PROJECT) + ":" + str(line)

            if reach < span:
                print("DISAGREE   %s  (%s >> %d) %% %d can only reach %d, so the top %d%% never comes up"
                      % (where, name, shift, span, reach - 1, round(100 * (1 - reach / span))))
                bad += 1
            elif reach < span * MARGIN:
                print("DISAGREE   %s  (%s >> %d) %% %d has only %d values to draw from, so the low ones come up oftener"
                      % (where, name, shift, span, reach))
                bad += 1

    print("%d slices" % looked)
    return bad

if __name__ == "__main__":
    raise SystemExit(1 if main() else 0)
