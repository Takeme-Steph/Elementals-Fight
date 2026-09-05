bl_info = {
    "name": "Loc Generator Tools",
    "author": "Elementals Fight",
    "version": (2, 3, 0),
    "blender": (5, 2, 0),
    "location": "3D View > Sidebar (N) > Locs  |  Properties > Modifiers",
    "description": "Reset / store / VERIFY Geometry Nodes modifier inputs, bake the tip-curl "
                   "alpha mask, sync newly added sockets, audit the evaluated output for "
                   "null material slots, validate a combed-guide Curves object against the "
                   "depsgraph rules that silently empty it, report mobile fragment cost, and "
                   "check any drivers bridging modifier sockets to shader values.",
    "category": "Object",
}

# ---------------------------------------------------------------------------
# WHY THIS FILE IS SHAPED THE WAY IT IS
#
# Rule 116/134: writing to a Geometry Nodes modifier - and ESPECIALLY editing
#   ng.interface - can reshuffle the stored values of unrelated sockets.
# Rule 133: a verification that reads through the same path as the thing it
#   verifies is worthless. So the snapshot below is keyed by socket NAME and
#   stored on the object, independent of the Socket_NN identifiers.
# Rule 147: Blender 5.x moved modifier input values off IDProperties onto
#   modifier.properties.inputs[ident]["value"].
# Rule 148: drivers bridge modifier sockets to shader node values and target
#   socket IDENTIFIERS, so any interface edit can silently orphan them.
#
# ADDED 2.1.0 (2026-08-21):
# Rule 177: adding a socket to ng.interface does NOT push its default onto an
#   EXISTING modifier - it reads 0.0 until written. A zero-valued new socket
#   looks like a failed build, not an unset value. Hence "Sync New Sockets".
# Rule 176: Join Geometry on two slotless meshes creates a NULL material slot,
#   and Set Material APPENDS rather than replaces. Nothing warns, and it breaks
#   the Unity "every submesh bound, 0 nulls" check. Hence "Audit Output".
# Rule 179: verify against an EXTERNAL reference, never against geometry
#   derived from the thing being tested. Exactly 0.0 is a smell, not a pass.
# Rule 182: hidden is NOT excluded from FBX export - use_visible defaults off,
#   and "visible" means the eye icon, not the render icon.
#
# NOTE ON DRIVERS: as of 2026-08-21 this generator has ZERO drivers by design.
#   All 7 were retired when Base Color was rewired to the baked texture
#   HairBaseColor_v6. "No drivers read this modifier" is now the CORRECT
#   result, not a failure.
#
# ADDED 2.3.0 (2026-08-23):
# FRIZZ IS GONE. The frizz card layer and its "Frizz" / "Frizz Density"
#   sockets were deleted from this generator (and from the hair experiment
#   graph) on 2026-08-23 - hair accessories will go on flat cards instead and
#   both together read as too busy. Removing it took measured overdraw from
#   ~14x to ~8.6x, a bigger win than any other change this session.
#
# COMBED GUIDES. The generator can now be driven by a hand-combed Curves
#   object instead of its own procedural guides: "Use Combed Curves" +
#   "Combed Curves" (object picker), injected at Store Root Arclen via
#   CB_Info -> CB_Resample -> CB_Switch. Hence "Check Combed Guides".
# Rule 203: a guide Curves object must have NO parent, must NOT use the
#   emitter as its data.surface, and must stay visible in viewport AND
#   render. Each of those four independently makes Object Info return EMPTY
#   geometry with no error anywhere - the locs just vanish and you are left
#   looking at a bare scalp. Because the causes STACK, fixing one at a time
#   looks like failure. Worse, the dependency is resolved when the depsgraph
#   is BUILT (i.e. at file load), so binding surface to the emitter seems
#   harmless until the next load, and clearing it afterwards restores
#   nothing. Bind surface to the BODY, never to the emitter.
# Rule 202 (corrected): an Object socket on the group input DOES register a
#   dependency. It was recorded as broken; that test was confounded by the
#   parent and surface above. Panel object pickers are fine.
#
# MOBILE COST. Triangle count is the wrong metric for this asset. Measured
#   2026-08-23: the cheaper-looking mesh had 36% fewer tris yet shaded 4%
#   MORE fragments. What costs on a tile GPU is fragments and overdraw, and
#   alpha-tested fragments most of all because they defeat early-Z. Hence
#   "Mobile Cost", which also reports the back-facing share - with
#   use_backface_culling off, closed loc tubes shade their far walls for
#   nothing, measured at 51% of all fragments.
# ---------------------------------------------------------------------------

