# Stitches a folder of rendered frames into an animated GIF. Blender's own Python has no PIL and
# this build has no video encoder, so the frames come out as stills and this puts them together.
#
#     python3 Tools/gif.py <frames folder> <out.gif> [fps]
import glob, os, sys
from PIL import Image

def stitch(folder, out, fps=24, colours=128):
    shots = sorted(glob.glob(os.path.join(folder, "*.png")))
    if not shots: raise SystemExit("no frames in " + folder)
    pics = [Image.open(p).convert("RGB").quantize(colors=colours, method=Image.MEDIANCUT) for p in shots]
    pics[0].save(out, save_all=True, append_images=pics[1:], duration=int(1000/fps), loop=0, optimize=True)
    print("%-28s %3d frames  %5.1f MB" % (os.path.basename(out), len(pics), os.path.getsize(out)/1e6))

if __name__ == "__main__":
    stitch(sys.argv[1], sys.argv[2], int(sys.argv[3]) if len(sys.argv) > 3 else 24)
