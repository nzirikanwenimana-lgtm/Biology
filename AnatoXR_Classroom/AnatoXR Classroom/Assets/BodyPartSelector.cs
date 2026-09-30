using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;

public class BodyPartSelector : MonoBehaviour
{
    [Header("Information")]
    [Tooltip("Optional custom display name. If empty, the GameObject name is used.")]
    public string displayName;

    [Tooltip("The organ/body system this part belongs to (e.g., Skeletal System). If empty, auto-detected from parent group.")]
    public string systemName;

    [Tooltip("Classification or anatomical category (e.g., Long Bone, Cranial Nerve).")]
    public string bodyPartTypeText;

    [TextArea(3, 6)]
    public string description;

    [TextArea(2, 5)]
    public string function;

    public string location;

    [Header("Anatomy Database")]
    [Tooltip("Reference to the central AnatomyDatabase ScriptableObject. Assigned automatically by 'Anatomy → Setup Selected Anatomy'.")]
    public AnatomyDatabase anatomyDatabase;

    [Header("Fallback UI References (Optional)")]
    [Tooltip("Optional direct panel reference. If BodyPartInfoUI is in the scene, these are not required.")]
    public GameObject infoPanel;
    public TMP_Text bodyPartName;
    public TMP_Text bodyPartSystem;
    public TMP_Text bodyPartType;
    public TMP_Text bodyPartDescription;
    public TMP_Text bodyPartFunction;
    public TMP_Text bodyPartLocation;

    [Header("Media & Visuals")]
    [Tooltip("Optional anatomical diagram or illustration sprite.")]
    public Sprite diagramSprite;

    [Header("Visual Feedback (Optional)")]
    [Tooltip("Highlight color when hovered or selected.")]
    public Color highlightColor = new Color(0.25f, 0.75f, 1f, 1f);
    public Color selectedHighlightColor = new Color(0.2f, 0.85f, 0.4f, 1f);

    public static BodyPartSelector CurrentlySelected { get; private set; }
    public static event System.Action<BodyPartSelector> OnPartSelected;