import bpy
import json
import os
import shutil
import datetime

SNAPSHOT_KEY = "locgen_panel_snapshot"

# Never reset: Geometry is wired, not a value; ID pointers are per-character
# assignments and a reset must not blank them or impose one character's
# material on another.
SKIP_TYPES = {
    'NodeSocketGeometry', 'NodeSocketMaterial', 'NodeSocketObject',
    'NodeSocketCollection', 'NodeSocketImage', 'NodeSocketTexture',
}


def _input_items(node_group):
    """Resettable input sockets of a node group interface, in panel order."""
    if node_group is None:
        return
    for item in node_group.interface.items_tree:
        if getattr(item, "item_type", "") != "SOCKET":
            continue
        if getattr(item, "in_out", "") != "INPUT":
            continue
        if item.socket_type in SKIP_TYPES:
            continue
        if not hasattr(item, "default_value"):
            continue
        yield item


def _read(modifier, identifier):
    """Read one modifier input value. Blender 5.x path first (rule 147)."""
    try:
        group = modifier.properties.inputs[identifier]
        if "value" in group.keys():
            return group["value"]
    except Exception:
        pass
    try:
        return modifier[identifier]          # pre-5.x IDProperty path
    except Exception:
        return None


def _write(modifier, identifier, value):
    try:
        modifier.properties.inputs[identifier]["value"] = value
        return True
    except Exception:
        pass
    try:
        modifier[identifier] = value
        return True
    except Exception:
        return False


def _plain(value):
    """Comparable / JSON-safe form. Colours and vectors arrive as arrays."""
    if value is None:
        return None
    if hasattr(value, "name"):                       # ID pointer
        return value.name
    if hasattr(value, "__len__") and not isinstance(value, str):
        return [round(float(x), 6) for x in value]
    if isinstance(value, bool):
        return value
    if isinstance(value, float):
        return round(value, 6)
    return value


def _same(a, b):
    a, b = _plain(a), _plain(b)
    if isinstance(a, list) and isinstance(b, list):
        return len(a) == len(b) and all(abs(x - y) < 1e-5 for x, y in zip(a, b))
    if isinstance(a, float) and isinstance(b, float):
        return abs(a - b) < 1e-5
    return a == b


def _target_modifier(context):
    obj = context.object
    if obj is None:
        return None
    active = obj.modifiers.active
    if active is not None and active.type == 'NODES' and active.node_group:
        return active
    for mod in obj.modifiers:
        if mod.type == 'NODES' and mod.node_group:
            return mod
    return None


def _capture(modifier):
    """Whole panel, keyed by NAME so it survives identifier reshuffles."""
    return {item.name: _plain(_read(modifier, item.identifier))
            for item in _input_items(modifier.node_group)}


def _restore(modifier, snapshot):
    """Write the whole panel back by name. Returns names that failed."""
    failed = []
    for item in _input_items(modifier.node_group):
        if item.name not in snapshot:
            continue
        if not _write(modifier, item.identifier, snapshot[item.name]):
            failed.append(item.name)
    return failed


# ---------------------------------------------------------------------------
# Operators
# ---------------------------------------------------------------------------

class LOCGEN_OT_snapshot(bpy.types.Operator):
    """Record the whole panel as the reference to verify against later"""
    bl_idname = "locgen.snapshot"
    bl_label = "Snapshot Panel"
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        return _target_modifier(context) is not None

    def execute(self, context):
        mod = _target_modifier(context)
        snap = _capture(mod)
        context.object[SNAPSHOT_KEY] = json.dumps(snap)
        self.report({'INFO'}, "Snapshot: %d socket(s) recorded" % len(snap))
        return {'FINISHED'}


class LOCGEN_OT_verify(bpy.types.Operator):
    """Compare every socket against the stored snapshot (rules 133/134)"""
    bl_idname = "locgen.verify"
    bl_label = "Verify Against Snapshot"

    @classmethod
    def poll(cls, context):
        return (_target_modifier(context) is not None
                and context.object is not None
                and SNAPSHOT_KEY in context.object)

    def execute(self, context):
        mod = _target_modifier(context)
        snap = json.loads(context.object[SNAPSHOT_KEY])
        now = _capture(mod)
        drift = [n for n in snap if n in now and not _same(snap[n], now[n])]
        gone = [n for n in snap if n not in now]
        added = [n for n in now if n not in snap]
        if drift or gone or added:
            msg = []
            if drift:
                msg.append("CHANGED: " + ", ".join(drift))
            if gone:
                msg.append("MISSING: " + ", ".join(gone))
            if added:
                msg.append("NEW: " + ", ".join(added))
            self.report({'WARNING'}, " | ".join(msg))
        else:
            self.report({'INFO'}, "Clean - all %d socket(s) match" % len(snap))
        return {'FINISHED'}


