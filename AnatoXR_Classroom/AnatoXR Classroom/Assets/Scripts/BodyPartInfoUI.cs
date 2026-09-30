using UnityEngine;
using TMPro;

/// <summary>
/// Centralized UI manager for displaying selected anatomical body part details.
/// Attach this script to your Info Panel (e.g. BodyPartInfoPanel) or a UI Manager GameObject.
/// </summary>
public class BodyPartInfoUI : MonoBehaviour
{
    public static BodyPartInfoUI Instance { get; private set; }

    [Header("Panel Root")]
    [Tooltip("The root UI panel GameObject to show/hide. Defaults to this GameObject if unassigned.")]
    public GameObject infoPanel;

    [Header("VR Positioning (Right-Front Medical Layout)")]
    [Tooltip("Enables ergonomic right-front VR positioning tailored for Meta Quest 3/3S.")]
    public bool enableVRPositioning = true;

    [Tooltip("Horizontal angle (in degrees) to the RIGHT of the user's forward viewing direction for midline/left structures.")]
    [Range(15f, 60f)]
    public float normalHorizontalAngle = 30f;

    [Tooltip("Adaptive horizontal angle (in degrees) to the RIGHT for right-sided structures to prevent occluding the body.")]
    [Range(25f, 75f)]
    public float rightSideHorizontalAngle = 42f;

    [Tooltip("Distance from headset to inspector panel in meters (0.8m - 1.0m recommended).")]
    [Range(0.6f, 1.5f)]
    public float distanceFromHeadset = 0.9f;

    [Tooltip("Vertical height offset relative to headset eye level (negative is slightly below eye level).")]
    [Range(-0.4f, 0.2f)]
    public float verticalOffset = -0.12f;

    [Tooltip("Transform of the human anatomy body model in the center. Auto-detected if null.")]
    public Transform anatomyModelRoot;

    [Tooltip("The root transform moved in VR space. Defaults to parent Canvas or infoPanel.")]
    public Transform inspectorRoot;

    [Tooltip("Ergonomic world scale for VR canvas (0.0008 produces crisp reading scale on Quest 3).")]
    public Vector3 canvasWorldScale = new Vector3(0.0008f, 0.0008f, 0.0008f);

    [Header("Smooth Transitions & Presentation")]
    [Tooltip("Duration of smooth position and appearance transitions in seconds.")]
    [Range(0.1f, 1.0f)]
    public float transitionDuration = 0.35f;

    [Tooltip("Easing curve for transition.")]
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Optional CanvasGroup for smooth alpha fade-in/fade-out.")]
    public CanvasGroup canvasGroup;

    [Header("Legacy Billboard Settings (Disabled when VR Positioning is active)")]
    [Tooltip("Legacy: constant billboarding every frame. Kept false for stable VR reading.")]
    public bool billboardToCamera = false;
    [Tooltip("Camera to face. Defaults to Camera.main.")]
    public Camera targetCamera;
    [Tooltip("Speed of smooth rotation toward camera.")]
    public float rotationSpeed = 8f;

    [Header("Scroll View")]
    [Tooltip("Optional scroll rect to reset scroll position on selection.")]
    public UnityEngine.UI.ScrollRect scrollRect;

    [Header("Media & Visuals (Images & Animation)")]
    [Tooltip("Media viewer component for diagrams and 3D organ animations.")]
    public AnatomyMediaViewer mediaViewer;
    [Tooltip("Optional direct Image component for anatomical diagrams.")]
    public UnityEngine.UI.Image diagramImage;

    [Header("Interactive Actions")]
    [Tooltip("Button to hide the currently selected part to reveal deeper layers.")]
    public UnityEngine.UI.Button hidePartButton;
    [Tooltip("Button to toggle 3D inspection / pulsing animation.")]
    public UnityEngine.UI.Button animationButton;

    [Header("UI Text Displays (TextMeshPro)")]
    public TMP_Text bodyPartName;
    public TMP_Text bodyPartSystem;
    public TMP_Text bodyPartType;
    public TMP_Text bodyPartDescription;
    public TMP_Text bodyPartFunction;
    public TMP_Text bodyPartLocation;

    [Header("Default Text Settings")]
    public string defaultDescription = "Detailed anatomical description not available.";
    public string defaultSystem = "General Anatomy";

    private BodyPartSelector _currentBodyPart;
    private Coroutine _transitionCoroutine;
    private Coroutine _hideCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        EnsureReferences();

        if (hidePartButton != null)
        {
            hidePartButton.onClick.AddListener(HideCurrentPart);
        }

