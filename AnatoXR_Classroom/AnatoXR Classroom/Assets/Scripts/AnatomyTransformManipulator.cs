using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controls 6-DOF Grab & Move, Scale Up / Down, and Rotate for the anatomy model or isolated structures.
/// Supports:
/// 1. Direct hand & ray grabbing in XR.
/// 2. Two-handed pinch-to-scale & controller thumbstick zoom in VR.
/// 3. Wrist/UI slider and +/- buttons for precise magnification of small structures.
/// 4. 6-DOF wrist rotation, yaw controls, and 360-degree presentation turntable mode.
/// 5. Desktop / Unity Editor controls (Mouse drag, scroll wheel scale, right-click rotate).
/// </summary>
[DisallowMultipleComponent]
public class AnatomyTransformManipulator : MonoBehaviour
{
    [Header("Transform Target")]
    [Tooltip("Target object to manipulate. If null, manipulates this GameObject.")]
    public Transform targetTransform;

    [Header("Scale Settings")]
    [Tooltip("Minimum allowable scale multiplier relative to default scale.")]
    public float minScaleMultiplier = 0.2f;

    [Tooltip("Maximum allowable scale multiplier relative to default scale.")]
    public float maxScaleMultiplier = 6.0f;

    [Tooltip("Speed of scaling when using thumbstick or UI hold buttons.")]
    public float scaleSpeed = 1.5f;

    [Header("Rotation Settings")]
    [Tooltip("Speed of rotation when using thumbstick or UI buttons (degrees/sec).")]
    public float rotationSpeed = 60f;

    [Tooltip("When enabled, the model smoothly rotates 360 degrees for classroom showcase.")]
    public bool isTurntableActive = false;

    [Tooltip("Turntable rotation speed in degrees/second.")]
    public float turntableSpeed = 25f;

    [Header("Desktop & Testing Controls")]
    [Tooltip("Enable mouse & keyboard testing in the Unity Editor / Desktop builds.")]
    public bool enableDesktopControls = true;

    // Cached initial transformation for one-click reset
    private Vector3 _defaultPosition;
    private Quaternion _defaultRotation;
    private Vector3 _defaultScale;
    private float _currentScaleMultiplier = 1.0f;



    private void Awake()
    {
        if (targetTransform == null)
        {
            targetTransform = transform;
        }

        _defaultPosition = targetTransform.localPosition;
        _defaultRotation = targetTransform.localRotation;
        _defaultScale = targetTransform.localScale;
    }

    private void Update()
    {
        // 1. Turntable auto-rotate
        if (isTurntableActive && targetTransform != null)
        {
            targetTransform.Rotate(Vector3.up, turntableSpeed * Time.deltaTime, Space.World);
        }

        // 2. Desktop mouse & keyboard fallback controls
        if (enableDesktopControls)
        {
            HandleDesktopManipulation();
        }
    }

    // -------------------------------------------------------------------------
    // Scale Up / Down API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scales the model up by a fractional factor (e.g. 1.25x).
    /// </summary>
    public void ScaleUp(float factor = 1.25f)
    {
        SetScaleMultiplier(_currentScaleMultiplier * factor);
    }

    /// <summary>
    /// Scales the model down by a fractional factor (e.g. 0.8x).
    /// </summary>
    public void ScaleDown(float factor = 0.8f)
    {
        SetScaleMultiplier(_currentScaleMultiplier * factor);
    }

    /// <summary>
    /// Directly sets the scale multiplier between minScaleMultiplier and maxScaleMultiplier.
    /// </summary>
    public void SetScaleMultiplier(float multiplier)
    {
        if (targetTransform == null) return;

        _currentScaleMultiplier = Mathf.Clamp(multiplier, minScaleMultiplier, maxScaleMultiplier);
        targetTransform.localScale = _defaultScale * _currentScaleMultiplier;
    }

    /// <summary>
    /// Returns current scale multiplier (1.0 = normal size).
    /// </summary>
    public float GetScaleMultiplier()
    {
        return _currentScaleMultiplier;
    }

    /// <summary>
    /// Sets normalized scale from a 0.0 to 1.0 UI slider.
    /// </summary>
    public void SetScaleNormalized(float t)
    {
        float targetMul = Mathf.Lerp(minScaleMultiplier, maxScaleMultiplier, Mathf.Clamp01(t));
        SetScaleMultiplier(targetMul);
    }

