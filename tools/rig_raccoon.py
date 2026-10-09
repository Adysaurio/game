"""
Blender (headless): rig the Tripo raccoon (T-pose, no skeleton) by hand.
  blender -b --python tools/rig_raccoon.py -- <in.glb> <out.blend> <renders prefix>
1. Face it toward -Y (Blender front), feet on the ground, ~1 m tall.
2. Measure the body (arms, shoulders, crotch, feet, neck, tail) from the vertices.
3. Build a skeleton with the bone names the game's gait code uses (Hips, Spine, neck, Head, Left/RightArm,
   ForeArm, Hand, Left/RightUpLeg, Leg, Foot, ToeBase) + Tail1..4.
4. Skin: heat weights on a watertight proxy, transferred to the real mesh (AI meshes aren't manifold).
5. Render a test pose so we can see the deformation before exporting.
"""
import bpy, bmesh, sys, math, mathutils
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
src, out_blend, renders = args[:3]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
# Unparent keeping the world transform (the glTF root carries the orientation), then drop the empties.
bpy.ops.object.select_all(action='DESELECT')
for m in meshes: m.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
for o in list(bpy.data.objects):
    if o.type != 'MESH': bpy.data.objects.remove(o)
bpy.ops.object.select_all(action='DESELECT')
for m in meshes: m.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1: bpy.ops.object.join()
body = bpy.context.view_layer.objects.active
body.name = "RaccoonBody"
body.parent = None
body.data.transform(body.matrix_world); body.matrix_world = mathutils.Matrix.Identity(4); body.data.update()

# 1) Orientation, measured (not assumed): the T-pose arms give the side axis → X; the tail is behind → +Y.
def extents():
    vv = [body.matrix_world @ v.co for v in body.data.vertices]
    return vv, max(v.x for v in vv) - min(v.x for v in vv), max(v.y for v in vv) - min(v.y for v in vv)
vv, ex, ey = extents()
print("BEFORE", ex, ey)
if ey > ex:
    body.data.transform(mathutils.Matrix.Rotation(math.radians(90), 4, 'Z')); body.data.update()
    vv, ex, ey = extents()
