import json
a=json.load(open("/tmp/vf/v114.json")); b=json.load(open("/tmp/vf/v115.json"))
print("=== scene ===")
for k in ("fps","fps_base","frame_start","frame_end"):
    print(k, a[k], "->", b[k])
print("\n=== actions ===")
print("v114:", sorted(a["actions"]))
print("v115:", sorted(b["actions"]))
for n in sorted(b["actions"]):
    A=b["actions"][n]
    nf=sum(len(c["fcurves"]) for l in A["layers"] for s in l["strips"] for c in s["channelbags"])
    print("  %-45s fake=%s range=%s slots=%s layers=%d fcurves=%d" % (n,A["fake"],A["frame_range"],A["slots"],len(A["layers"]),nf))
print("\n=== idle actions byte-compare ===")
for n in ("Yemoja_Idle_MASTER","Yemoja_Idle_MASTER_v113_corkscrew"):
    if n not in a["actions"]: print(n,"MISSING in v114"); continue
    if n not in b["actions"]: print(n,"MISSING in v115"); continue
    same = a["actions"][n]==b["actions"][n]
    print(n, "IDENTICAL" if same else "DIFFERENT")
    if not same:
        for k in a["actions"][n]:
            if a["actions"][n][k]!=b["actions"][n].get(k):
                print("   field differs:",k)
                if k=="layers":
                    fa={(f["path"],f["idx"]):f for l in a["actions"][n]["layers"] for s in l["strips"] for c in s["channelbags"] for f in c["fcurves"]}
                    fb={(f["path"],f["idx"]):f for l in b["actions"][n]["layers"] for s in l["strips"] for c in s["channelbags"] for f in c["fcurves"]}
                    print("    curves only in v114:", sorted(set(fa)-set(fb))[:5])
                    print("    curves only in v115:", sorted(set(fb)-set(fa))[:5])
                    d=[k2 for k2 in fa if k2 in fb and fa[k2]!=fb[k2]]
                    print("    curves differing:", len(d), d[:5])
                    for k2 in d[:3]:
                        print("      v114",fa[k2]); print("      v115",fb[k2])
                else:
                    print("    v114:",a["actions"][n][k]); print("    v115:",b["actions"][n][k])
print("\n=== objects ===")
oa=set(a["objects"]); ob=set(b["objects"])
print("only v114:",sorted(oa-ob)); print("only v115:",sorted(ob-oa))
for n in sorted(oa&ob):
    da,db=a["objects"][n],b["objects"][n]
    diffs=[k for k in da if da[k]!=db[k]]
    if diffs: print("  DIFF",n,diffs, {k:(da[k],db[k]) for k in diffs if k not in ("vgroups",)})
print("\n=== materials ===", "same" if a["materials"]==b["materials"] else (a["materials"],b["materials"]))
print("=== cameras ===", "same" if a["cameras"]==b["cameras"] else (set(a["cameras"])^set(b["cameras"])))
print("n cameras", len(b["cameras"]))
print("=== bones ===")
ba=set(a["bones"]); bb=set(b["bones"])
print("count",len(ba),len(bb),"only114",sorted(ba-bb),"only115",sorted(bb-ba))
bd=[n for n in ba&bb if a["bones"][n]!=b["bones"][n]]
print("bones differing:",bd)
print("=== body weights (2000 sampled verts) ===")
wa=a["body_weights"]; wb=b["body_weights"]
diff=[k for k in wa if wa[k]!=wb.get(k)]
print("sampled",len(wa),"differing",len(diff), diff[:5])
print("body vertex coord hash:", a["body_co_hash"], b["body_co_hash"], "SAME" if a["body_co_hash"]==b["body_co_hash"] else "DIFFER")
print("n objects", len(oa), len(ob))