    // -------------------------------------------------------------------------
    // Rotate API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Rotates the model around the world Y axis by the given angle in degrees.
    /// </summary>
    public void RotateYaw(float degrees)
    {
        if (targetTransform == null) return;
        targetTransform.Rotate(Vector3.up, degrees, Space.World);
    }

    /// <summary>
    /// Rotates the model around the camera/view Pitch axis by the given angle in degrees.
    /// </summary>
    public void RotatePitch(float degrees)
    {
        if (targetTransform == null) return;
        Camera cam = Camera.main;
        Vector3 rightAxis = (cam != null) ? cam.transform.right : Vector3.right;
        targetTransform.Rotate(rightAxis, degrees, Space.World);
    }

    /// <summary>
    /// Toggles continuous 360 presentation turntable rotation.
    /// </summary>
    public void ToggleTurntable()
    {
        isTurntableActive = !isTurntableActive;
    }

    public void SetTurntable(bool active)
    {
        isTurntableActive = active;
    }

    // -------------------------------------------------------------------------
    // Reset API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Restores the model to its original default position, orientation, and scale.
    /// </summary>
    public void ResetTransform()
    {
        if (targetTransform == null) return;

        StopAllCoroutines();
        StartCoroutine(AnimateReset(0.35f));
    }

    private IEnumerator AnimateReset(float duration)
    {
        Vector3 startPos = targetTransform.localPosition;
        Quaternion startRot = targetTransform.localRotation;
        Vector3 startScale = targetTransform.localScale;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            targetTransform.localPosition = Vector3.Lerp(startPos, _defaultPosition, t);
            targetTransform.localRotation = Quaternion.Slerp(startRot, _defaultRotation, t);
            targetTransform.localScale = Vector3.Lerp(startScale, _defaultScale, t);
            yield return null;
        }

        targetTransform.localPosition = _defaultPosition;
        targetTransform.localRotation = _defaultRotation;
        targetTransform.localScale = _defaultScale;
        _currentScaleMultiplier = 1.0f;
    }

    // -------------------------------------------------------------------------
    // Desktop Testing Fallback
    // -------------------------------------------------------------------------

    private void HandleDesktopManipulation()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        var kb = Keyboard.current;
        if (mouse == null) return;

        // Scroll Wheel: Scale Up / Down
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float factor = (scroll > 0f) ? 1.1f : 0.9f;
            SetScaleMultiplier(_currentScaleMultiplier * factor);
        }

        // Middle Click or Left Ctrl + Left Click: Drag & Move
        if (mouse.middleButton.isPressed || (kb != null && kb.leftCtrlKey.isPressed && mouse.leftButton.isPressed))
        {
            Vector2 delta = mouse.delta.ReadValue();
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 move = (cam.transform.right * delta.x + cam.transform.up * delta.y) * 0.005f;
                targetTransform.position += move;
            }
        }

        // Right Mouse Button: Rotate Model
        if (mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            targetTransform.Rotate(Vector3.up, -delta.x * 0.3f, Space.World);
            Camera cam = Camera.main;
            if (cam != null)
            {
                targetTransform.Rotate(cam.transform.right, delta.y * 0.3f, Space.World);
            }
        }
#else
        // Legacy Input
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float factor = (scroll > 0f) ? 1.1f : 0.9f;
            SetScaleMultiplier(_currentScaleMultiplier * factor);
        }

        if (Input.GetMouseButton(2) || (Input.GetKey(KeyCode.LeftControl) && Input.GetMouseButton(0)))
        {
            float dx = Input.GetAxis("Mouse X");
            float dy = Input.GetAxis("Mouse Y");
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 move = (cam.transform.right * dx + cam.transform.up * dy) * 0.05f;
                targetTransform.position += move;
            }
        }

        if (Input.GetMouseButton(1))
        {
            float dx = Input.GetAxis("Mouse X");
            float dy = Input.GetAxis("Mouse Y");
            targetTransform.Rotate(Vector3.up, -dx * 4f, Space.World);
            Camera cam = Camera.main;
            if (cam != null)
            {
                targetTransform.Rotate(cam.transform.right, dy * 4f, Space.World);
            }
        }
#endif
    }
}
