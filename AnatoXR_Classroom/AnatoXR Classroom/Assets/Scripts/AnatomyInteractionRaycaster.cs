using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Dual VR and Desktop point-and-click anatomy raycaster.
/// Enables seamless point-and-click selection of anatomy meshes via:
/// 1. Mouse click & hover in Unity Editor / Desktop (New Input System & Legacy compatible)
/// 2. VR Ray Interactors in XR
/// </summary>
public class AnatomyInteractionRaycaster : MonoBehaviour
{
    [Header("Raycast Settings")]
    [Tooltip("Camera used for mouse raycasting (defaults to Camera.main).")]
    public Camera targetCamera;

    [Tooltip("Maximum distance to raycast for anatomy selection.")]
    public float maxDistance = 50f;

    [Tooltip("Layer mask for anatomy meshes. Defaults to Everything.")]
    public LayerMask anatomyLayerMask = ~0;

    [Header("Audio Feedback (Optional)")]
    [Tooltip("Audio clip played when an anatomy mesh is clicked/selected.")]
    public AudioClip selectionSound;

    [Tooltip("AudioSource used to play the selection sound.")]
    public AudioSource audioSource;

    private BodyPartSelector _currentHovered;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null && selectionSound != null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }
    }

    private void Update()
    {
        HandleMousePointerInteraction();
    }

    /// <summary>
    /// Handles mouse hover and left-click selection in the Editor Game view or Desktop builds.
    /// Compatible with Unity's new Input System and legacy Input Manager.
    /// </summary>
    private void HandleMousePointerInteraction()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null) return;
        }

        // Avoid clicking through UI elements if EventSystem is present
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            ClearHover();
            return;
        }

        if (!TryGetPointerRay(out Ray ray))
        {
            ClearHover();
            return;
        }

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, anatomyLayerMask))
        {
            BodyPartSelector selector = hit.collider.GetComponent<BodyPartSelector>();
            if (selector == null)
            {
                selector = hit.collider.GetComponentInParent<BodyPartSelector>();
            }

            if (selector != null)
            {
                // Hover state
                if (_currentHovered != selector)
                {
                    ClearHover();
                    _currentHovered = selector;
                    _currentHovered.SetHovered(true);
                }

                // Click selection (Left Mouse Button)
                if (IsPointerClickTriggered())
                {
                    SelectBodyPart(selector);
                }
                return;
            }
        }

        ClearHover();
    }

    private bool TryGetPointerRay(out Ray ray)
    {
        ray = default;
        if (targetCamera == null) return false;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            ray = targetCamera.ScreenPointToRay(mousePos);
            return true;
        }
        return false;
#else
        try
        {
            ray = targetCamera.ScreenPointToRay(Input.mousePosition);
            return true;
        }
        catch
        {
            return false;
        }
#endif
    }

    private bool IsPointerClickTriggered()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.wasPressedThisFrame;
        }
        return false;
#else
        try
        {
            return Input.GetMouseButtonDown(0);
        }
        catch
        {
            return false;
        }
#endif
    }

    /// <summary>
    /// Selects the given body part, displays its details, and plays feedback audio.
    /// </summary>
    public void SelectBodyPart(BodyPartSelector selector)
    {
        if (selector == null) return;

        PlayClickSound();
        selector.SelectThisPart();
        Debug.Log($"[AnatomyInteractionRaycaster] Selected anatomy part: {selector.GetDisplayName()} ({selector.name})");
    }

    public void ClearHover()
    {
        if (_currentHovered != null)
        {
            _currentHovered.SetHovered(false);
            _currentHovered = null;
        }
    }

    private void PlayClickSound()
    {
        if (selectionSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(selectionSound, 0.7f);
        }
    }
}
