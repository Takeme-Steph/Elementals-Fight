import json, re, collections
b=json.load(open("/tmp/vf/v115.json"))
SPEC={"Yemoja_Atk_Punch":([1,4,7,9,14],1,14),"Yemoja_Atk_HardPunch":([1,7,12,15,26],1,26),
      "Yemoja_Atk_Kick":([1,4,7,9,16],1,16),"Yemoja_Atk_HardKick":([1,6,12,16,22,28],1,28)}
# rig bone list from v114 dump
a=json.load(open("/tmp/vf/v114.json"))
allbones=set(a["bones"])
human=set(n for n in allbones if n.startswith("mixamorig:") and not n.endswith("_end") and not n.startswith("mixamorig:Eye"))
print("humanoid bone count:",len(human))
print("non-humanoid:",sorted(allbones-human))
for name,(kf,f0,f1) in SPEC.items():
    A=b["actions"][name]
    fcs=[f for l in A["layers"] for s in l["strips"] for c in s["channelbags"] for f in c["fcurves"]]
    print("\n===",name,"fcurves",len(fcs))
    bybone=collections.defaultdict(dict)
    bad=[]
    for f in fcs:
        m=re.match(r'pose\.bones\["(.+)"\]\.(\w+)$', f["path"])
        if not m: bad.append(f["path"]); continue
        bybone[m.group(1)].setdefault(m.group(2),[]).append(f)
    if bad: print("  UNPARSED PATHS:",bad)
    keyed=set(bybone)
    print("  bones keyed:",len(keyed))
    print("  keyed but NOT humanoid:",sorted(keyed-human))
    print("  humanoid but NOT keyed:",sorted(human-keyed))
    hair=[k for k in keyed if k.startswith("hair_")]; eyes=[k for k in keyed if "Eye" in k]
    print("  hair fcurves:",hair,"  Eye fcurves:",eyes)
    # frames
    allframes=collections.Counter()
    prob=[]
    for bn,props in bybone.items():
        for prop,curves in props.items():
            if prop=="rotation_quaternion" and len(curves)!=4: prob.append((bn,prop,len(curves)))
            if prop=="location" and len(curves)!=3: prob.append((bn,prop,len(curves)))
            for c in curves:
                fr=tuple(sorted(round(k[0],4) for k in c["kps"]))
                allframes[fr]+=1
                if list(fr)!=kf: prob.append((bn,prop,c["idx"],list(fr)))
    print("  distinct frame sets:",dict(allframes))
    print("  spec frames:",kf, "MATCH" if list(list(allframes)[0])==kf and len(allframes)==1 else "MISMATCH")
    if prob: print("  PROBLEMS:",prob[:10])
    # properties per bone
    props=collections.Counter(tuple(sorted(v)) for v in bybone.values())
    print("  property sets:",dict(props))
    loc_bones=[bn for bn,v in bybone.items() if "location" in v]
    print("  bones with location keys:",loc_bones)
    scale=[bn for bn,v in bybone.items() if "scale" in v]
    print("  bones with scale keys:",scale)
    print("  action frame_range",A["frame_range"],"expected",[f0,f1])
    # interpolation types
    interps=collections.Counter(k[2] for f in fcs for k in f["kps"])
    print("  interpolation:",dict(interps))
    hl=collections.Counter((k[7],k[8]) for f in fcs for k in f["kps"])
    print("  handles:",dict(hl))
    ext=collections.Counter(f["extrap"] for f in fcs)
    print("  extrapolation:",dict(ext))
