"""
Blender (headless): merge the Meshy raccoon (rigged GLB) and its animation GLBs into one FBX for Unity.
  blender -b --python tools/raccoon_to_fbx.py -- <art dir> <out fbx> <out texture png>
Each art/raccoon/anim_<Name>.glb holds one clip on the same skeleton. Clips are renamed to <Name>,
made in-place (no horizontal hips travel), and "Crawl" is the backward crawl played in reverse.
"""
import bpy, sys, os, glob

art, out_fbx, out_tex = sys.argv[-3:]
bpy.ops.wm.read_factory_settings(use_empty=True)

def import_glb(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    return [o for o in bpy.data.objects if o not in before]

base = import_glb(os.path.join(art, "raccoon_rigged.glb"))
for o in list(base):
    if o.name.startswith("Icosphere"):
        bpy.data.objects.remove(o)
armature = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
mesh = next(o for o in bpy.data.objects if o.type == 'MESH')
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)  # the base file's own pose clip

def curves(action):
    """F-curves of an action (Blender 5 layered actions, or the legacy list)."""
    out = []
    if hasattr(action, "layers") and len(action.layers):
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    out.extend((bag, fc) for fc in bag.fcurves)
    elif hasattr(action, "fcurves"):
        out.extend((action, fc) for fc in action.fcurves)
    return out

def clean(action, reverse=False):
    hips = 'pose.bones["Hips"].location'
    for owner, fc in curves(action):
        # Clips that travel (push, jump…): pin the hips' horizontal travel to the start so they play in place.
        if fc.data_path == hips and fc.array_index in (0, 2):
            vals = [kp.co.y for kp in fc.keyframe_points]
            if vals and max(vals) - min(vals) > 10:
                v0 = vals[0]
                for kp in fc.keyframe_points:
                    kp.co.y = v0; kp.handle_left.y = v0; kp.handle_right.y = v0
                fc.update()
    if reverse:
        all_fc = [fc for _, fc in curves(action)]
        end = max((kp.co.x for fc in all_fc for kp in fc.keyframe_points), default=0)
        for fc in all_fc:
            pts = [(end - kp.co.x, kp.co.y) for kp in fc.keyframe_points]
            for kp, (x, y) in zip(fc.keyframe_points, pts):
                kp.co.x = x
                kp.handle_left.x = end - kp.handle_left.x
                kp.handle_right.x = end - kp.handle_right.x
                kp.handle_left, kp.handle_right = kp.handle_right.copy(), kp.handle_left.copy()
            fc.update()

names = []
for path in sorted(glob.glob(os.path.join(art, "anim_*.glb"))):
    name = os.path.basename(path)[5:-4]
    objs = import_glb(path)
    act = None
    for o in objs:
        if o.type == 'ARMATURE' and o.animation_data and o.animation_data.action:
            act = o.animation_data.action
    for o in objs:
        bpy.data.objects.remove(o, do_unlink=True)
    if not act:
        print("NO ACTION in", path); continue
    act.name = name
    act.use_fake_user = True
    clean(act, reverse=(name == "Crawl"))
    names.append(name)
print("CLIPS", names)

# Every clip as its own take.
armature.animation_data_create()
armature.animation_data.action = None  # (an active action gets dropped from the takes)

# The texture, for a URP material in Unity.
for img in bpy.data.images:
    if img.size[0] > 0:
        img.filepath_raw = out_tex
        img.file_format = 'PNG'
        img.save()
        print("TEXTURE", img.name, img.size[:])
        break

bpy.ops.object.select_all(action='DESELECT')
armature.select_set(True); mesh.select_set(True)
bpy.context.view_layer.objects.active = armature
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, object_types={'ARMATURE', 'MESH'},
                         add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True,
                         bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                         apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                         path_mode='COPY', embed_textures=False, mesh_smooth_type='FACE')
print("EXPORTED", out_fbx)
