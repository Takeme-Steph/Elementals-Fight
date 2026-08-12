"""
Headless installer for the blender-mcp addon.
Usage: blender --background --python install_blender_mcp_addon.py -- <path-to-blender_mcp_addon.py>

Installs the addon into Blender's addon directory, enables it, and saves
user preferences so the addon stays enabled the next time Blender is opened
normally (this script itself never starts the live socket server - that
requires an interactive Blender session, see the "BlenderMCP" sidebar tab).
"""
import bpy
import sys
import traceback

def main():
    argv = sys.argv
    if "--" not in argv:
        print("INSTALL_FAILED: no addon path passed after '--'")
        return
    addon_path = argv[argv.index("--") + 1]

    try:
        bpy.ops.preferences.addon_install(filepath=addon_path, overwrite=True)
    except Exception as e:
        print("INSTALL_FAILED: addon_install raised: " + str(e))
        traceback.print_exc()
        return

    module_name = "blender_mcp_addon"
    try:
        bpy.ops.preferences.addon_enable(module=module_name)
    except Exception as e:
        print("INSTALL_FAILED: addon_enable raised: " + str(e))
        traceback.print_exc()
        return

    enabled = module_name in bpy.context.preferences.addons
    print("ADDON_ENABLED=" + str(enabled))

    try:
        bpy.ops.wm.save_userpref()
        print("PREFS_SAVED=True")
    except Exception as e:
        print("PREFS_SAVED=False: " + str(e))
        traceback.print_exc()
        return

    print("INSTALL_OK")

main()
