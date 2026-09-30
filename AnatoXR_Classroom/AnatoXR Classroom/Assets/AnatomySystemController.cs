using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls anatomical layer visibility (Build Book Phase 4 & Appendix B).
/// Provides data-driven layer toggling while maintaining backward-compatibility
/// with existing scene button bindings.
/// </summary>
public class AnatomySystemController : MonoBehaviour
{
    [System.Serializable]
    public class SystemLayer
    {
        public string id;
        public string displayName;
        public GameObject root;

        public SystemLayer() { }

        public SystemLayer(string id, string displayName, GameObject root)
        {
            this.id = id;
            this.displayName = displayName;
            this.root = root;
        }
    }

    [Header("Build Book v2 Data-Driven Layers")]
    [SerializeField] private List<SystemLayer> layers = new List<SystemLayer>();

    [Header("Legacy Scene References (Maintained for backward-compatibility)")]
    public GameObject maleBody;
    public GameObject skeletalSystem;
    public GameObject muscularInsertions;
    public GameObject muscularSystem;
    public GameObject circulatorySystem;
    public GameObject visceralSystem;
    public GameObject nervousSystem;
    public GameObject joints;

    private void Awake()
    {
        if (maleBody == null || skeletalSystem == null || muscularSystem == null)
        {
            AutoFindLayers();
        }
        SyncLegacyLayers();
    }

    /// <summary>
    /// Automatically searches child transforms of HumanBody or the scene to populate missing layer references.
    /// </summary>
    public void AutoFindLayers()
    {
        Transform searchRoot = transform;
        Transform humanBody = transform.Find("HumanBody") ?? transform.parent?.Find("HumanBody");
        if (humanBody != null) searchRoot = humanBody;

        List<Transform> candidates = new List<Transform>();
        for (int i = 0; i < searchRoot.childCount; i++)
            candidates.Add(searchRoot.GetChild(i));

        if (candidates.Count == 0 && transform.parent != null)
        {
            for (int i = 0; i < transform.parent.childCount; i++)
                candidates.Add(transform.parent.GetChild(i));
        }

        // Also search in scene if still empty
        if (candidates.Count == 0)
        {
            var stage = Object.FindFirstObjectByType<AnatomyDisplayStage>(FindObjectsInactive.Include);
            if (stage != null && stage.humanBody != null)
            {
                for (int i = 0; i < stage.humanBody.childCount; i++)
                    candidates.Add(stage.humanBody.GetChild(i));
            }
        }

        foreach (var c in candidates)
        {
            string n = c.name.ToLower();
            if (maleBody == null && (n.Contains("male body") || (n.Contains("body") && !n.Contains("part"))))
                maleBody = c.gameObject;
            else if (skeletalSystem == null && (n.Contains("skelet") || n.Contains("bone")))
                skeletalSystem = c.gameObject;
            else if (muscularInsertions == null && (n.Contains("insertion") || n.Contains("insert")))
                muscularInsertions = c.gameObject;
            else if (muscularSystem == null && (n.Contains("muscul") || n.Contains("muscle")))
                muscularSystem = c.gameObject;
            else if (circulatorySystem == null && (n.Contains("circulat") || n.Contains("cardio") || n.Contains("heart") || n.Contains("vessel")))
                circulatorySystem = c.gameObject;
            else if (visceralSystem == null && (n.Contains("viscer") || n.Contains("organ")))
                visceralSystem = c.gameObject;
            else if (nervousSystem == null && (n.Contains("nerv") || n.Contains("brain") || n.Contains("sense")))
                nervousSystem = c.gameObject;
            else if (joints == null && (n.Contains("joint") || n.Contains("articul")))
                joints = c.gameObject;
        }

        SyncLegacyLayers();
    }

