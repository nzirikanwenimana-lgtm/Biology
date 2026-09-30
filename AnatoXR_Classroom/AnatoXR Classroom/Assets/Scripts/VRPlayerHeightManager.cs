using UnityEngine;
using Unity.XR.CoreUtils;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Automatically manages and calibrates player height for Meta Quest 3S and Desktop.
/// Fixes the issue where the student spawns too short (e.g. at floor level or toddler height).
/// Ensures player eye level is comfortably set to realistic adult standing height (~1.65m - 1.70m).
/// </summary>
[DisallowMultipleComponent]
public class VRPlayerHeightManager : MonoBehaviour
{
    public static VRPlayerHeightManager Instance { get; private set; }

    [Header("XR Rig References")]
    [Tooltip("Reference to the XROrigin in the scene. Auto-detected if unassigned.")]
    public XROrigin xrOrigin;

    [Tooltip("The Camera Floor Offset GameObject under XROrigin. Auto-detected if unassigned.")]
    public Transform cameraFloorOffset;

    [Tooltip("Main VR Camera. Auto-detected if unassigned.")]
    public Camera vrCamera;

    [Header("Target Height Settings")]
    [Tooltip("Target eye height in meters from the classroom floor for a standing student (1.65m = ~5.4 ft).")]
    public float targetStandingEyeHeight = 1.68f;

    [Tooltip("Minimum allowed eye height before height compensation kicks in.")]
    public float minEyeHeightThreshold = 1.35f;

    [Tooltip("Enable automatic height compensation on scene start if the student is seated or too low.")]
    public bool autoCompensateOnStart = true;

    [Header("Manual Adjustment")]
    [Tooltip("Height change increment per button press (meters).")]
    public float heightStep = 0.1f;

    private float _manualOffset = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        FindRigReferences();
    }

    private void Start()
    {
        FindRigReferences();

        if (xrOrigin != null)
        {
            // Ensure default camera Y offset is adult height (1.68m) instead of toddler height (1.11m)
            xrOrigin.CameraYOffset = targetStandingEyeHeight;
        }

        if (autoCompensateOnStart)
        {
            // Delay slightly to allow Quest tracking to establish guardian floor
            Invoke(nameof(CalibrateStandingHeight), 0.5f);
        }
    }

    private void Update()
    {
        // Desktop testing shortcut: PageUp / PageDown to raise or lower height
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.pageUpKey.wasPressedThisFrame || (kb.leftShiftKey.isPressed && kb.upArrowKey.wasPressedThisFrame))
                AdjustHeight(heightStep);
            if (kb.pageDownKey.wasPressedThisFrame || (kb.leftShiftKey.isPressed && kb.downArrowKey.wasPressedThisFrame))
                AdjustHeight(-heightStep);
        }
#else
        if (Input.GetKeyDown(KeyCode.PageUp)) AdjustHeight(heightStep);
        if (Input.GetKeyDown(KeyCode.PageDown)) AdjustHeight(-heightStep);
#endif
    }

    private void FindRigReferences()
    {
        if (xrOrigin == null)
            xrOrigin = FindFirstObjectByType<XROrigin>();

        if (xrOrigin != null)
        {
            if (cameraFloorOffset == null && xrOrigin.CameraFloorOffsetObject != null)
                cameraFloorOffset = xrOrigin.CameraFloorOffsetObject.transform;

            if (vrCamera == null && xrOrigin.Camera != null)
                vrCamera = xrOrigin.Camera;
        }

        if (vrCamera == null)
            vrCamera = Camera.main;

        if (cameraFloorOffset == null && vrCamera != null && vrCamera.transform.parent != null)
            cameraFloorOffset = vrCamera.transform.parent;
    }

    /// <summary>
    /// Calibrates the player's view so their eyes are at targetStandingEyeHeight above the room floor.
    /// Works whether the student is standing or sitting.
    /// </summary>
    [ContextMenu("Calibrate Standing Height")]
    public void CalibrateStandingHeight()
    {
        FindRigReferences();
        if (vrCamera == null) return;

        // Current world Y of camera vs Origin base
        float originBaseY = (xrOrigin != null) ? xrOrigin.transform.position.y : 0f;
        float currentEyeY = vrCamera.transform.position.y;
        float currentEyeHeightAboveOrigin = currentEyeY - originBaseY;

        // If camera eye height is too low (e.g. seated or guardian floor issue), elevate the camera offset
        if (currentEyeHeightAboveOrigin < minEyeHeightThreshold || currentEyeHeightAboveOrigin > 2.2f)
        {
            float neededBoost = targetStandingEyeHeight - currentEyeHeightAboveOrigin;
            SetHeightOffset(_manualOffset + neededBoost);
            Debug.Log($"[VRPlayerHeightManager] Compensated player eye height: added +{neededBoost:F2}m (new eye height: {vrCamera.transform.position.y:F2}m)");
        }
        else
        {
            Debug.Log($"[VRPlayerHeightManager] Player eye height is comfortable at {currentEyeHeightAboveOrigin:F2}m.");
        }
    }

    /// <summary>
    /// Adjusts height by a delta (e.g. +0.1m or -0.1m).
    /// </summary>
    public void AdjustHeight(float delta)
    {
        SetHeightOffset(_manualOffset + delta);
    }

    /// <summary>
    /// Resets manual height offset back to 0.
    /// </summary>
    public void ResetHeight()
    {
        SetHeightOffset(0f);
    }

    private void SetHeightOffset(float newOffset)
    {
        FindRigReferences();
        _manualOffset = Mathf.Clamp(newOffset, -0.8f, 1.2f);

        if (cameraFloorOffset != null)
        {
            Vector3 localPos = cameraFloorOffset.localPosition;
            localPos.y = _manualOffset;
            cameraFloorOffset.localPosition = localPos;
        }
        else if (xrOrigin != null)
        {
            xrOrigin.CameraYOffset = targetStandingEyeHeight + _manualOffset;
        }
    }
}