class LOCGEN_OT_reset_defaults(bpy.types.Operator):
    """Reset every value on this modifier to the node group's defaults"""
    bl_idname = "locgen.reset_defaults"
    bl_label = "Reset to Defaults"
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        return _target_modifier(context) is not None

    def invoke(self, context, event):
        return context.window_manager.invoke_confirm(self, event)

    def execute(self, context):
        mod = _target_modifier(context)
        changed, failed = 0, []
        for item in _input_items(mod.node_group):
            if _write(mod, item.identifier, item.default_value):
                changed += 1
            else:
                failed.append(item.name)
        context.object.update_tag()
        if failed:
            self.report({'WARNING'}, "Reset %d; could not set: %s"
                        % (changed, ", ".join(failed)))
        else:
            self.report({'INFO'}, "Reset %d value(s) on '%s'" % (changed, mod.name))
        return {'FINISHED'}


class LOCGEN_OT_store_defaults(bpy.types.Operator):
    """Store current values as the node group's defaults, safely

    Writing item.default_value IS an interface edit, which can reshuffle the
    modifier's stored values mid-loop (rule 116/134). The old version of this
    tool read and wrote in one interleaved pass, so later reads could come
    from already-reshuffled identifiers. This one captures the whole panel
    first, then edits, then writes the capture back and verifies.
    """
    bl_idname = "locgen.store_defaults"
    bl_label = "Store Current as Defaults"
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        return _target_modifier(context) is not None

    def invoke(self, context, event):
        return context.window_manager.invoke_confirm(self, event)

    def execute(self, context):
        mod = _target_modifier(context)

        snapshot = _capture(mod)                       # 1. capture, by name

        stored = 0                                     # 2. edit the interface
        for item in _input_items(mod.node_group):
            value = snapshot.get(item.name)
            if value is None:
                continue
            try:
                item.default_value = value
                stored += 1
            except Exception:
                pass

        failed = _restore(mod, snapshot)               # 3. write it all back

        after = _capture(mod)                          # 4. verify
        drift = [n for n in snapshot
                 if n in after and not _same(snapshot[n], after[n])]

        context.object[SNAPSHOT_KEY] = json.dumps(snapshot)
        context.object.update_tag()

        if drift or failed:
            self.report({'WARNING'},
                        "Stored %d default(s) but panel did not survive - "
                        "drift: %s / failed: %s"
                        % (stored, ", ".join(drift) or "none",
                           ", ".join(failed) or "none"))
        else:
            self.report({'INFO'},
                        "Stored %d default(s); panel verified intact" % stored)
        return {'FINISHED'}


def _modifier_drivers(obj, mod):
    """Every driver anywhere that reads a socket on this modifier (rule 148)."""
    needle = 'modifiers["%s"]' % mod.name
    found = []
    trees = list(bpy.data.node_groups) + [m.node_tree for m in bpy.data.materials
                                          if m.node_tree]
    for tree in trees:
        ad = getattr(tree, "animation_data", None)
        if not ad:
            continue
        for fc in ad.drivers:
            for var in fc.driver.variables:
                for tgt in var.targets:
                    path = tgt.data_path or ""
                    if needle not in path:
                        continue
                    src = tgt.id if tgt.id else obj
                    try:
                        src.path_resolve(path)
                        ok = True
                    except Exception:
                        ok = False
                    found.append((tree.name, path, ok))
    return found


class LOCGEN_OT_check_drivers(bpy.types.Operator):
    """Re-resolve every driver that reads this modifier's sockets"""
    bl_idname = "locgen.check_drivers"
    bl_label = "Check Driver Bridge"

    @classmethod
    def poll(cls, context):
        return _target_modifier(context) is not None

    def execute(self, context):
        mod = _target_modifier(context)
        found = _modifier_drivers(context.object, mod)
        if not found:
            self.report({'INFO'},
                        "No drivers read this modifier - EXPECTED since 2026-08-21, "
                        "all 7 were retired when Base Color moved to the baked texture")
            return {'FINISHED'}
        broken = [f for f in found if not f[2]]
        if broken:
            self.report({'WARNING'}, "%d of %d driver(s) BROKEN: %s"
                        % (len(broken), len(found),
                           "; ".join("%s -> %s" % (b[0], b[1]) for b in broken)))
        else:
            self.report({'INFO'}, "All %d driver(s) resolve" % len(found))
        return {'FINISHED'}


