using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gaze and Palm Affordance System for the AnatomyVR Dashboard.
/// - Anchors and follows the user's Left Hand Model.
/// - Dynamically detects when the student's left palm is turned towards their head (eyes)
///   and whether the student is looking towards the hand (gaze alignment).
/// - Smoothly fades in / scales up when looking at palm, and fades out when looking away.
/// - Enables dual XR interaction (Raycast + Poke) when visible, and disables raycasts when hidden.
/// - Provides Pinning capability to lock the dashboard in place or keep it open.
/// - Supports desktop Play Mode testing shortcuts ([H] toggle affordance, [P] pin).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class AnatomyDashboardHandAffordance : MonoBehaviour
{
    public static AnatomyDashboardHandAffordance Instance { get; private set; }

    public enum PalmAxisOption
    {
        Auto,       // Automatically detects Left Hand Model (-Y) vs Left Hand Controller (+Y)
        Down,       // -Y (Standard for Oculus / Meta Quest Left Hand Model prefab)
        Up,         // +Y (Standard for XR controller tracked pose driver)
        Forward,    // +Z
        Back,       // -Z
        Right,      // +X
        Left,       // -X
        Custom
    }

    [Header("Anchoring & Positioning")]
    [Tooltip("Left Hand Model transform to follow. If null, automatically located in scene.")]
    public Transform handAnchor;

    [Tooltip("Local offset relative to hand anchor.")]
    public Vector3 localPositionOffset = new Vector3(0f, -0.06f, 0.08f);

    [Tooltip("Local rotation offset angled toward the user's eyes when holding palm up.")]
    public Vector3 localRotationOffset = new Vector3(-65f, 180f, 0f);

    [Tooltip("Scale multiplier for the dashboard.")]
    public float targetScale = 1.0f;

    [Header("Palm Alignment Check")]
    [Tooltip("Which axis of the hand transform represents the palm normal (facing out of the palm).")]
    public PalmAxisOption palmAxis = PalmAxisOption.Auto;

    [Tooltip("Custom local palm vector if PalmAxisOption is set to Custom.")]
    public Vector3 customPalmNormal = Vector3.down;

    [Tooltip("Dot product threshold to open UI. 0.35 ≈ within 70 degrees of pointing towards head.")]
    [Range(0.1f, 0.9f)]
    public float palmOpenThreshold = 0.35f;

    [Tooltip("Dot product threshold to close UI (hysteresis buffer to prevent edge-flicker).")]
    [Range(0.0f, 0.8f)]
    public float palmCloseThreshold = 0.15f;

    [Header("Gaze Alignment Check")]
    [Tooltip("Require user to be looking in the direction of the hand to open the UI.")]
    public bool requireGaze = true;

    [Tooltip("Dot product threshold between head forward and hand direction. 0.40 ≈ within 66 degrees FOV.")]
    [Range(0.1f, 0.9f)]
    public float gazeOpenThreshold = 0.40f;

    [Tooltip("Dot product threshold to close UI if looking away (hysteresis buffer).")]
    [Range(0.0f, 0.8f)]
    public float gazeCloseThreshold = 0.20f;

    [Header("Affordance Transitions")]
    [Tooltip("Fade speed for smooth alpha and scale transitions.")]
    public float transitionSpeed = 9f;

    [Tooltip("Initial scale ratio when closed (scales up smoothly from this value to 1.0).")]
    public float closedScaleRatio = 0.82f;

    [Tooltip("Target scale for the WorldSpace Canvas (default ~0.00048 for ~0.6m dashboard in VR).")]
    public float canvasWorldScale = 0.00048f;

    [Header("Live Affordance Status (Read-Only)")]
    [Tooltip("Current dot product between hand palm vector and direction to head.")]
    public float currentPalmDot;
    [Tooltip("Current dot product between head camera forward and direction to hand.")]
    public float currentGazeDot;
    public bool isPalmAligned;
    public bool isGazeAligned;
    public bool isAffordanceMet;

    [Header("Pinning & Overrides")]
    [Tooltip("When pinned, the dashboard remains visible and stops auto-fading.")]
    public bool isPinned = false;

    [Tooltip("Force dashboard to remain always visible (useful for debugging).")]
    public bool forceAlwaysVisible = false;

    [Header("Audio Feedback")]
    public AudioClip openSound;
    public AudioClip closeSound;
    public AudioSource audioSource;

    [Header("UI References")]
    public CanvasGroup canvasGroup;
    public Button pinButton;
    public Image pinIcon;

    // Runtime state
    private Camera _headCamera;
    private bool _isOpen = false;
    private float _currentAlpha = 1f;
    private float _openDebounceTimer = 0f;
    private float _closeDebounceTimer = 0f;
    private const float DebounceDuration = 0.10f;
    private Transform _canvasRootTransform;
    private bool _isParentedToHand = false;

    public bool IsOpen => _isOpen;
    public float CurrentAlpha => _currentAlpha;
    public bool IsParentedToHand => _isParentedToHand;

    private void Awake()
    {
        Instance = this;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _headCamera = Camera.main;

        // Determine if this object or its parent canvas is the anchor
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        _canvasRootTransform = (parentCanvas != null) ? parentCanvas.transform : transform;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f; // 3D audio in VR
            }
        }

        if (pinButton != null)
        {
            pinButton.onClick.AddListener(TogglePin);
        }

        AutoLocateLeftHandAnchor();
    }

    private void Start()
    {
        // Initial setup
        if (forceAlwaysVisible || isPinned)
        {
            _isOpen = true;
            _currentAlpha = 1f;
            ApplyVisibilityState(1f);
        }
        else
        {
            // Start closed for palm affordance
            _isOpen = false;
            _currentAlpha = 0f;
            ApplyVisibilityState(0f);
        }

        UpdatePinVisual();
    }

    private void Update()
    {
        // 1. Maintain anchor parent or follow
        EnsureHandParenting();

        // 2. Desktop debug shortcuts
        HandleDesktopTestingShortcuts();

        // 3. Evaluate Gaze & Palm Affordance
        EvaluateAffordanceState();

        // 4. Smooth Animation
        AnimateTransition();
    }

    [ContextMenu("Apply Preset: Reference Palm Float (Screenshot Match)")]
    public void ApplyPresetReferencePalm()
    {
        bool isLeftHandRoot = (handAnchor != null && (handAnchor.name.Equals("Left Hand", StringComparison.OrdinalIgnoreCase) || handAnchor.CompareTag("Left Hand")));
        if (isLeftHandRoot)
        {
            localPositionOffset = new Vector3(0.03f, 0.09f, 0.08f);
            localRotationOffset = new Vector3(38f, 0f, 0f);
            canvasWorldScale = 0.00038f;
        }
        else
        {
            // For Left Hand Model (which has 90 deg Z roll)
            localPositionOffset = new Vector3(0.09f, -0.03f, 0.08f);
            localRotationOffset = new Vector3(-65f, 180f, 0f);
            canvasWorldScale = 0.00038f;
        }
        ApplyTransform();
    }

    [ContextMenu("Apply Preset: Forearm / Wrist Tablet")]
    public void ApplyPresetWristTablet()
    {
        bool isLeftHandRoot = (handAnchor != null && (handAnchor.name.Equals("Left Hand", StringComparison.OrdinalIgnoreCase) || handAnchor.CompareTag("Left Hand")));
        if (isLeftHandRoot)
        {
            localPositionOffset = new Vector3(0.02f, 0.04f, 0.03f);
            localRotationOffset = new Vector3(25f, 0f, 0f);
            canvasWorldScale = 0.00034f;
        }
        else
        {
            localPositionOffset = new Vector3(0.04f, -0.02f, 0.03f);
            localRotationOffset = new Vector3(-75f, 180f, 0f);
            canvasWorldScale = 0.00034f;
        }
        ApplyTransform();
    }

    [ContextMenu("Apply Preset: Forward Reach (Comfort Poke)")]
    public void ApplyPresetComfortPoke()
    {
        bool isLeftHandRoot = (handAnchor != null && (handAnchor.name.Equals("Left Hand", StringComparison.OrdinalIgnoreCase) || handAnchor.CompareTag("Left Hand")));
        if (isLeftHandRoot)
        {
            localPositionOffset = new Vector3(0.04f, 0.12f, 0.12f);
            localRotationOffset = new Vector3(45f, 0f, 0f);
            canvasWorldScale = 0.00040f;
        }
        else
        {
            localPositionOffset = new Vector3(0.12f, -0.04f, 0.12f);
            localRotationOffset = new Vector3(-55f, 180f, 0f);
            canvasWorldScale = 0.00040f;
        }
        ApplyTransform();
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        ApplyTransform();
    }

    public void ApplyTransform()
    {
        if (_canvasRootTransform == null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            _canvasRootTransform = (parentCanvas != null) ? parentCanvas.transform : transform;
        }

        if (_canvasRootTransform != null && handAnchor != null)
        {
            if (_canvasRootTransform.parent != handAnchor && !isPinned)
            {
                _canvasRootTransform.SetParent(handAnchor, false);
            }
            _canvasRootTransform.localPosition = localPositionOffset;
            _canvasRootTransform.localRotation = Quaternion.Euler(localRotationOffset);
            _canvasRootTransform.localScale = Vector3.one * canvasWorldScale;
        }
    }

    /// <summary>
    /// Searches for Left Hand Model or Left Hand in the scene hierarchy and binds anchor.
    /// </summary>
    public void AutoLocateLeftHandAnchor()
    {
        if (handAnchor != null) return;

        var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        // Priority 1: Tracked Hand Root (GameObject named "Left Hand" or tagged "Left Hand" with TrackedPoseDriver)
        foreach (var t in allTransforms)
        {
            if (t.CompareTag("Left Hand") || t.name.Equals("Left Hand", StringComparison.OrdinalIgnoreCase))
            {
                handAnchor = t;
                Debug.Log($"[AnatomyDashboardHandAffordance] Anchored to Left Hand Tracked Root: '{t.name}'");
                return;
            }
        }

        // Priority 2: Left Hand Model prefab instance (Oculus Hands)
        foreach (var t in allTransforms)
        {
            string lower = t.name.ToLower();
            if (lower.Contains("left") && lower.Contains("hand") && lower.Contains("model") && !lower.Contains("canvas") && !lower.Contains("dashboard"))
            {
                handAnchor = t;
                Debug.Log($"[AnatomyDashboardHandAffordance] Anchored to Left Hand Model: '{t.name}'");
                return;
            }
        }

        // Priority 3: Left hand wrist bone (hands:b_l_hand)
        foreach (var t in allTransforms)
        {
            if (t.name == "hands:b_l_hand")
            {
                handAnchor = t;
                Debug.Log($"[AnatomyDashboardHandAffordance] Anchored to Left Hand Bone: '{t.name}'");
                return;
            }
        }
    }

    /// <summary>
    /// Ensures the canvas is physically parented to the Left Hand with user-tuned local offsets.
    /// </summary>
    private void EnsureHandParenting()
    {
        if (isPinned) return;

        if (handAnchor == null)
        {
            AutoLocateLeftHandAnchor();
            if (handAnchor == null) return;
        }

        if (_canvasRootTransform == null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            _canvasRootTransform = (parentCanvas != null) ? parentCanvas.transform : transform;
        }

        if (_canvasRootTransform.parent != handAnchor)
        {
            _canvasRootTransform.SetParent(handAnchor, false);
        }

        // Faithful application of user-defined local offsets without arbitrary overrides
        _canvasRootTransform.localPosition = localPositionOffset;
        _canvasRootTransform.localRotation = Quaternion.Euler(localRotationOffset);
        _canvasRootTransform.localScale = Vector3.one * canvasWorldScale;
        _isParentedToHand = true;
    }

    /// <summary>
    /// Calculates palm direction and gaze angle to trigger smooth open/close.
    /// </summary>
    private void EvaluateAffordanceState()
    {
        if (forceAlwaysVisible || isPinned)
        {
            _isOpen = true;
            isAffordanceMet = true;
            return;
        }

        if (handAnchor == null)
        {
            _isOpen = true;
            return;
        }

        if (_headCamera == null)
        {
            _headCamera = Camera.main;
            if (_headCamera == null)
            {
                _isOpen = true;
                return;
            }
        }

        // 1. Palm-facing-head check
        Vector3 palmNormalWorld = GetPalmNormalWorld();
        Vector3 handPos = handAnchor.position;
        Vector3 headPos = _headCamera.transform.position;
        Vector3 dirToHead = (headPos - handPos).normalized;

        float palmDot = Vector3.Dot(palmNormalWorld, dirToHead);

        // 2. Gaze check (head facing toward hand)
        float gazeDot = 1f;
        if (requireGaze)
        {
            Vector3 headForward = _headCamera.transform.forward;
            Vector3 dirToHand = (handPos - headPos).normalized;
            gazeDot = Vector3.Dot(headForward, dirToHand);
        }

        // Update live Inspector variables
        currentPalmDot = palmDot;
        currentGazeDot = gazeDot;
        isPalmAligned = palmDot >= (_isOpen ? palmCloseThreshold : palmOpenThreshold);
        isGazeAligned = !requireGaze || gazeDot >= (_isOpen ? gazeCloseThreshold : gazeOpenThreshold);
        isAffordanceMet = isPalmAligned && isGazeAligned;

        // 3. Evaluate with Hysteresis & Debouncing
        bool conditionsMet = false;
        if (!_isOpen)
        {
            // Open when both thresholds are exceeded
            conditionsMet = (palmDot >= palmOpenThreshold) && (!requireGaze || gazeDot >= gazeOpenThreshold);
            if (conditionsMet)
            {
                _openDebounceTimer += Time.deltaTime;
                if (_openDebounceTimer >= DebounceDuration)
                {
                    SetOpenState(true);
                    _openDebounceTimer = 0f;
                }
            }
            else
            {
                _openDebounceTimer = 0f;
            }
        }
        else
        {
            // Close when either threshold drops below close threshold
            bool shouldClose = (palmDot < palmCloseThreshold) || (requireGaze && gazeDot < gazeCloseThreshold);
            if (shouldClose)
            {
                _closeDebounceTimer += Time.deltaTime;
                if (_closeDebounceTimer >= DebounceDuration)
                {
                    SetOpenState(false);
                    _closeDebounceTimer = 0f;
                }
            }
            else
            {
                _closeDebounceTimer = 0f;
            }
        }
    }

    /// <summary>
    /// Smoothly animates canvasGroup alpha and local scale.
    /// </summary>
    private void AnimateTransition()
    {
        float targetAlpha = _isOpen ? 1f : 0f;
        _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, Time.deltaTime * transitionSpeed);

        ApplyVisibilityState(_currentAlpha);
    }

    private void ApplyVisibilityState(float alpha)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
            // Only accept raycasts and poke interaction when mostly visible
            bool interactable = (alpha > 0.5f);
            canvasGroup.interactable = interactable;
            canvasGroup.blocksRaycasts = interactable;
        }

        // Smooth scale affordance
        float scaleFactor = Mathf.Lerp(closedScaleRatio, 1f, alpha) * targetScale;
        if (_canvasRootTransform == transform)
        {
            transform.localScale = Vector3.one * (scaleFactor * canvasWorldScale);
        }
        else
        {
            transform.localScale = Vector3.one * scaleFactor;
            _canvasRootTransform.localScale = Vector3.one * canvasWorldScale;
        }

        // When completely faded out, disable canvas rendering to optimize GPU draw calls
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvas.enabled = (alpha > 0.01f || _isOpen);
        }
    }

    private void SetOpenState(bool open)
    {
        if (_isOpen == open) return;
        _isOpen = open;

        if (audioSource != null)
        {
            AudioClip clip = _isOpen ? openSound : closeSound;
            if (clip != null) audioSource.PlayOneShot(clip);
        }
    }

    public Vector3 GetPalmNormalWorld()
    {
        if (handAnchor == null) return Vector3.up;

        Vector3 localDir;
        if (palmAxis == PalmAxisOption.Auto)
        {
            // If anchored to Left Hand Model (Oculus Hands prefab has ~170° roll around Z),
            // the vector pointing out of the palm towards the eyes is Vector3.down (-Y).
            // If anchored to Left Hand controller (unrotated), it is Vector3.up (+Y).
            bool isHandModel = handAnchor.name.ToLower().Contains("model") || 
                               handAnchor.localRotation.eulerAngles.z > 90f;
            localDir = isHandModel ? Vector3.down : Vector3.up;
        }
        else
        {
            switch (palmAxis)
            {
                case PalmAxisOption.Down:    localDir = Vector3.down; break;
                case PalmAxisOption.Up:      localDir = Vector3.up; break;
                case PalmAxisOption.Forward: localDir = Vector3.forward; break;
                case PalmAxisOption.Back:    localDir = Vector3.back; break;
                case PalmAxisOption.Right:   localDir = Vector3.right; break;
                case PalmAxisOption.Left:    localDir = Vector3.left; break;
                case PalmAxisOption.Custom:  localDir = customPalmNormal; break;
                default:                     localDir = Vector3.down; break;
            }
        }

        return handAnchor.TransformDirection(localDir);
    }

    public void TogglePin()
    {
        SetPinned(!isPinned);
    }

    public void SetPinned(bool pin)
    {
        isPinned = pin;
        if (isPinned)
        {
            _isOpen = true;
            // Detach from hand to float in world space if desired
            _canvasRootTransform.SetParent(null, true);
            _isParentedToHand = false;
        }
        else
        {
            // Re-parent to hand
            _isParentedToHand = false;
            EnsureHandParenting();
        }

        UpdatePinVisual();
    }

    private void UpdatePinVisual()
    {
        if (pinIcon != null)
        {
            pinIcon.color = isPinned ? new Color(0.18f, 0.76f, 0.98f, 1f) : new Color(0.6f, 0.75f, 0.9f, 0.8f);
        }
    }

    private void HandleDesktopTestingShortcuts()
    {
#if ENABLE_INPUT_SYSTEM
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.hKey.wasPressedThisFrame)
        {
            forceAlwaysVisible = !forceAlwaysVisible;
            if (forceAlwaysVisible) _isOpen = true;
            Debug.Log($"[AnatomyDashboardHandAffordance] Force Always Visible: {forceAlwaysVisible}");
        }

        if (keyboard.pKey.wasPressedThisFrame)
        {
            TogglePin();
            Debug.Log($"[AnatomyDashboardHandAffordance] Pinned: {isPinned}");
        }
#else
        if (Input.GetKeyDown(KeyCode.H))
        {
            forceAlwaysVisible = !forceAlwaysVisible;
            if (forceAlwaysVisible) _isOpen = true;
            Debug.Log($"[AnatomyDashboardHandAffordance] Force Always Visible: {forceAlwaysVisible}");
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            TogglePin();
            Debug.Log($"[AnatomyDashboardHandAffordance] Pinned: {isPinned}");
        }
#endif
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (handAnchor == null) return;

        // Draw palm normal ray in Scene view for easy calibration
        Gizmos.color = Color.cyan;
        Vector3 palmNorm = GetPalmNormalWorld();
        Gizmos.DrawRay(handAnchor.position, palmNorm * 0.25f);
        Gizmos.DrawWireSphere(handAnchor.position + palmNorm * 0.25f, 0.02f);

        if (_headCamera != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(handAnchor.position, _headCamera.transform.position);
        }
    }
#endif
}
