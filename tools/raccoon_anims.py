"""
Blender (headless): keyframe the raccoon's clips on our own rig and export one FBX for Unity.
  blender -b <rig.blend> --python tools/raccoon_anims.py -- <out.fbx> <out texture png>
Poses are built from rotations about WORLD axes (front = -Y, side = X, up = Z), so they read the same on any bone roll.
Idle's first frame is the neutral standing pose the game freezes under its procedural walk/sneak/gallop.
"""
import bpy, sys, math, mathutils
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index("--") + 1:]
out_fbx, out_tex = args[:2]
rig = bpy.data.objects["Armature"]
body = bpy.data.objects["RaccoonBody"]
pb = rig.pose.bones
ORDER = [b.name for b in rig.data.bones]  # parents come before children
X, Y, Z = (1, 0, 0), (0, 1, 0), (0, 0, 1)
FPS = 30
bpy.context.scene.render.fps = FPS

def rest():
    for b in pb:
        b.rotation_mode = 'QUATERNION'
        b.rotation_quaternion = (1, 0, 0, 0)
        b.location = (0, 0, 0)

def turn(n, axis, deg):
    if n not in pb or abs(deg) < 1e-3: return
    b = pb[n]
    bpy.context.view_layer.update()
    h = b.head.copy()
    R = Matrix.Rotation(math.radians(deg), 4, Vector(axis))
    b.matrix = Matrix.Translation(h) @ R @ Matrix.Translation(-h) @ b.matrix

def lift(dz):
    pb["Hips"].location = (0, 0, 0)
    bpy.context.view_layer.update()
    m = pb["Hips"].matrix.copy(); m.translation += Vector((0, 0, dz)); pb["Hips"].matrix = m

def base(arms_down=62):
    """Standing: arms relaxed at the sides (T-pose arms come down)."""
    turn("LeftArm", Y, arms_down); turn("RightArm", Y, -arms_down)

def key(frame):
    bpy.context.view_layer.update()
    for b in pb:
        b.keyframe_insert("rotation_quaternion", frame=frame)
        if b.name == "Hips": b.keyframe_insert("location", frame=frame)

def clip(name, frames, pose, loop=True):
    """pose(t) builds the pose for t in [0,1)."""
    act = bpy.data.actions.new(name); act.use_fake_user = True
    rig.animation_data_create(); rig.animation_data.action = act
    n = frames
    for f in range(n + 1):
        t = (f % n) / n if loop else min(1.0, f / n)
        rest(); pose(t); key(f)
    print("CLIP", name, n)

S = lambda t, k=1, ph=0: math.sin((t * k + ph) * math.tau)

def idle(t):
    base(62 + 3 * S(t))
    turn("Spine", X, 2 * S(t))
    for i in range(1, 5): turn(f"Tail{i}", Z, 10 * S(t, 1, i * 0.08))
    turn("Head", Z, 4 * S(t, 1, 0.25))

def jump(t):
    k = math.sin(min(1, t * 1.6) * math.pi * 0.5)
    base(62 - 95 * k)  # arms swing up and out
    turn("LeftUpLeg", X, -45 * k); turn("RightUpLeg", X, -45 * k)
    turn("LeftLeg", X, 70 * k); turn("RightLeg", X, 70 * k)
    for i in range(1, 5): turn(f"Tail{i}", X, -15 * k)

def hang(t):
    base(-80)  # both arms straight up
    turn("LeftArm", X, -10); turn("RightArm", X, -10)
    turn("LeftUpLeg", X, 8 * S(t)); turn("RightUpLeg", X, -8 * S(t))
    for i in range(1, 5): turn(f"Tail{i}", Z, 12 * S(t, 1, i * 0.1))

def push(t):
    base(10)
    turn("LeftArm", Z, -80); turn("RightArm", Z, 80)   # paws forward, flat against it
    turn("Spine", X, 18)
    turn("LeftUpLeg", X, 20 * S(t) - 10); turn("RightUpLeg", X, -20 * S(t) - 10)
    turn("LeftLeg", X, 25 * max(0, S(t, 1, 0.25))); turn("RightLeg", X, 25 * max(0, -S(t, 1, 0.25)))

def carry(t):
    base(-20)
    turn("LeftArm", Z, -60); turn("RightArm", Z, 60)   # paws up in front, holding the loot
    turn("LeftForeArm", Y, -30); turn("RightForeArm", Y, 30)
    turn("Spine", X, -4 + 2 * S(t, 2))

def cheer(t):
    k = abs(S(t, 2))
    base(-70 - 15 * k)
    lift(0.06 * k)
    turn("LeftForeArm", Y, -20 * S(t, 2)); turn("RightForeArm", Y, 20 * S(t, 2))
    for i in range(1, 5): turn(f"Tail{i}", Z, 20 * S(t, 2, i * 0.1))

def dance(t):
    base(30 + 40 * S(t, 2))
    turn("LeftArm", Z, -30 * max(0, S(t, 1))); turn("RightArm", Z, 30 * max(0, -S(t, 1)))
    turn("Hips", Y, 10 * S(t, 2))
    turn("Spine", Y, -12 * S(t, 2))
    turn("Head", Y, 8 * S(t, 2, 0.25))
    lift(0.03 * abs(S(t, 2)))
    turn("LeftUpLeg", X, -15 * max(0, S(t, 2))); turn("RightUpLeg", X, -15 * max(0, -S(t, 2)))
    for i in range(1, 5): turn(f"Tail{i}", Z, 25 * S(t, 2, i * 0.12))

def crawl(t):
    # belly low: the game pitches the body; here the limbs paddle in turn
    base(10)
    turn("LeftArm", Z, -70 + 25 * S(t)); turn("RightArm", Z, 70 + 25 * S(t))
    turn("LeftUpLeg", X, 30 * S(t) + 20); turn("RightUpLeg", X, -30 * S(t) + 20)
    turn("Head", X, -20)

clip("Idle", 60, idle)
clip("Jump", 18, jump, loop=False)
clip("Hang", 40, hang)
clip("Push", 30, push)
clip("Carry", 30, carry)
clip("Cheer", 30, cheer)
clip("Dance", 40, dance)
clip("Crawl", 24, crawl)
rig.animation_data.action = None
rest()

for img in bpy.data.images:
    if img.size[0] > 0:
        img.filepath_raw = out_tex; img.file_format = 'PNG'; img.save()
        print("TEXTURE", img.name, img.size[:]); break

bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True); body.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=True, object_types={'ARMATURE', 'MESH'},
                         add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True,
                         bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                         apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                         path_mode='COPY', embed_textures=False, mesh_smooth_type='FACE')
print("EXPORTED", out_fbx)

# A contact sheet of the clips for review.
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE'
scene.render.resolution_x = scene.render.resolution_y = 400
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scene.collection.objects.link(cam); scene.camera = cam
cam.location = Vector((1.9, -1.9, 0.9)); cam.rotation_euler = (Vector((0, 0, 0.45)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sun.data.energy = 3; sun.rotation_euler = (0.7, 0.2, 0.4); scene.collection.objects.link(sun)
w = bpy.data.worlds.new("w"); scene.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[1].default_value = 0.8
for name, frame in (("Idle", 0), ("Jump", 18), ("Hang", 10), ("Push", 8), ("Carry", 5), ("Cheer", 8), ("Dance", 10), ("Crawl", 6)):
    rig.animation_data.action = bpy.data.actions[name]
    scene.frame_set(frame)
    scene.render.filepath = f"{out_tex[:-4]}_pose_{name}.png"
    bpy.ops.render.render(write_still=True)