# ---------------------------------------------------------------------------
# UI
# ---------------------------------------------------------------------------

def _draw_locgen(layout, context):
    mod = _target_modifier(context)
    box = layout.box()
    box.label(text="Tip Cards", icon='TEXTURE')
    box.operator("locgen.bake_tipcurl", icon='SHADERFX')

    if mod is None:
        layout.label(text="No Geometry Nodes modifier", icon='INFO')
        return

    col = layout.column(align=True)
    col.scale_y = 1.2
    col.operator("locgen.snapshot", icon='FILE_TICK')
    col.operator("locgen.verify", icon='CHECKMARK')
    col.separator()
    col.operator("locgen.audit_output", icon='EXPORT')
    col.operator("locgen.mobile_cost", icon='MOD_PARTICLES')
    col.operator("locgen.check_guides", icon='CURVE_DATA')
    col.operator("locgen.sync_new_sockets", icon='FILE_REFRESH')
    col.separator()
    col.operator("locgen.check_drivers", icon='DRIVER')
    col.separator()
    col.operator("locgen.reset_defaults", icon='LOOP_BACK')
    col.operator("locgen.store_defaults", icon='PINNED')

    obj = context.object
    if obj is not None and SNAPSHOT_KEY in obj:
        try:
            snap = json.loads(obj[SNAPSHOT_KEY])
            now = _capture(mod)
            drift = [n for n in snap if n in now and not _same(snap[n], now[n])]
        except Exception:
            snap, drift = {}, []
        box = layout.box()
        if drift:
            box.label(text="Changed since snapshot:", icon='ERROR')
            for name in drift:
                box.label(text="    " + name)
        else:
            box.label(text="Matches snapshot (%d)" % len(snap), icon='CHECKMARK')
    else:
        layout.label(text="No snapshot recorded", icon='DOT')

    modified = []
    for item in _input_items(mod.node_group):
        current = _read(mod, item.identifier)
        if current is None:
            continue
        if not _same(current, item.default_value):
            modified.append(item.name)
    box = layout.box()
    if modified:
        box.label(text="Differs from node-group default:", icon='DOT')
        for name in modified:
            box.label(text="    " + name)
    else:
        box.label(text="All values at default", icon='CHECKMARK')



class LOCGEN_OT_sync_new_sockets(bpy.types.Operator):
    """Push node-group defaults onto sockets the modifier never received (rule 177)

    Adding a socket to ng.interface does not write its default onto an existing
    modifier - it reads 0.0 (or the type's zero) until something writes it. That
    is indistinguishable from a deliberate zero, and a zero-valued new socket
    usually looks like the feature is broken rather than unset.
    """
    bl_idname = "locgen.sync_new_sockets"
    bl_label = "Sync New Sockets"
    bl_options = {'REGISTER', 'UNDO'}

    @classmethod
    def poll(cls, context):
        return _target_modifier(context) is not None

    def execute(self, context):
        mod = _target_modifier(context)
        synced = []
        for item in _input_items(mod.node_group):
            current = _read(mod, item.identifier)
            default = item.default_value
            if current is None:
                continue
            # only act where the modifier holds a zero and the group default is not
            if _same(current, default):
                continue
            zeroish = _plain(current)
            base = _plain(default)
            is_zero = (zeroish == 0 or zeroish is False
                       or (isinstance(zeroish, list) and not any(zeroish)))
            if is_zero and not (base == 0 or base is False
                                or (isinstance(base, list) and not any(base))):
                if _write(mod, item.identifier, default):
                    synced.append(item.name)
        if synced:
            self.report({'WARNING'},
                        "Synced %d unset socket(s): %s" % (len(synced), ", ".join(synced)))
        else:
            self.report({'INFO'}, "No unset sockets - every socket holds a value")
        return {'FINISHED'}


