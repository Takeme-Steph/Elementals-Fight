using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
#if UNITY_EDITOR
using UnityEditor;
#endif

// NOTE: the whole class is wrapped in UNITY_EDITOR.
// The CharacterImport scene is a development tool, not part of the shipped game, so this
// class must not exist in a player build. An Editor-only assembly definition would have
// been the tidier way to say that, but Unity refuses to attach a MonoBehaviour that lives
// in an editor assembly to a scene GameObject, so the preprocessor guard is the route that
// actually works: an ordinary MonoBehaviour in the editor, compiled away in a build.

#if UNITY_EDITOR
/// <summary>
/// Character audit rig for the CharacterImport scene.
///
/// WHY THIS EXISTS
/// Auditing a freshly imported character in FightScene means loading the whole game:
/// managers, UI, the other fighters, input. That is slow, and it hides problems, because
/// a fault in the model looks the same as a fault in the game code. This scene loads
/// nothing but a light, a floor and the character, so anything wrong is the character.
///
/// HOW IT DRIVES ANIMATION
/// It does not use an AnimatorController. It builds a PlayableGraph by hand and feeds one
/// AnimationClipPlayable into the Animator. That matters twice over: it plays ANY clip on
/// ANY rig without first wiring a state machine, and because the graph is set to Manual
/// time we advance the clock ourselves, which is what gives pause, scrub and slow motion.
/// Humanoid retargeting still runs exactly as it does in game, so what you see here is
/// what the fight scene will show.
/// </summary>
[DisallowMultipleComponent]
public class CharacterPreview : MonoBehaviour
{
    // A clip plus a human-readable label. Mixamo names every clip inside its FBX
    // "mixamo.com", so the clip's own name is useless in a picker; the file name is
    // the thing that actually identifies it.
    class Entry
    {
        public AnimationClip clip;
        public string label;
    }

    [Header("Subject")]
    [Tooltip("Parent the character sits under. Drop a model or prefab in here.")]
    public Transform characterRoot;

    [Header("Where to look for clips")]
    [Tooltip("Folders scanned for AnimationClips, in addition to the model's own asset.")]
    public string[] clipFolders = new[] { "Assets/Animations" };

    [Tooltip("Optional: also pull every clip out of this controller.")]
    public RuntimeAnimatorController clipSource;

    [Tooltip("Clips added by hand, on top of everything found automatically.")]
    public List<AnimationClip> extraClips = new List<AnimationClip>();

    [Header("Stage")]
    public Transform turntable;
    public Camera previewCamera;
    public Light keyLight;
    public Transform floor;

    [Header("Playback")]
    public bool autoSpin = false;
    public float spinSpeed = 30f;

    // ---- runtime state -------------------------------------------------
    Animator _animator;
    PlayableGraph _graph;
    AnimationClipPlayable _clipPlayable;

    readonly List<Entry> _clips = new List<Entry>();
    readonly List<Renderer> _renderers = new List<Renderer>();

    int _clipIndex = -1;
    double _time;
    bool _playing = true;
    bool _loop = true;
    float _speed = 1f;

    Vector2 _scroll;
    bool _showRenderers = true;
    bool _showStats = true;
    bool _showClipList = false;
    int _framing;                       // 0 full, 1 torso, 2 head
    float _orbit, _pitch = 8f, _dist = 6f;
    static readonly Color BgDark = new Color(0.06f, 0.07f, 0.09f);
    static readonly Color BgLight = new Color(0.78f, 0.80f, 0.84f);

    string _stats = "";
    Transform _lFoot, _rFoot, _lToe, _rToe;

    // ====================================================================
    void OnEnable() { Rebind(); }
    void OnDisable() { DestroyGraph(); }