zmin = min(v.z for v in vv); zmax = max(v.z for v in vv); hh = zmax - zmin
low = [v for v in vv if v.z < zmin + hh * 0.35]
mid_y = sorted(v.y for v in vv if zmin + hh * 0.3 < v.z < zmin + hh * 0.55)
my = mid_y[len(mid_y) // 2]
if max(v.y for v in low) - my < my - min(v.y for v in low):  # the tail sticks out toward -Y: turn around
    body.data.transform(mathutils.Matrix.Rotation(math.radians(180), 4, 'Z')); body.data.update()
print("FACING fixed (front -Y, tail +Y)")
vs = [body.matrix_world @ v.co for v in body.data.vertices]
minz = min(v.z for v in vs); maxz = max(v.z for v in vs)
cx = (min(v.x for v in vs) + max(v.x for v in vs)) / 2
H = maxz - minz
s = 1.0 / H
body.data.transform(mathutils.Matrix.Diagonal((s, s, s, 1)) @ mathutils.Matrix.Translation((-cx, 0, -minz))); body.data.update()
vs = [v.co.copy() for v in body.data.vertices]
# center depth on the torso (not the tail): median y of the middle band
band = sorted(v.y for v in vs if 0.3 < v.z < 0.55 and abs(v.x) < 0.15)
cy = band[len(band) // 2]
for v in body.data.vertices: v.co.y -= cy
vs = [v.co.copy() for v in body.data.vertices]
sx_, sy_ = max(v.x for v in vs) - min(v.x for v in vs), max(v.y for v in vs) - min(v.y for v in vs)
print("SIZE", sx_, sy_, max(v.z for v in vs))
assert sx_ > sy_, "the arms should run along X after turning"

def slab(z0, z1, cond=lambda v: True): return [v for v in vs if z0 <= v.z <= z1 and cond(v)]

# 2) Landmarks.
span = max(v.x for v in vs)
arm_pts = [v for v in vs if v.x > span * 0.7]
arm_z = sorted(v.z for v in arm_pts)[len(arm_pts) // 2]
hand_x = span
# torso half-width just below the arm
torso = slab(arm_z - 0.16, arm_z - 0.09, lambda v: v.y < 0.25)
torso_w = max(abs(v.x) for v in torso) if torso else 0.2
shoulder_x = torso_w * 0.75
elbow_x = shoulder_x + (hand_x - shoulder_x) * 0.48
wrist_x = shoulder_x + (hand_x - shoulder_x) * 0.82
# feet: lowest band, right side
feet = slab(0, 0.06, lambda v: v.x > 0.02 and v.y < 0.2)
foot_x = sum(v.x for v in feet) / max(1, len(feet))
foot_y = sum(v.y for v in feet) / max(1, len(feet))
toe_y = min(v.y for v in feet) if feet else -0.1
# crotch: lowest height where the middle (between the legs, front half) is solid
crotch = 0.25
for i in range(60):
    z = i * 0.006
    if any(abs(v.x) < 0.02 and v.y < 0.05 for v in slab(z, z + 0.006)):
        crotch = z; break
hip_z = crotch + 0.05
knee_z = crotch * 0.5
neck_z = arm_z + 0.05
head_z = neck_z + 0.08
# tail: everything well behind the body (+Y)
back_y = max(v.y for v in slab(0.3, 0.55, lambda v: abs(v.x) < 0.2)) if slab(0.3, 0.55, lambda v: abs(v.x) < 0.2) else 0.15
tail = [v for v in vs if v.y > back_y * 0.6 and v.z < hip_z + 0.15]
print("LANDMARKS arm_z %.3f hand_x %.3f shoulder_x %.3f foot_x %.3f foot_y %.3f crotch %.3f neck %.3f back_y %.3f tailpts %d" %
      (arm_z, hand_x, shoulder_x, foot_x, foot_y, crotch, neck_z, back_y, len(tail)))

# 3) Skeleton.
arm_data = bpy.data.armatures.new("RaccoonRig")
rig = bpy.data.objects.new("Armature", arm_data)
bpy.context.scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
eb = arm_data.edit_bones
def bone(name, head, tail, parent=None, connect=False):
    b = eb.new(name); b.head = Vector(head); b.tail = Vector(tail)
    if parent: b.parent = eb[parent]; b.use_connect = connect
    return b
spine_y = 0.0
bone("Hips", (0, spine_y, hip_z), (0, spine_y, hip_z + 0.1))
bone("Spine", (0, spine_y, hip_z + 0.1), (0, spine_y, neck_z - 0.04), "Hips", True)
bone("neck", (0, spine_y, neck_z - 0.04), (0, spine_y - 0.01, head_z), "Spine", True)
bone("Head", (0, spine_y - 0.01, head_z), (0, spine_y - 0.02, 1.0), "neck", True)
for side, sx in (("Left", 1), ("Right", -1)):
    bone(side + "Shoulder", (sx * 0.04, spine_y, arm_z), (sx * shoulder_x, spine_y, arm_z), "Spine")
    bone(side + "Arm", (sx * shoulder_x, spine_y, arm_z), (sx * elbow_x, spine_y + 0.005, arm_z), side + "Shoulder", True)
    bone(side + "ForeArm", (sx * elbow_x, spine_y + 0.005, arm_z), (sx * wrist_x, spine_y, arm_z), side + "Arm", True)
    bone(side + "Hand", (sx * wrist_x, spine_y, arm_z), (sx * hand_x, spine_y, arm_z), side + "ForeArm", True)
    bone(side + "UpLeg", (sx * foot_x, foot_y * 0.3, hip_z), (sx * foot_x, foot_y * 0.3 - 0.01, knee_z), "Hips")
    bone(side + "Leg", (sx * foot_x, foot_y * 0.3 - 0.01, knee_z), (sx * foot_x, foot_y, 0.05), side + "UpLeg", True)
    bone(side + "Foot", (sx * foot_x, foot_y, 0.05), (sx * foot_x, (foot_y + toe_y) / 2, 0.02), side + "Leg", True)
    bone(side + "ToeBase", (sx * foot_x, (foot_y + toe_y) / 2, 0.02), (sx * foot_x, toe_y, 0.015), side + "Foot", True)
# tail chain from its root (closest tail point to the body) to its tip (farthest)
if tail:
    root = min(tail, key=lambda v: v.y)
    tip = max(tail, key=lambda v: (v - Vector((0, 0, hip_z))).length)
    root = Vector((root.x * 0.5, back_y * 0.85, max(root.z, hip_z - 0.08)))
    prev = "Hips"
    for i in range(4):
        a = root.lerp(tip, i / 4); b = root.lerp(tip, (i + 1) / 4)
        bone(f"Tail{i + 1}", a, b, prev, i > 0); prev = f"Tail{i + 1}"
    print("TAIL", tuple(round(c, 3) for c in root), tuple(round(c, 3) for c in tip))
bpy.ops.object.mode_set(mode='OBJECT')

# 4) Skin through a watertight proxy.
proxy = body.copy(); proxy.data = body.data.copy(); proxy.name = "Proxy"
bpy.context.scene.collection.objects.link(proxy)
rm = proxy.modifiers.new("remesh", 'REMESH'); rm.mode = 'VOXEL'; rm.voxel_size = 0.012
bpy.context.view_layer.objects.active = proxy
bpy.ops.object.modifier_apply(modifier="remesh")
bpy.ops.object.select_all(action='DESELECT')
proxy.select_set(True); rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
weighted = sum(1 for v in proxy.data.vertices if v.groups)
print("PROXY weighted", weighted, "/", len(proxy.data.vertices))
# copy groups to the real mesh
for vg in proxy.vertex_groups: body.vertex_groups.new(name=vg.name)
dt = body.modifiers.new("dt", 'DATA_TRANSFER'); dt.object = proxy
dt.use_vert_data = True; dt.data_types_verts = {'VGROUP_WEIGHTS'}; dt.vert_mapping = 'POLYINTERP_NEAREST'
dt.layers_vgroup_select_src = 'ALL'; dt.layers_vgroup_select_dst = 'NAME'
bpy.context.view_layer.objects.active = body
bpy.ops.object.modifier_apply(modifier="dt")
bpy.data.objects.remove(proxy)
body.parent = rig
am = body.modifiers.new("Armature", 'ARMATURE'); am.object = rig
print("BODY weighted", sum(1 for v in body.data.vertices if v.groups), "/", len(body.data.vertices))
bpy.ops.wm.save_as_mainfile(filepath=out_blend)

# 5) Test pose renders: arms down, a step, tail swung, head turned.
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='POSE')
pb = rig.pose.bones
def turn(n, axis, deg):
    """Rotate a pose bone about a WORLD axis through its head (rig sits at the origin)."""
    if n not in pb: return
    b = pb[n]
    bpy.context.view_layer.update()
    h = b.head.copy()
    R = mathutils.Matrix.Rotation(math.radians(deg), 4, Vector(axis))
    b.matrix = mathutils.Matrix.Translation(h) @ R @ mathutils.Matrix.Translation(-h) @ b.matrix
turn("LeftArm", (0, 1, 0), 65); turn("RightArm", (0, 1, 0), -65)   # arms down to the sides
turn("LeftUpLeg", (1, 0, 0), 30); turn("RightUpLeg", (1, 0, 0), -25)  # a step
turn("RightLeg", (1, 0, 0), 35)                                       # knee bend
turn("Tail1", (0, 0, 1), 25); turn("Tail2", (0, 0, 1), 20)
turn("Head", (0, 0, 1), 25)
bpy.ops.object.mode_set(mode='OBJECT')
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE'
scene.render.resolution_x = scene.render.resolution_y = 560
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scene.collection.objects.link(cam); scene.camera = cam
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sun.data.energy = 3; sun.rotation_euler = (0.7, 0.2, 0.4); scene.collection.objects.link(sun)
w = bpy.data.worlds.new("w"); scene.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[1].default_value = 0.8
def shot(name, az):
    cam.location = Vector((math.sin(az) * 2.6, -math.cos(az) * 2.6, 0.75))
    cam.rotation_euler = (Vector((0, 0, 0.5)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = f"{renders}_{name}.png"; bpy.ops.render.render(write_still=True)
shot("front", 0); shot("side", math.pi / 2); shot("back", math.pi * 0.8)
