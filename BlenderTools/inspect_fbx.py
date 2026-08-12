import bpy, sys, json
argv = sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
path = argv[0]
bpy.ops.wm.read_factory_settings(use_empty=True)
ok = False
for op in ('wm.fbx_import','import_scene.fbx'):
    try:
        mod, fn = op.split('.')
        getattr(getattr(bpy.ops, mod), fn)(filepath=path)
        ok = True
        print('IMPORTER_USED:', op)
        break
    except Exception as e:
        print('importer', op, 'failed:', e)
if not ok:
    print('NO_IMPORTER'); sys.exit(1)
out = []
for ob in bpy.data.objects:
    if ob.type != 'MESH': continue
    me = ob.data
    info = {'object': ob.name, 'verts': len(me.vertices), 'polys': len(me.polygons),
            'uv_layers': [l.name for l in me.uv_layers],
            'materials': [m.name if m else None for m in me.materials]}
    if me.uv_layers:
        l0 = me.uv_layers[0].data
        z = sum(1 for d in l0 if abs(d.uv[0])<1e-6 and abs(d.uv[1])<1e-6)
        info['uv0_loops'] = len(l0); info['uv0_zero_loops'] = z
        info['uv0_pct_zero'] = round(100.0*z/max(1,len(l0)), 1)
    out.append(info)
print('JSON_START')
print(json.dumps(out, indent=1))
print('JSON_END')