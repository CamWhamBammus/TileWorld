# Fill blocks for Tile World: plain bodies laid under a tile wherever the ground drops away further
# than a tile is deep, so a cliff is solid to its foot. Two of them, earth and rock. Exports FBX.
import bpy, os
HERE = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(HERE, "forest_tiles.py")).read().replace("\nmain()\n", "\n")
ft = {"__file__": os.path.join(HERE, "forest_tiles.py"), "__name__": "forest_tiles"}
exec(compile(src, "forest_tiles.py", "exec"), ft)
Build = ft["Build"]; TOP, BOTTOM, HALF = ft["TOP"], ft["BOTTOM"], ft["HALF"]

def block(name, upper, lower):
    b = Build()
    corners = [(-HALF,-HALF),(HALF,-HALF),(HALF,HALF),(-HALF,HALF)]
    for k in range(4):
        (x0,z0),(x1,z1) = corners[k], corners[(k+1)%4]
        outward = ((x0+x1)*0.5, 0, (z0+z1)*0.5)
        b.quad((x0,0.1,z0),(x1,0.1,z1),(x1,TOP,z1),(x0,TOP,z0), upper, out=outward)
        b.quad((x0,BOTTOM,z0),(x1,BOTTOM,z1),(x1,0.1,z1),(x0,0.1,z0), lower, out=outward)
    b.quad((-HALF,TOP,-HALF),(HALF,TOP,-HALF),(HALF,TOP,HALF),(-HALF,TOP,HALF), upper, out=(0,1,0))
    b.quad((-HALF,BOTTOM,-HALF),(-HALF,BOTTOM,HALF),(HALF,BOTTOM,HALF),(HALF,BOTTOM,-HALF), lower, out=(0,-1,0))
    return b.make(name)

bpy.ops.wm.read_factory_settings(use_empty=True)
for ob in (block("Fill Earth", "earth", "earth2"), block("Fill Rock", "frostrock", "frostrock2")):
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(HERE, ob.name + ".fbx"), use_selection=True, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL', mesh_smooth_type='OFF', add_leaf_bones=False, bake_space_transform=True)
    print("FILL", ob.name, len(ob.data.vertices))
print("DONE")