    private Renderer _renderer;
    private Material[] _originalMaterials;
    private Material[] _highlightMaterials;
    private bool _isHovered = false;
    private bool _isSelected = false;
    private AnatomyHighlight _highlightComponent;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _highlightComponent = GetComponent<AnatomyHighlight>();
        if (_renderer != null)
        {
            _originalMaterials = _renderer.sharedMaterials;
        }
    }

    /// <summary>
    /// Returns the formatted display name of this body part.
    /// Converts raw IDs like 'Femur.l' into 'Femur (Left)'.
    /// </summary>
    public string GetDisplayName()
    {
        if (!string.IsNullOrEmpty(displayName))
            return displayName;

        return AnatomyNamingUtils.FormatDisplayName(gameObject.name);
    }

    /// <summary>
    /// Highlights or un-highlights the mesh on hover without allocating runtime materials.
    /// </summary>
    public void SetHovered(bool hovered)
    {
        _isHovered = hovered;
        UpdateHighlightVisuals();
    }

    /// <summary>
    /// Sets persistent selection highlight state.
    /// </summary>
    public void SetSelectedState(bool selected)
    {
        _isSelected = selected;
        UpdateHighlightVisuals();
    }

    private void UpdateHighlightVisuals()
    {
        bool shouldHighlight = _isHovered || _isSelected;

        if (_highlightComponent != null)
        {
            _highlightComponent.SetSelected(shouldHighlight);
            return;
        }

        if (_renderer == null) return;

        if (shouldHighlight)
        {
            if (_originalMaterials == null || _originalMaterials.Length == 0)
            {
                _originalMaterials = _renderer.sharedMaterials;
            }

            if (_originalMaterials != null && _originalMaterials.Length > 0)
            {
                Color colorToUse = _isSelected ? selectedHighlightColor : highlightColor;
                Material sharedMat = AnatomyHighlight.GetSharedHighlightMaterial(colorToUse);
                if (_highlightMaterials == null || _highlightMaterials.Length != _originalMaterials.Length)
                {
                    _highlightMaterials = new Material[_originalMaterials.Length];
                }
                for (int i = 0; i < _highlightMaterials.Length; i++)
                {
                    _highlightMaterials[i] = sharedMat;
                }
                _renderer.sharedMaterials = _highlightMaterials;
            }
        }
        else
        {
            if (_originalMaterials != null && _originalMaterials.Length > 0)
            {
                _renderer.sharedMaterials = _originalMaterials;
            }
        }
    }

    /// <summary>
    /// Direct selection trigger from mouse, VR raycaster, or direct hand touch.
    /// </summary>
    public void SelectThisPart()
    {
        if (CurrentlySelected != null && CurrentlySelected != this)
        {
            CurrentlySelected.SetSelectedState(false);
        }

        CurrentlySelected = this;
        SetSelectedState(true);
        ShowInfo();
        OnPartSelected?.Invoke(this);
    }

    /// <summary>
    /// Hides this anatomical part to reveal deeper structures beneath it.
    /// </summary>
    public void HideThisPart()
    {
        if (CurrentlySelected == this)
        {
            SetSelectedState(false);
            CurrentlySelected = null;
        }
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Shows this anatomical part.
    /// </summary>
    public void ShowThisPart()
    {
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Returns the system name, auto-detecting from parent hierarchy if not set.
    /// </summary>
    public string GetSystemName()
    {
        if (!string.IsNullOrWhiteSpace(systemName))
            return systemName;

        Transform curr = transform.parent;
        while (curr != null)
        {
            string pName = curr.name.ToLowerInvariant();
            if (pName.Contains("skelet") || pName.Contains("bone")) return "Skeletal System";
            if (pName.Contains("muscul") || pName.Contains("muscle")) return "Muscular System";
            if (pName.Contains("joint") || pName.Contains("articul")) return "Joints & Articulations";
            if (pName.Contains("cardio") || pName.Contains("circulat") || pName.Contains("arter") || pName.Contains("vein")) return "Cardiovascular System";
            if (pName.Contains("lymph")) return "Lymphoid Organs";
            if (pName.Contains("nerv") || pName.Contains("brain")) return "Nervous System";
            if (pName.Contains("viscer") || pName.Contains("organ")) return "Visceral Systems";
            if (pName.Contains("region")) return "Regions of Human Body";
            curr = curr.parent;
        }

        return "General Anatomy";
    }

    public void ShowInfo()
    {
        // --- Database lookup: inject data into local fields if they are blank ---
        // This means manual overrides always win; database only fills in the gaps.
        InjectDatabaseData();

        // 1. Try centralized singleton UI manager first
        if (BodyPartInfoUI.Instance != null)
        {
            BodyPartInfoUI.Instance.DisplayBodyPart(this);
            return;
        }

        // 2. Try finding BodyPartInfoUI in scene (including inactive) if singleton wasn't initialized yet
        BodyPartInfoUI foundUI = FindFirstObjectByType<BodyPartInfoUI>(FindObjectsInactive.Include);
        if (foundUI != null)
        {
            foundUI.DisplayBodyPart(this);
            return;
        }

        // 3. Fallback to direct local references with strict null-checking
        if (infoPanel != null)
        {
            infoPanel.SetActive(true);
        }

        if (bodyPartName != null)
        {
            bodyPartName.text = GetDisplayName();
        }

        if (bodyPartSystem != null)
        {
            bodyPartSystem.text = GetSystemName();
        }

        if (bodyPartType != null)
        {
            bodyPartType.text = !string.IsNullOrWhiteSpace(bodyPartTypeText) ? bodyPartTypeText : "";
        }

        if (bodyPartDescription != null)
        {
            bodyPartDescription.text = !string.IsNullOrWhiteSpace(description) ? description : "No detailed description available.";
        }

        if (bodyPartFunction != null)
        {
            bodyPartFunction.text = !string.IsNullOrWhiteSpace(function) ? function : "";
        }

        if (bodyPartLocation != null)
        {
            bodyPartLocation.text = !string.IsNullOrWhiteSpace(location) ? location : "";
        }
    }

    public void OnSelectEntered(SelectEnterEventArgs args)
    {
        SelectThisPart();

        // Subtle tactile haptic confirmation on Quest controller
        if (args != null && args.interactorObject is UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor inputInteractor)
        {
            inputInteractor.SendHapticImpulse(0.5f, 0.1f);
        }
    }

    /// <summary>
    /// Direct physical touch from VR hand/controller collider.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        // Check if touching collider belongs to VR controller, hand interactor, or player
        var directInteractor = other.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor>();
        if (directInteractor != null)
        {
            SelectThisPart();
            if (directInteractor is UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor inputInteractor)
            {
                inputInteractor.SendHapticImpulse(0.5f, 0.1f);
            }
            return;
        }

        string tagOrName = other.gameObject.name.ToLower();
        if (tagOrName.Contains("hand") || tagOrName.Contains("controller") || other.CompareTag("Player"))
        {
            SelectThisPart();
        }
    }

    public void OnHoverEntered(HoverEnterEventArgs args)
    {
        SetHovered(true);
    }

    public void OnHoverExited(HoverExitEventArgs args)
    {
        SetHovered(false);
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Looks up this GameObject's name in the AnatomyDatabase and populates
    /// local fields that are currently blank. Existing non-empty values are
    /// never overwritten, so manual Inspector data always takes priority.
    /// Falls back to Resources.Load if the Inspector reference is unassigned.
    /// </summary>
    private void InjectDatabaseData()
    {
        // Try to get database from Inspector reference first, then Resources fallback
        AnatomyDatabase db = anatomyDatabase;

        if (db == null)
        {
            db = Resources.Load<AnatomyDatabase>("AnatomyDatabase");
        }

        if (db == null) return;

        AnatomyEntry entry = db.FindEntry(gameObject.name);
        if (entry == null) return;

        // Only fill blank fields — never overwrite existing data
        if (string.IsNullOrWhiteSpace(systemName) && !string.IsNullOrWhiteSpace(entry.systemName))
            systemName = entry.systemName;

        if (string.IsNullOrWhiteSpace(bodyPartTypeText) && !string.IsNullOrWhiteSpace(entry.partType))
            bodyPartTypeText = entry.partType;

        if (string.IsNullOrWhiteSpace(description) && !string.IsNullOrWhiteSpace(entry.description))
            description = entry.description;

        if (string.IsNullOrWhiteSpace(function) && !string.IsNullOrWhiteSpace(entry.function))
            function = entry.function;

        if (string.IsNullOrWhiteSpace(location) && !string.IsNullOrWhiteSpace(entry.location))
            location = entry.location;
    }
}