class LOCGEN_OT_audit_output(bpy.types.Operator):
    """Evaluate the modifier and audit what it will actually export (rule 176)

    Reports triangle count, NULL material slots, UV layers and the stored
    attribute surface. Null slots come from Join Geometry on slotless meshes and
    from mesh primitives, are silent in Blender, and break the Unity import
    check that every submesh binds to a real material.
    """
    bl_idname = "locgen.audit_output"
    bl_label = "Audit Output"

    @classmethod
    def poll(cls, context):
        return _target_modifier(context) is not None

    def execute(self, context):
        obj = context.object
        deps = context.evaluated_depsgraph_get()
        evaluated = obj.evaluated_get(deps)
        mesh = evaluated.to_mesh()
        try:
            mesh.calc_loop_triangles()
            tris = len(mesh.loop_triangles)
            mats = [m.name if m else None for m in mesh.materials]
            nulls = sum(1 for m in mats if m is None)
            uvs = [layer.name for layer in mesh.uv_layers]
            attrs = sorted(a.name for a in mesh.attributes
                           if not a.name.startswith("."))
        finally:
            evaluated.to_mesh_clear()

        print("[locgen] tris=%d  materials=%s  nulls=%d" % (tris, mats, nulls))
        print("[locgen] uv layers=%s" % uvs)
        print("[locgen] attributes=%s" % attrs)
        if len(uvs) > 1:
            print("[locgen] NOTE %d UV layers - strip the generator's input UV "
                  "before export, Unity only needs UV0" % len(uvs))

        if nulls:
            self.report({'WARNING'},
                        "%d NULL material slot(s) - will break the Unity import "
                        "check. %d tris. See console." % (nulls, tris))
        else:
            self.report({'INFO'},
                        "%d tris, %d material(s), 0 nulls, %d UV layer(s). See console."
                        % (tris, len(mats), len(uvs)))
        return {'FINISHED'}


class LOCGEN_OT_check_guides(bpy.types.Operator):
    """Validate the combed-guide Curves object against the rules that silently empty it

    Rule 203: a guide Curves object must have NO parent, must NOT use this
    emitter as its data.surface, and must stay visible in viewport AND render.
    Each of those independently makes Object Info return empty geometry, with no
    error anywhere - the locs just disappear. The causes STACK, so fixing one at
    a time looks like failure. And the dependency is resolved when the depsgraph
    is BUILT, so a bad surface binding only bites after the next file load and
    clearing it afterwards restores nothing.

    This also checks the far more common cause first: the "Use Combed Curves"
    toggle being off, which shows the procedural guides and looks like a
    different bug entirely.
    """
    bl_idname = "locgen.check_guides"
    bl_label = "Check Combed Guides"

    @classmethod
    def poll(cls, context):
        return _target_modifier(context) is not None

    def execute(self, context):
        mod = _target_modifier(context)
        obj = context.object
        ng = mod.node_group
        problems, notes = [], []

        toggle = None
        for item in _input_items(ng):
            if item.name == "Use Combed Curves":
                toggle = _read(mod, item.identifier)
        if toggle is None:
            self.report({'INFO'}, "No 'Use Combed Curves' socket - procedural generator, nothing to check.")
            return {'FINISHED'}
        if not toggle:
            self.report({'WARNING'},
                        "'Use Combed Curves' is OFF - you are seeing the procedural guides, "
                        "not your combed ones. Check this before assuming anything is broken.")
            return {'FINISHED'}

        # the guide object: either straight off an Object Info node, or via the panel socket
        guide = None
        for node in ng.nodes:
            if node.bl_idname != 'GeometryNodeObjectInfo':
                continue
            sock = node.inputs.get('Object')
            if sock is None:
                continue
            if sock.is_linked:
                # NOTE: cannot use _input_items here - it skips NodeSocketObject
                # (SKIP_TYPES) so that resets never blank an ID pointer.
                for item in ng.interface.items_tree:
                    if getattr(item, "item_type", "") != "SOCKET":
                        continue
                    if getattr(item, "in_out", "") != "INPUT":
                        continue
                    if item.socket_type == 'NodeSocketObject':
                        candidate = _read(mod, item.identifier)
                        if candidate is not None:
                            guide = candidate
                            break
            elif sock.default_value is not None:
                guide = sock.default_value
            if guide is not None:
                break
        if guide is None:
            self.report({'ERROR'}, "'Use Combed Curves' is ON but no guide object is set.")
            return {'CANCELLED'}

        notes.append("guide = %s (%s)" % (guide.name, guide.type))
        if guide.parent is not None:
            problems.append("has a parent (%s) - clear it, keep matrix_world" % guide.parent.name)
        surf = getattr(guide.data, "surface", None)
        if surf is not None and surf == obj:
            problems.append("data.surface is the emitter (%s) - bind it to the BODY instead" % obj.name)
        elif surf is not None:
            notes.append("surface = %s (safe, not the emitter)" % surf.name)
        if guide.hide_viewport:
            problems.append("hide_viewport is on (monitor icon)")
        if guide.hide_get():
            problems.append("hidden in viewport (eye icon)")
        if guide.hide_render:
            problems.append("hide_render is on - breaks the RENDER depsgraph specifically")
        for flag in ("visible_camera", "visible_shadow", "visible_diffuse", "visible_glossy"):
            if hasattr(guide, flag) and not getattr(guide, flag):
                problems.append("%s is off - also removes it from the evaluated set" % flag)

        # does anything actually come through? compare against the emitter's own base mesh
        base = 0
        if obj.type == 'MESH':
            base = sum(len(poly.vertices) - 2 for poly in obj.data.polygons)
        deps = context.evaluated_depsgraph_get()
        evaluated = obj.evaluated_get(deps)
        mesh = evaluated.to_mesh()
        try:
            mesh.calc_loop_triangles()
            tris = len(mesh.loop_triangles)
        finally:
            evaluated.to_mesh_clear()
        notes.append("evaluated %d tris (emitter base mesh is %d)" % (tris, base))
        if tris <= base + 16:
            problems.append("output is only the bare emitter - the guides are NOT reaching the "
                            "generator. Fix the above, then cycle the guide object off and on to "
                            "force a relations rebuild, and re-save.")

        for line in notes:
            print("[locgen] guides: " + line)
        for line in problems:
            print("[locgen] GUIDE PROBLEM: " + line)
        if problems:
            self.report({'WARNING'}, "%d guide problem(s) - see console. %s"
                        % (len(problems), problems[0]))
        else:
            self.report({'INFO'}, "Combed guides healthy: %s, %d tris." % (guide.name, tris))
        return {'FINISHED'}