    /// <summary>Find the character, collect clips and renderers, build the graph.</summary>
    public void Rebind()
    {
        DestroyGraph();
        _clips.Clear();
        _renderers.Clear();
        _animator = null;
        _lFoot = _rFoot = _lToe = _rToe = null;

        Transform root = characterRoot != null ? characterRoot : transform;
        _animator = root.GetComponentInChildren<Animator>(true);
        if (_animator == null)
        {
            _stats = "No Animator found under " + root.name +
                     ".\nDrag a character model or prefab under CharacterStage, then press Rebind.";
            return;
        }

        root.GetComponentsInChildren(true, _renderers);

        if (_animator.avatar != null && _animator.avatar.isHuman)
        {
            _lFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            _lToe = _animator.GetBoneTransform(HumanBodyBones.LeftToes);
            _rToe = _animator.GetBoneTransform(HumanBodyBones.RightToes);
        }

        CollectClips();
        BuildStats();

        if (_clips.Count > 0) SelectClip(0);
        FrameCamera();
    }

    // ---- clip discovery -------------------------------------------------
    void CollectClips()
    {
        var seen = new HashSet<AnimationClip>();
        var found = new List<AnimationClip>();

        foreach (var c in extraClips)
            if (c != null && seen.Add(c)) found.Add(c);

        if (clipSource != null)
            foreach (var c in clipSource.animationClips)
                if (c != null && seen.Add(c)) found.Add(c);

        if (_animator.runtimeAnimatorController != null)
            foreach (var c in _animator.runtimeAnimatorController.animationClips)
                if (c != null && seen.Add(c)) found.Add(c);

        // Clips that live inside the model asset itself (a rig exported with its own takes).
        Object src = PrefabUtility.GetCorrespondingObjectFromSource(_animator.gameObject);
        string modelPath = src != null ? AssetDatabase.GetAssetPath(src) : null;
        if (string.IsNullOrEmpty(modelPath) && _animator.avatar != null)
            modelPath = AssetDatabase.GetAssetPath(_animator.avatar);
        if (!string.IsNullOrEmpty(modelPath)) AddClipsFromAsset(modelPath, seen, found);

        // Clips in the project's animation folders. This is the case that matters for this
        // project: the Mixamo takes each live in their own FBX, referenced by a controller
        // the raw imported model has never been given.
        if (clipFolders != null && clipFolders.Length > 0)
        {
            var folders = new List<string>();
            foreach (var f in clipFolders) if (!string.IsNullOrEmpty(f) && AssetDatabase.IsValidFolder(f)) folders.Add(f);
            if (folders.Count > 0)
                foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", folders.ToArray()))
                    AddClipsFromAsset(AssetDatabase.GUIDToAssetPath(guid), seen, found);
        }

        // Label each clip. Group by source file so a file holding one clip is named after
        // the file, and a file holding several keeps the clip name as a suffix.
        var perFile = new Dictionary<string, int>();
        foreach (var c in found)
        {
            string path = AssetDatabase.GetAssetPath(c);
            int n; perFile.TryGetValue(path, out n);
            perFile[path] = n + 1;
        }
        foreach (var c in found)
        {
            string path = AssetDatabase.GetAssetPath(c);
            string file = string.IsNullOrEmpty(path) ? "" : System.IO.Path.GetFileNameWithoutExtension(path);
            string label;
            if (string.IsNullOrEmpty(file)) label = c.name;
            else if (perFile[path] > 1) label = file + " / " + c.name;
            else label = file;
            _clips.Add(new Entry { clip = c, label = label });
        }
        _clips.Sort((a, b) => string.Compare(a.label, b.label, System.StringComparison.OrdinalIgnoreCase));
    }

