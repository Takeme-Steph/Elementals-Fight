using UnityEngine;

// Instantiates one ArenaTab per roster entry from a serialized (inactive) template
// child. Tapping a tab only ever raises IndexChanged - it does NOT change which tab
// reads as active. That's deliberate: the controller applies the new arena (panorama,
// particles, header, badges, dock, confirm accent) together at the warp flash's peak,
// so the dock's active-tab visual has to be driven externally via SetActive rather
// than reacting to its own taps.

/// <summary>
/// Horizontal row of ArenaTab slots built from the arena roster.
/// </summary>
public class ArenaTabDock : MonoBehaviour
{
    [SerializeField]
    private RectTransform content;

    [SerializeField]
    [Tooltip("Inactive child under content; cloned once per roster entry, never shown itself.")]
    private ArenaTab template;

    public event System.Action<int> IndexChanged;

    private bool validRefs = true;
    private ArenaTab[] tabs;

    private void Awake()
    {
        if (content == null || template == null)
        {
            Debug.LogError("ArenaTabDock: content and template must be assigned.");
            validRefs = false;
        }
    }

    /// <summary>Clears any previously built tabs and instantiates one per roster entry.</summary>
    public void Build(ArenaRoster roster)
    {
        if (!validRefs)
        {
            return;
        }

        if (roster == null)
        {
            Debug.LogError("ArenaTabDock.Build: roster is null.");
            return;
        }

        ClearExisting();

        tabs = new ArenaTab[roster.Count];

        for (int i = 0; i < roster.Count; i++)
        {
            ArenaDefinition def = roster.Get(i);

            ArenaTab tab = Instantiate(template, content);
            tab.gameObject.SetActive(true);
            tab.gameObject.name = def != null ? $"Tab_{def.Id}" : $"Tab_{i}";

            if (def != null)
            {
                tab.Paint(def);
            }

            tab.SetActive(false, true);

            int capturedIndex = i;
            if (tab.Button != null)
            {
                tab.Button.onClick.AddListener(() => OnTabTapped(capturedIndex));
            }

            tabs[i] = tab;
        }
    }

    /// <summary>Sets which tab reads as active; the rest ease back to idle. Does not itself change any other widget.</summary>
    public void SetActive(int index, bool instant)
    {
        if (tabs == null)
        {
            return;
        }

        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i] != null)
            {
                tabs[i].SetActive(i == index, instant);
            }
        }
    }

    private void OnTabTapped(int index)
    {
        IndexChanged?.Invoke(index);
    }

    private void ClearExisting()
    {
        if (tabs == null)
        {
            return;
        }

        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i] != null)
            {
                Destroy(tabs[i].gameObject);
            }
        }

        tabs = null;
    }
}