class LOCGEN_OT_mobile_cost(bpy.types.Operator):
    """Report what this asset actually costs a mobile tile GPU

    Triangle count is the wrong metric here - measured 2026-08-23, the mesh with
    36% fewer tris shaded 4% MORE fragments. What costs is fragments, overdraw,
    and alpha-tested area (which defeats early-Z and so cannot be discarded
    cheaply). Cards are identified by the project convention u < -0.1 on UV0.

    Back-facing share matters because the locs are closed tubes: with
    use_backface_culling off, every one shades its far wall for nothing.
    """
    bl_idname = "locgen.mobile_cost"
    bl_label = "Mobile Cost"

    @classmethod
    def poll(cls, context):
        return _target_modifier(context) is not None

    def execute(self, context):
        import numpy as np
        from bpy_extras.object_utils import world_to_camera_view
        from mathutils import Vector

        obj = context.object
        scene = context.scene
        deps = context.evaluated_depsgraph_get()
        evaluated = obj.evaluated_get(deps)
        mesh = evaluated.to_mesh()
        try:
            matrix = evaluated.matrix_world
            count = len(mesh.vertices)
            coords = np.empty(count * 3, dtype=np.float32)
            mesh.vertices.foreach_get("co", coords)
            coords = coords.reshape(-1, 3)
            world = np.array([matrix @ Vector(c) for c in coords])

            uv = None
            if mesh.uv_layers:
                layer = mesh.uv_layers[0]
                raw = np.empty(len(layer.data) * 2, dtype=np.float32)
                layer.data.foreach_get("uv", raw)
                uv = raw.reshape(-1, 2)

            cam = scene.camera
            res_x = scene.render.resolution_x
            res_y = scene.render.resolution_y
            projected = None
            if cam is not None:
                projected = np.zeros((count, 3))
                for i, point in enumerate(world):
                    v = world_to_camera_view(scene, cam, Vector(point))
                    projected[i] = (v.x * res_x, v.y * res_y, v.z)
                cam_pos = np.array(cam.matrix_world.translation)

            # Materials must come from the EVALUATED mesh: on a geometry-nodes
            # asset the real material is assigned by Set Material inside the
            # graph, not by an object material slot, so reading obj.material_slots
            # audits the wrong materials entirely.
            eval_mats = [mat for mat in mesh.materials if mat]
            tris = card_tris = 0
            card_px = tube_px = front_px = back_px = 0.0
            for poly in mesh.polygons:
                n = len(poly.vertices) - 2
                tris += n
                is_card = bool(uv is not None and
                               any(uv[li][0] < -0.1 for li in poly.loop_indices))
                if is_card:
                    card_tris += n
                if projected is None:
                    continue
                pts = projected[list(poly.vertices)]
                if not (pts[:, 2] > 0).all():
                    continue
                x, y = pts[:, 0], pts[:, 1]
                area = 0.5 * abs(np.dot(x, np.roll(y, -1)) - np.dot(y, np.roll(x, -1)))
                if is_card:
                    card_px += area
                else:
                    tube_px += area
                normal = np.array(matrix.to_3x3() @ poly.normal)
                centre = world[list(poly.vertices)].mean(axis=0)
                if np.dot(normal, cam_pos - centre) > 0:
                    front_px += area
                else:
                    back_px += area
        finally:
            evaluated.to_mesh_clear()

        print("[locgen] verts=%d tris=%d (cards %d / tubes %d)"
              % (count, tris, card_tris, tris - card_tris))
        print("[locgen] evaluated materials (= submeshes on export): %s"
              % [m.name for m in eval_mats])
        if projected is None:
            self.report({'WARNING'}, "%d tris. No scene camera - fragment cost not measured." % tris)
            return {'FINISHED'}

        total = card_px + tube_px
        alpha_share = 100.0 * card_px / max(total, 1.0)
        back_share = 100.0 * back_px / max(front_px + back_px, 1.0)
        print("[locgen] fragments=%.2fM  alpha-tested=%.1f%%  back-facing=%.1f%%"
              % (total / 1e6, alpha_share, back_share))
        print("[locgen] NOTE overdraw needs the silhouette area too - render this object alone "
              "with film_transparent and divide fragments by the covered pixel count.")

        culled = [m.name for m in eval_mats if not m.use_backface_culling]
        if culled:
            print("[locgen] backface culling OFF on: %s" % culled)
            self.report({'WARNING'},
                        "%d tris, %.2fM fragments, %.1f%% alpha-tested, %.1f%% back-facing - "
                        "culling is OFF, so that back-facing share is wasted. See console."
                        % (tris, total / 1e6, alpha_share, back_share))
        else:
            self.report({'INFO'},
                        "%d tris, %.2fM fragments, %.1f%% alpha-tested. See console."
                        % (tris, total / 1e6, alpha_share))
        return {'FINISHED'}