    private void SyncLegacyLayers()
    {
        if (layers == null)
            layers = new List<SystemLayer>();

        // Auto-register legacy fields into layers list if not already present
        RegisterLegacyLayer("skin", "Skin / Body", maleBody);
        RegisterLegacyLayer("skeleton", "Skeletal System", skeletalSystem);
        RegisterLegacyLayer("muscular_insertions", "Muscular Insertions", muscularInsertions);
        RegisterLegacyLayer("muscular", "Muscular System", muscularSystem);
        RegisterLegacyLayer("circulatory", "Circulatory System", circulatorySystem);
        RegisterLegacyLayer("visceral", "Visceral Systems", visceralSystem);
        RegisterLegacyLayer("nervous", "Nervous System", nervousSystem);
        RegisterLegacyLayer("joints", "Joints", joints);

        // Aliases for dashboard grid system IDs
        RegisterLegacyLayer("respiratory", "Respiratory System", visceralSystem);
        RegisterLegacyLayer("digestive", "Digestive System", visceralSystem);
        RegisterLegacyLayer("endocrine", "Endocrine System", visceralSystem);
        RegisterLegacyLayer("renal", "Renal System", visceralSystem);
        RegisterLegacyLayer("reproductive", "Reproductive System", visceralSystem);
    }

    public IReadOnlyList<SystemLayer> GetLayers()
    {
        SyncLegacyLayers();
        return layers;
    }

    public bool IsSystemVisible(string id)
    {
        SyncLegacyLayers();
        var layer = layers.Find(l => string.Equals(l.id, id, System.StringComparison.OrdinalIgnoreCase));
        return layer != null && layer.root != null && layer.root.activeSelf;
    }

    public void RegisterLayer(string id, string name, GameObject root)
    {
        if (root == null) return;
        if (layers == null) layers = new List<SystemLayer>();
        if (!layers.Exists(l => l.id == id || l.root == root))
        {
            layers.Add(new SystemLayer(id, name, root));
        }
    }

    private void RegisterLegacyLayer(string id, string name, GameObject root)
    {
        RegisterLayer(id, name, root);
    }

    // -------------------------------------------------------------------------
    // Build Book v2 Core API (Phase 4.3 & Phase 7.1)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Shows only the specified system layer and hides all others.
    /// </summary>
    public void ShowSystem(string id)
    {
        SyncLegacyLayers();
        foreach (var layer in layers)
        {
            if (layer.root != null)
                layer.root.SetActive(string.Equals(layer.id, id, System.StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Hides the specified system layer.
    /// </summary>
    public void HideSystem(string id)
    {
        SyncLegacyLayers();
        foreach (var layer in layers)
        {
            if (layer.root != null && string.Equals(layer.id, id, System.StringComparison.OrdinalIgnoreCase))
                layer.root.SetActive(false);
        }
    }

    /// <summary>
    /// Toggles a system layer without affecting other visible layers (Phase 7 Layer Isolation).
    /// </summary>
    public void ToggleSystem(string id)
    {
        SyncLegacyLayers();
        foreach (var layer in layers)
        {
            if (layer.root != null && string.Equals(layer.id, id, System.StringComparison.OrdinalIgnoreCase))
                layer.root.SetActive(!layer.root.activeSelf);
        }
    }

    /// <summary>
    /// Restores full body visibility (all layers on).
    /// </summary>
    public void ShowAll()
    {
        SyncLegacyLayers();
        foreach (var layer in layers)
        {
            if (layer.root != null)
                layer.root.SetActive(true);
        }
    }

    /// <summary>
    /// Hides all layers.
    /// </summary>
    public void HideAll()
    {
        SyncLegacyLayers();
        foreach (var layer in layers)
        {
            if (layer.root != null)
                layer.root.SetActive(false);
        }
    }

    // -------------------------------------------------------------------------
    // Legacy Scene Event Wrappers
    // -------------------------------------------------------------------------

    public void ShowMaleBody()           => ShowSystem("skin");
    public void ShowSkeletalSystem()     => ShowSystem("skeleton");
    public void ShowMuscularInsertions() => ShowSystem("muscular_insertions");
    public void ShowMuscularSystem()     => ShowSystem("muscular");
    public void ShowCirculatorySystem()  => ShowSystem("circulatory");
    public void ShowVisceralSystem()     => ShowSystem("visceral");
    public void ShowNervousSystem()      => ShowSystem("nervous");
    public void ShowJoints()             => ShowSystem("joints");
}