        if (animationButton != null)
        {
            animationButton.onClick.AddListener(ToggleAnimation);
        }

        AutoFindTextReferences();
    }

    private void Start()
    {
        EnsureReferences();

        // Initial setup for VR presentation
        if (enableVRPositioning && inspectorRoot != null)
        {
            // Decouple from model parent so inspector moves independently in world space
            if (inspectorRoot.parent != null &&
                (inspectorRoot.parent.name.ToLower().Contains("anatomy") || inspectorRoot.parent.name.ToLower().Contains("body")))
            {
                inspectorRoot.SetParent(null, true);
            }

            inspectorRoot.localScale = canvasWorldScale;

            // Center panel on its Canvas if different
            if (infoPanel != null && infoPanel.transform != inspectorRoot)
            {
                RectTransform rt = infoPanel.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = Vector2.zero;
                    rt.localRotation = Quaternion.identity;
                }
            }

            // Align to initial right-front pose
            CalculateTargetPose(null, out Vector3 startPos, out Quaternion startRot);
            inspectorRoot.position = startPos;
            inspectorRoot.rotation = startRot;
        }
    }

    /// <summary>
    /// Ensures all required references (camera, anatomy root, inspector root, canvas group) are properly resolved.
    /// </summary>
    public void EnsureReferences()
    {
        if (infoPanel == null)
        {
            infoPanel = gameObject;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindFirstObjectByType<Camera>();
            }
        }

        if (inspectorRoot == null)
        {
            if (transform.parent != null && transform.parent.GetComponent<Canvas>() != null)
            {
                inspectorRoot = transform.parent;
            }
            else
            {
                inspectorRoot = (infoPanel != null) ? infoPanel.transform : transform;
            }
        }

        if (canvasGroup == null)
        {
            if (infoPanel != null)
            {
                canvasGroup = infoPanel.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = infoPanel.AddComponent<CanvasGroup>();
                }
            }
            else
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        if (anatomyModelRoot == null)
        {
            if (AnatomyDisplayStage.Instance != null && AnatomyDisplayStage.Instance.anatomyCenter != null)
            {
                anatomyModelRoot = AnatomyDisplayStage.Instance.anatomyCenter;
            }
            else
            {
                GameObject stageGo = GameObject.Find("AnatomyDisplayStage");
                if (stageGo != null)
                {
                    Transform center = stageGo.transform.Find("AnatomyCenter");
                    anatomyModelRoot = (center != null) ? center : stageGo.transform;
                }
                else
                {
                    GameObject model = GameObject.Find("Human male anatomy");
                    if (model != null)
                    {
                        anatomyModelRoot = model.transform;
                    }
                    else
                    {
                        var controller = FindFirstObjectByType<AnatomySystemController>(FindObjectsInactive.Include);
                        if (controller != null)
                        {
                            anatomyModelRoot = controller.transform.root;
                        }
                    }
                }
            }
        }

        if (mediaViewer == null)
        {
            mediaViewer = GetComponentInChildren<AnatomyMediaViewer>(true);
        }
    }

    private void LateUpdate()
    {
        // Stable VR Reading: When enableVRPositioning is true, the inspector remains
        // completely stable and does NOT rotate every frame with head micro-movements.
        if (!enableVRPositioning && billboardToCamera && infoPanel != null && infoPanel.activeInHierarchy)
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera != null)
            {
                Vector3 lookDir = transform.position - targetCamera.transform.position;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Displays the details of the given body part on the UI panel safely and smoothly positions
    /// the Anatomy Inspector at the user's Right-Front viewing angle (+30° or adaptive +42°).
    /// </summary>
    public void DisplayBodyPart(BodyPartSelector bodyPart)
    {
        if (bodyPart == null) return;

        _currentBodyPart = bodyPart;
        EnsureReferences();

        if (_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }

        bool wasInactive = (infoPanel != null && !infoPanel.activeSelf) || (inspectorRoot != null && !inspectorRoot.gameObject.activeSelf);

        if (infoPanel != null)
        {
            infoPanel.SetActive(true);
        }

        if (inspectorRoot != null && !inspectorRoot.gameObject.activeSelf)
        {
            inspectorRoot.gameObject.SetActive(true);
        }

        // Apply Right-Front VR positioning with smooth transition
        if (enableVRPositioning && inspectorRoot != null)
        {
            CalculateTargetPose(bodyPart, out Vector3 targetPos, out Quaternion targetRot);

            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
            }
            _transitionCoroutine = StartCoroutine(AnimateToPose(targetPos, targetRot, wasInactive));
        }

        // Populate Text Fields
        if (bodyPartName != null)
        {
            bodyPartName.text = bodyPart.GetDisplayName();
        }

        if (bodyPartSystem != null)
        {
            string sys = bodyPart.GetSystemName();
            string colorHex = GetSystemColorHex(sys);
            bodyPartSystem.text = $"<color={colorHex}>{(!string.IsNullOrWhiteSpace(sys) ? sys : defaultSystem)}</color>";
        }

        if (bodyPartType != null)
        {
            bodyPartType.text = !string.IsNullOrWhiteSpace(bodyPart.bodyPartTypeText) 
                ? bodyPart.bodyPartTypeText 
                : "Anatomical Structure";
        }

        if (bodyPartDescription != null)
        {
            bodyPartDescription.text = !string.IsNullOrWhiteSpace(bodyPart.description) 
                ? bodyPart.description 
                : defaultDescription;
        }

        if (bodyPartFunction != null)
        {
            bodyPartFunction.text = !string.IsNullOrWhiteSpace(bodyPart.function) 
                ? bodyPart.function 
                : "No specific physiological function documented in database.";
        }

        if (bodyPartLocation != null)
        {
            bodyPartLocation.text = !string.IsNullOrWhiteSpace(bodyPart.location) 
                ? bodyPart.location 
                : "No specific anatomical location details documented.";
        }

        // Display image/diagram
        if (mediaViewer != null)
        {
            mediaViewer.DisplayMedia(bodyPart);
        }
        else if (diagramImage != null)
        {
            if (bodyPart.diagramSprite != null)
            {
                diagramImage.sprite = bodyPart.diagramSprite;
                diagramImage.gameObject.SetActive(true);
            }
            else
            {
                diagramImage.gameObject.SetActive(false);
            }
        }

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    /// <summary>
    /// Calculates the optimal Right-Front VR target position and rotation.
    /// Body remains in center (0°).
    /// Inspector placed at +30° (midline/left) or +42° (right-sided anatomy), facing headset.
    /// </summary>
    public void CalculateTargetPose(BodyPartSelector bodyPart, out Vector3 targetPos, out Quaternion targetRot)
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = FindFirstObjectByType<Camera>();
            }
        }

        Vector3 headPos = (targetCamera != null) ? targetCamera.transform.position : Vector3.zero;

        // Determine baseline viewing direction from headset toward centered body model
        Vector3 baselineForward;
        if (anatomyModelRoot != null)
        {
            Vector3 toBody = anatomyModelRoot.position - headPos;
            toBody.y = 0f;
            baselineForward = (toBody.sqrMagnitude > 0.01f) ? toBody.normalized : Vector3.forward;
        }
        else if (targetCamera != null)
        {
            Vector3 camFwd = targetCamera.transform.forward;
            camFwd.y = 0f;
            baselineForward = (camFwd.sqrMagnitude > 0.01f) ? camFwd.normalized : Vector3.forward;
        }
        else
        {
            baselineForward = Vector3.forward;
        }

        // Adaptive angle: +42° for right-sided anatomy, +30° for midline/left
        bool isRightSided = IsRightSidedStructure(bodyPart);
        float horizontalAngle = isRightSided ? rightSideHorizontalAngle : normalHorizontalAngle;

        // Position: +angle to the RIGHT, at specified distance and height offset
        Vector3 rightFrontDir = Quaternion.AngleAxis(horizontalAngle, Vector3.up) * baselineForward;
        targetPos = headPos + rightFrontDir * distanceFromHeadset;
        targetPos.y = headPos.y + verticalOffset;

        // Rotation: inspector faces the user's headset
        Vector3 lookDir = targetPos - headPos;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
        }
        else
        {
            targetRot = Quaternion.identity;
        }
    }

    /// <summary>
    /// Determines whether the selected anatomical structure is on the right side of the body,
    /// evaluating both semantic naming metadata and 3D spatial position relative to the viewing axis.
    /// </summary>
    public bool IsRightSidedStructure(BodyPartSelector bodyPart)
    {
        if (bodyPart == null) return false;

        // 1. Semantic Check (Display name & GameObject name)
        string dName = bodyPart.GetDisplayName().ToLowerInvariant();
        string gName = bodyPart.gameObject.name.ToLowerInvariant();

        if (dName.Contains("(right)") || dName.Contains(" right") || dName.StartsWith("right ") ||
            gName.EndsWith(".r") || gName.EndsWith("_r") || gName.Contains(".right") || gName.Contains("_right") ||
            gName.StartsWith("right_") || gName.StartsWith("right "))
        {
            return true;
        }

        // 2. Spatial / Geometric Check relative to user-to-body viewing axis
        if (targetCamera != null)
        {
            Vector3 headPos = targetCamera.transform.position;
            Vector3 bodyPos = (anatomyModelRoot != null) ? anatomyModelRoot.position : headPos + targetCamera.transform.forward * 2f;
            Vector3 baseline = bodyPos - headPos;
            baseline.y = 0f;

            if (baseline.sqrMagnitude > 0.01f)
            {
                baseline.Normalize();
                Vector3 toPart = bodyPart.transform.position - headPos;
                toPart.y = 0f;
                // Signed angle around Y axis
                float signedAngle = Vector3.SignedAngle(baseline, toPart, Vector3.up);
                // If part is situated > 4 degrees to the right of the body center line
                if (signedAngle > 4f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Smoothly glides the inspector to the target pose with alpha fade and scale easing.
    /// Once the transition is complete, the inspector remains stable in world space.
    /// </summary>
    private System.Collections.IEnumerator AnimateToPose(Vector3 targetPos, Quaternion targetRot, bool isAppearing)
    {
        if (inspectorRoot == null) yield break;

        Vector3 startPos = inspectorRoot.position;
        Quaternion startRot = inspectorRoot.rotation;

        Vector3 startScale = isAppearing ? (canvasWorldScale * 0.88f) : inspectorRoot.localScale;
        Vector3 targetScale = canvasWorldScale;

        float startAlpha = isAppearing ? 0f : (canvasGroup != null ? canvasGroup.alpha : 1f);
        float targetAlpha = 1f;

        if (isAppearing)
        {
            // If appearing from inactive, start closer to target pose for a natural reveal
            if (Vector3.Distance(startPos, targetPos) > 1.5f)
            {
                startPos = targetPos - (targetRot * Vector3.forward * 0.08f);
                startRot = targetRot;
            }
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, transitionDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curveT = (transitionCurve != null) ? transitionCurve.Evaluate(t) : (1f - Mathf.Pow(1f - t, 3f));

            inspectorRoot.position = Vector3.Lerp(startPos, targetPos, curveT);
            inspectorRoot.rotation = Quaternion.Slerp(startRot, targetRot, curveT);
            inspectorRoot.localScale = Vector3.Lerp(startScale, targetScale, curveT);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, curveT);
            }

            yield return null;
        }

        // Lock final target pose (Rock-solid stability, zero subsequent frame drift)
        inspectorRoot.position = targetPos;
        inspectorRoot.rotation = targetRot;
        inspectorRoot.localScale = targetScale;
        if (canvasGroup != null) canvasGroup.alpha = targetAlpha;

        _transitionCoroutine = null;
    }

    /// <summary>
    /// Smoothly fades out and disables the inspector panel.
    /// </summary>
    private System.Collections.IEnumerator FadeOutAndHide()
    {
        if (canvasGroup != null)
        {
            float elapsed = 0f;
            float duration = 0.2f;
            float startAlpha = canvasGroup.alpha;
            Vector3 startScale = (inspectorRoot != null) ? inspectorRoot.localScale : canvasWorldScale;
            Vector3 endScale = canvasWorldScale * 0.9f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                if (inspectorRoot != null)
                {
                    inspectorRoot.localScale = Vector3.Lerp(startScale, endScale, t);
                }
                yield return null;
            }
            canvasGroup.alpha = 0f;
        }

        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }

        if (inspectorRoot != null && inspectorRoot != transform)
        {
            inspectorRoot.gameObject.SetActive(false);
        }

        _hideCoroutine = null;
    }

    /// <summary>
    /// Hides the Anatomy Inspector panel with a smooth fade-out.
    /// </summary>
    public void HidePanel()
    {
        if (_transitionCoroutine != null)
        {
            StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = null;
        }

        if (gameObject.activeInHierarchy)
        {
            if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
            _hideCoroutine = StartCoroutine(FadeOutAndHide());
        }
        else
        {
            if (infoPanel != null) infoPanel.SetActive(false);
            if (inspectorRoot != null) inspectorRoot.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Toggles the visibility of the Anatomy Inspector panel.
    /// </summary>
    public void TogglePanel()
    {
        bool isCurrentlyActive = (infoPanel != null && infoPanel.activeSelf) || (inspectorRoot != null && inspectorRoot.gameObject.activeSelf);
        if (isCurrentlyActive)
        {
            HidePanel();
        }
        else
        {
            if (_currentBodyPart != null)
            {
                DisplayBodyPart(_currentBodyPart);
            }
            else if (BodyPartSelector.CurrentlySelected != null)
            {
                DisplayBodyPart(BodyPartSelector.CurrentlySelected);
            }
            else
            {
                if (infoPanel != null) infoPanel.SetActive(true);
                if (inspectorRoot != null) inspectorRoot.gameObject.SetActive(true);
            }
        }
    }

    /// <summary>
    /// Hides the currently selected anatomical part to peel away layers and closes the inspector.
    /// </summary>
    public void HideCurrentPart()
    {
        if (_currentBodyPart != null)
        {
            _currentBodyPart.HideThisPart();
            HidePanel();
        }
    }

    /// <summary>
    /// Toggles 3D pulsation / inspection animation on the currently viewed organ.
    /// </summary>
    public void ToggleAnimation()
    {
        if (mediaViewer != null)
        {
            mediaViewer.ToggleInspectionAnimation();
        }
    }


    /// <summary>
    /// Automatically searches child components for TMP_Text elements matching common naming conventions.
    /// </summary>
    public void AutoFindTextReferences()
    {
        if (scrollRect == null)
        {
            scrollRect = GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
        }

        TMP_Text[] allTexts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text t in allTexts)
        {
            string lowerName = t.gameObject.name.ToLower();

            if (bodyPartName == null && (lowerName.Contains("name") || lowerName.Contains("title")))
            {
                bodyPartName = t;
            }
            else if (bodyPartDescription == null && (lowerName.Contains("desc") || lowerName.Contains("info") || lowerName.Contains("detail")))
            {
                bodyPartDescription = t;
            }
            else if (bodyPartSystem == null && lowerName.Contains("system"))
            {
                bodyPartSystem = t;
            }
            else if (bodyPartType == null && lowerName.Contains("type"))
            {
                bodyPartType = t;
            }
            else if (bodyPartFunction == null && lowerName.Contains("func"))
            {
                bodyPartFunction = t;
            }
            else if (bodyPartLocation == null && lowerName.Contains("loc"))
            {
                bodyPartLocation = t;
            }
        }
    }

    /// <summary>
    /// Returns the hex color code for the given anatomical organ system.
    /// </summary>
    public static string GetSystemColorHex(string system)
    {
        if (string.IsNullOrEmpty(system)) return "#94A3B8";
        string s = system.ToLowerInvariant();
        if (s.Contains("skelet") || s.Contains("bone")) return "#F59E0B"; // Amber / Gold
        if (s.Contains("muscul") || s.Contains("muscle")) return "#EF4444"; // Crimson Red
        if (s.Contains("cardio") || s.Contains("circulat") || s.Contains("arter") || s.Contains("vein")) return "#3B82F6"; // Blue
        if (s.Contains("nerv") || s.Contains("brain")) return "#06B6D4"; // Cyan
        if (s.Contains("viscer") || s.Contains("organ")) return "#10B981"; // Emerald
        if (s.Contains("joint") || s.Contains("articul")) return "#8B5CF6"; // Purple
        if (s.Contains("lymph")) return "#F97316"; // Orange
        return "#38BDF8"; // Sky Blue
    }

    private void Reset()
    {
        infoPanel = gameObject;
        AutoFindTextReferences();
    }

#if UNITY_EDITOR
    [ContextMenu("Align Inspector to Right-Front Layout")]
    public void AlignInspectorInEditor()
    {
        EnsureReferences();
        CalculateTargetPose(null, out Vector3 targetPos, out Quaternion targetRot);
        if (inspectorRoot != null)
        {
            UnityEditor.Undo.RecordObject(inspectorRoot, "Align Inspector Right-Front");
            inspectorRoot.position = targetPos;
            inspectorRoot.rotation = targetRot;
            inspectorRoot.localScale = canvasWorldScale;

            if (infoPanel != null && infoPanel.transform != inspectorRoot)
            {
                RectTransform rt = infoPanel.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = Vector2.zero;
                    rt.localRotation = Quaternion.identity;
                }
            }

            UnityEditor.EditorUtility.SetDirty(inspectorRoot);
        }
        Debug.Log($"[BodyPartInfoUI] Aligned Anatomy Inspector to Right-Front ({normalHorizontalAngle}°) at {targetPos}.");
    }
#endif
}