class LOCGEN_PT_modifier_panel(bpy.types.Panel):
    bl_label = "Loc Generator Defaults"
    bl_idname = "LOCGEN_PT_modifier_panel"
    bl_space_type = 'PROPERTIES'
    bl_region_type = 'WINDOW'
    bl_context = "modifier"
    bl_options = {'DEFAULT_CLOSED'}

    @classmethod
    def poll(cls, context):
        return _target_modifier(context) is not None

    def draw(self, context):
        mod = _target_modifier(context)
        box = self.layout.box()
        box.label(text=mod.node_group.name, icon='GEOMETRY_NODES')
        _draw_locgen(self.layout, context)


class LOCGEN_PT_panel(bpy.types.Panel):
    bl_label = "Loc Generator"
    bl_idname = "LOCGEN_PT_panel"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "Locs"

    def draw(self, context):
        mod = _target_modifier(context)
        if mod is not None:
            box = self.layout.box()
            box.label(text=mod.node_group.name, icon='GEOMETRY_NODES')
            box.label(text="on: %s" % context.object.name)
        _draw_locgen(self.layout, context)


class LOCGEN_OT_bake_tipcurl(bpy.types.Operator):
    """Regenerate the tip-card curl alpha mask procedurally

    The mask is one strand spiralling up the card, written to RGB (the shader
    reads the Color output, not Alpha) in Non-Color space:

        centre u(v) = 0.5 + amp(v) * sin(2*pi*TURNS*v)
        width  w(v) = W_TIP + (W_BASE - W_TIP) * (1-v)**TAPER_POW
        amp(v)      = AMP_BASE + (AMP_TIP - AMP_BASE) * v

    v = 0 is the card BASE (where it meets the tube), v = 1 the free tip.

    Two things worth knowing before you turn TURNS up. Coverage is set mostly by
    the taper, not the turn count - softening TAPER_POW or raising W_TIP thickens
    the strand and raises overdraw. And a tight coil is high-frequency detail in
    v, so it averages away in the mip chain: at fight-camera distance the card
    resolves to a flat rectangle whose opacity is the reported coverage.
    """
    bl_idname = "locgen.bake_tipcurl"
    bl_label = "Bake Tip Curl Alpha"
    bl_options = {'REGISTER'}

    image_name: bpy.props.StringProperty(
        name="Image", default="Yemoja_Hair_TipCurl_Alpha.png",
        description="Existing image datablock to overwrite")
    turns: bpy.props.FloatProperty(name="Turns", default=4.0, min=0.25, max=16.0,
        description="Full spiral revolutions over the card length")
    amp_base: bpy.props.FloatProperty(name="Amp Base", default=0.28, min=0.0, max=0.5,
        description="Coil radius at the base, in u")
    amp_tip: bpy.props.FloatProperty(name="Amp Tip", default=0.13, min=0.0, max=0.5,
        description="Coil radius at the tip. Below Amp Base reads as a corkscrew")
    w_base: bpy.props.FloatProperty(name="Width Base", default=0.80, min=0.0, max=1.0)
    w_tip: bpy.props.FloatProperty(name="Width Tip", default=0.09, min=0.0, max=1.0,
        description="Too low and the tip reads as a spike rather than hair")
    taper_pow: bpy.props.FloatProperty(name="Taper Power", default=1.7, min=0.2, max=6.0,
        description="Higher collapses the strand faster. Main driver of coverage")
    soft: bpy.props.FloatProperty(name="Edge Soften", default=0.035, min=0.001, max=0.2)
    res_x: bpy.props.IntProperty(name="Width px", default=256, min=8, max=4096)
    res_y: bpy.props.IntProperty(name="Height px", default=512, min=8, max=4096)
    backup: bpy.props.BoolProperty(name="Back up existing file", default=True)

    def invoke(self, context, event):
        return context.window_manager.invoke_props_dialog(self, width=340)

    def execute(self, context):
        import numpy as np
        img = bpy.data.images.get(self.image_name)
        if img is None:
            self.report({'ERROR'}, "No image datablock named %r" % self.image_name)
            return {'CANCELLED'}

        w, h = int(self.res_x), int(self.res_y)
        u = (np.arange(w) + 0.5) / w
        v = (np.arange(h) + 0.5) / h
        U = u[None, :]
        V = v[:, None]
        amp = self.amp_base + (self.amp_tip - self.amp_base) * V
        centre = 0.5 + amp * np.sin(2.0 * np.pi * self.turns * V)
        half = 0.5 * (self.w_tip + (self.w_base - self.w_tip) * (1.0 - V) ** self.taper_pow)
        t = np.clip((np.abs(U - centre) - (half - self.soft)) / (2.0 * self.soft), 0.0, 1.0)
        mask = (1.0 - (t * t * (3.0 - 2.0 * t))).astype(np.float32)

        path = bpy.path.abspath(img.filepath) if img.filepath else ""
        if self.backup and path and os.path.isfile(path):
            stamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
            bak = "%s_PRE_%s%s" % (os.path.splitext(path)[0], stamp, os.path.splitext(path)[1])
            shutil.copy2(path, bak)

        if list(img.size) != [w, h]:
            img.scale(w, h)
        px = np.zeros((h, w, 4), dtype=np.float32)
        px[:, :, 0] = mask
        px[:, :, 1] = mask
        px[:, :, 2] = mask
        px[:, :, 3] = 1.0
        img.pixels = px.ravel().tolist()
        img.colorspace_settings.name = 'Non-Color'
        if path:
            img.file_format = 'PNG'
            img.save()
            img.reload()

        cov = float(mask.mean())
        self.report({'INFO'},
                    "Curl baked: %dx%d, %.2f turns, coverage %.3f "
                    "(mips to a %.0f%% opaque card at distance)"
                    % (w, h, self.turns, cov, cov * 100.0))
        return {'FINISHED'}


CLASSES = (LOCGEN_OT_snapshot, LOCGEN_OT_verify, LOCGEN_OT_check_drivers,
           LOCGEN_OT_bake_tipcurl,
           LOCGEN_OT_sync_new_sockets, LOCGEN_OT_audit_output,
           LOCGEN_OT_check_guides, LOCGEN_OT_mobile_cost,
           LOCGEN_OT_reset_defaults, LOCGEN_OT_store_defaults,
           LOCGEN_PT_modifier_panel, LOCGEN_PT_panel)


def register():
    for cls in CLASSES:
        bpy.utils.register_class(cls)


def unregister():
    for cls in reversed(CLASSES):
        bpy.utils.unregister_class(cls)


if __name__ == "__main__":
    register()