    static void AddClipsFromAsset(string path, HashSet<AnimationClip> seen, List<AnimationClip> into)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            var c = o as AnimationClip;
            // Skip Unity's hidden __preview__ clips, which are not real takes.
            if (c == null) continue;
            if ((c.hideFlags & HideFlags.HideInHierarchy) != 0) continue;
            if (c.name.StartsWith("__preview__")) continue;
            if (seen.Add(c)) into.Add(c);
        }
    }

    // ---- stats ----------------------------------------------------------
    void BuildStats()
    {
        int tris = 0, submeshes = 0, bones = 0, nullSlots = 0;
        var mats = new HashSet<Material>();
        foreach (var r in _renderers)
        {
            Mesh m = null;
            var smr = r as SkinnedMeshRenderer;
            if (smr != null)
            {
                m = smr.sharedMesh;
                if (smr.bones != null) bones = Mathf.Max(bones, smr.bones.Length);
            }
            else
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null) m = mf.sharedMesh;
            }

            if (m != null)
            {
                submeshes += m.subMeshCount;
                for (int i = 0; i < m.subMeshCount; i++) tris += (int)(m.GetIndexCount(i) / 3);
            }
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null) nullSlots++;
                else mats.Add(mat);
            }
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("renderers   " + _renderers.Count);
        sb.AppendLine("submeshes   " + submeshes);
        sb.AppendLine("triangles   " + tris.ToString("N0"));
        sb.AppendLine("materials   " + mats.Count + (nullSlots > 0 ? "   (" + nullSlots + " EMPTY SLOTS)" : ""));
        sb.AppendLine("bones       " + bones);
        sb.AppendLine("clips       " + _clips.Count);
        if (_animator != null && _animator.avatar != null)
            sb.AppendLine("avatar      valid=" + _animator.avatar.isValid + "  human=" + _animator.avatar.isHuman);
        else
            sb.AppendLine("avatar      NONE");
        _stats = sb.ToString();
    }

    // ---- playable graph --------------------------------------------------
    void DestroyGraph() { if (_graph.IsValid()) _graph.Destroy(); }

    public void SelectClip(int index)
    {
        if (_animator == null || index < 0 || index >= _clips.Count) return;
        DestroyGraph();

        _clipIndex = index;
        _time = 0;

        _graph = PlayableGraph.Create("CharacterPreview");
        _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);   // we drive the clock ourselves
        var output = AnimationPlayableOutput.Create(_graph, "Animation", _animator);
        _clipPlayable = AnimationClipPlayable.Create(_graph, _clips[index].clip);
        _clipPlayable.SetApplyFootIK(true);
        _clipPlayable.SetSpeed(0);                             // manual scrubbing
        output.SetSourcePlayable(_clipPlayable);
        _graph.Play();
        Evaluate();
    }

    void Evaluate()
    {
        if (!_graph.IsValid()) return;
        _clipPlayable.SetTime(_time);
        _graph.Evaluate(0f);
    }

    void Update()
    {
        if (_graph.IsValid() && _clipIndex >= 0)
        {
            float len = _clips[_clipIndex].clip.length;
            if (_playing && len > 0f)
            {
                _time += Time.unscaledDeltaTime * _speed;
                if (_time > len)
                {
                    if (_loop) _time -= len;
                    else { _time = len; _playing = false; }
                }
            }
            Evaluate();
        }

        if (autoSpin && turntable != null)
            turntable.Rotate(Vector3.up, spinSpeed * Time.unscaledDeltaTime, Space.World);

        ApplyCamera();
    }

    // ---- camera -----------------------------------------------------------
    Bounds SubjectBounds()
    {
        bool any = false;
        var b = new Bounds(Vector3.zero, Vector3.zero);
        foreach (var r in _renderers)
        {
            if (r == null || !r.enabled) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    public void FrameCamera()
    {
        var b = SubjectBounds();
        _dist = Mathf.Max(2f, b.size.magnitude * 1.1f);
    }

    void ApplyCamera()
    {
        if (previewCamera == null) return;
        var b = SubjectBounds();
        if (b.size == Vector3.zero) return;

        Vector3 target;
        float dist;
        switch (_framing)
        {
            case 1: target = new Vector3(b.center.x, b.min.y + b.size.y * 0.68f, b.center.z); dist = _dist * 0.42f; break;
            case 2: target = new Vector3(b.center.x, b.max.y - b.size.y * 0.07f, b.center.z); dist = _dist * 0.16f; break;
            default: target = b.center; dist = _dist; break;
        }

        var rot = Quaternion.Euler(_pitch, _orbit, 0f);
        previewCamera.transform.position = target + rot * new Vector3(0f, 0f, -dist);
        previewCamera.transform.rotation = rot;
        previewCamera.nearClipPlane = Mathf.Max(0.01f, dist * 0.01f);
    }

    // ---- measurement ------------------------------------------------------
    // Renderer.bounds on a SkinnedMeshRenderer is a cached, deliberately generous volume;
    // it does not tighten frame by frame. So for the one number that has to be exact --
    // how far the feet are off the floor -- read the actual foot bones instead.
    bool TryFootClearance(out float clearance)
    {
        clearance = 0f;
        if (_lFoot == null && _rFoot == null) return false;
        float floorY = floor != null ? floor.position.y : 0f;
        float lowest = float.MaxValue;
        if (_lToe != null) lowest = Mathf.Min(lowest, _lToe.position.y);
        if (_rToe != null) lowest = Mathf.Min(lowest, _rToe.position.y);
        if (_lToe == null && _lFoot != null) lowest = Mathf.Min(lowest, _lFoot.position.y);
        if (_rToe == null && _rFoot != null) lowest = Mathf.Min(lowest, _rFoot.position.y);
        if (lowest == float.MaxValue) return false;
        clearance = lowest - floorY;
        return true;
    }

    // ---- on-screen controls -----------------------------------------------
    void OnGUI()
    {
        const float W = 310f;
        GUILayout.BeginArea(new Rect(8, 8, W, Screen.height - 16), GUI.skin.box);
        _scroll = GUILayout.BeginScrollView(_scroll);

        GUILayout.Label("<b>CHARACTER IMPORT AUDIT</b>", Rich());
        if (GUILayout.Button("Rebind / refresh")) Rebind();

        if (_animator == null)
        {
            GUILayout.Label(_stats, Rich());
            GUILayout.EndScrollView(); GUILayout.EndArea();
            return;
        }

        // --- animation ---
        GUILayout.Space(6);
        GUILayout.Label("<b>ANIMATION</b>", Rich());
        if (_clips.Count == 0)
        {
            GUILayout.Label("No clips found. Check the clipFolders list on PreviewRig.", Rich());
        }
        else
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(26))) SelectClip((_clipIndex - 1 + _clips.Count) % _clips.Count);
            if (GUILayout.Button(_clips[_clipIndex].label, GUILayout.ExpandWidth(true))) _showClipList = !_showClipList;
            if (GUILayout.Button(">", GUILayout.Width(26))) SelectClip((_clipIndex + 1) % _clips.Count);
            GUILayout.EndHorizontal();

            if (_showClipList)
            {
                for (int i = 0; i < _clips.Count; i++)
                {
                    bool sel = i == _clipIndex;
                    if (GUILayout.Toggle(sel, "  " + _clips[i].label) && !sel)
                    {
                        SelectClip(i);
                        _showClipList = false;
                    }
                }
            }

            float len = _clips[_clipIndex].clip.length;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_playing ? "Pause" : "Play", GUILayout.Width(62))) _playing = !_playing;
            if (GUILayout.Button("|<", GUILayout.Width(30))) { _time = 0; _playing = false; }
            _loop = GUILayout.Toggle(_loop, "loop");
            GUILayout.EndHorizontal();

            GUILayout.Label(string.Format("{0:0.00}s / {1:0.00}s    clip fps {2:0}    loop flag {3}",
                _time, len, _clips[_clipIndex].clip.frameRate, _clips[_clipIndex].clip.isLooping));
            float t = GUILayout.HorizontalSlider((float)_time, 0f, Mathf.Max(0.0001f, len));
            if (!Mathf.Approximately(t, (float)_time)) { _time = t; _playing = false; }

            GUILayout.Label(string.Format("speed  {0:0.00}x", _speed));
            _speed = GUILayout.HorizontalSlider(_speed, 0f, 2f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("0.1x")) _speed = 0.1f;
            if (GUILayout.Button("0.25x")) _speed = 0.25f;
            if (GUILayout.Button("1x")) _speed = 1f;
            GUILayout.EndHorizontal();
        }

        // --- camera ---
        GUILayout.Space(6);
        GUILayout.Label("<b>CAMERA</b>", Rich());
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Full")) _framing = 0;
        if (GUILayout.Button("Torso")) _framing = 1;
        if (GUILayout.Button("Face")) _framing = 2;
        GUILayout.EndHorizontal();
        GUILayout.Label("orbit");   _orbit = GUILayout.HorizontalSlider(_orbit, -180f, 180f);
        GUILayout.Label("pitch");   _pitch = GUILayout.HorizontalSlider(_pitch, -40f, 60f);
        GUILayout.Label("distance"); _dist = GUILayout.HorizontalSlider(_dist, 0.5f, 20f);
        GUILayout.BeginHorizontal();
        autoSpin = GUILayout.Toggle(autoSpin, "auto-spin");
        if (GUILayout.Button("Reset", GUILayout.Width(62))) { _orbit = 0f; _pitch = 8f; FrameCamera(); }
        GUILayout.EndHorizontal();

        // --- lighting ---
        GUILayout.Space(6);
        GUILayout.Label("<b>LIGHTING</b>", Rich());
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Sky")) SetBg(0);
        if (GUILayout.Button("Dark")) SetBg(1);
        if (GUILayout.Button("Light")) SetBg(2);
        GUILayout.EndHorizontal();
        if (keyLight != null)
        {
            GUILayout.Label(string.Format("key intensity  {0:0.00}   (game uses 2.00)", keyLight.intensity));
            keyLight.intensity = GUILayout.HorizontalSlider(keyLight.intensity, 0f, 5f);
            float yaw = keyLight.transform.eulerAngles.y;
            GUILayout.Label(string.Format("key yaw  {0:0}   (game uses 330)", yaw));
            float ny = GUILayout.HorizontalSlider(yaw, 0f, 360f);
            if (!Mathf.Approximately(ny, yaw))
                keyLight.transform.rotation = Quaternion.Euler(keyLight.transform.eulerAngles.x, ny, 0f);
        }
        if (floor != null)
            floor.gameObject.SetActive(GUILayout.Toggle(floor.gameObject.activeSelf, "floor"));

        // --- renderer isolation ---
        GUILayout.Space(6);
        _showRenderers = GUILayout.Toggle(_showRenderers, "<b>MESHES</b>   isolate to find clipping", Rich());
        if (_showRenderers)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("all on")) foreach (var r in _renderers) if (r != null) r.enabled = true;
            if (GUILayout.Button("all off")) foreach (var r in _renderers) if (r != null) r.enabled = false;
            GUILayout.EndHorizontal();
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.enabled = GUILayout.Toggle(r.enabled, r.name);
            }
        }

        // --- measurements ---
        GUILayout.Space(6);
        _showStats = GUILayout.Toggle(_showStats, "<b>STATS</b>", Rich());
        if (_showStats)
        {
            GUILayout.Label(_stats, Rich());
            var b = SubjectBounds();
            GUILayout.Label(string.Format("height      {0:0.000}", b.size.y));

            float clearance;
            if (TryFootClearance(out clearance))
            {
                GUILayout.Label(string.Format("foot gap    {0:+0.0000;-0.0000}{1}", clearance,
                    clearance < -0.005f ? "   SINKING" : (clearance > 0.05f ? "   FLOATING" : "")));
                GUILayout.Label("<i>measured from the toe bones, live, every frame</i>", Rich());
            }
            else
            {
                GUILayout.Label("foot gap    n/a (not a humanoid avatar)");
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void SetBg(int mode)
    {
        if (previewCamera == null) return;
        if (mode == 0) previewCamera.clearFlags = CameraClearFlags.Skybox;
        else
        {
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = mode == 1 ? BgDark : BgLight;
        }
    }

    static GUIStyle _rich;
    static GUIStyle Rich()
    {
        if (_rich == null) _rich = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
        return _rich;
    }
}
#endif
