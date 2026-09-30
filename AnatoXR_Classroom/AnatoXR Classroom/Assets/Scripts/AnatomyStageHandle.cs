using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Dedicated interactive handle attached to the DisplayCircle.
/// Enables the learner/instructor to grab and reposition the master AnatomyDisplayStage
/// anywhere in the VR classroom using XR Ray or direct hand grabbing.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AnatomyStageHandle : MonoBehaviour
{
    [Header("Stage Reference")]
    [Tooltip("The master AnatomyDisplayStage being moved. Defaults to parent AnatomyDisplayStage if null.")]
    public AnatomyDisplayStage stage;

    [Header("Visual Feedback")]
    [Tooltip("Renderer to highlight on hover/grab.")]
    public Renderer handleRenderer;

    [Tooltip("Normal idle color.")]
    public Color normalColor = new Color(0.15f, 0.55f, 0.95f, 0.9f);

    [Tooltip("Hovered color.")]
    public Color hoverColor = new Color(0.35f, 0.85f, 1.0f, 1.0f);

    [Tooltip("Grabbed / active color.")]
    public Color grabbedColor = new Color(0.1f, 1.0f, 0.6f, 1.0f);

    private Material _handleMaterialInstance;
    private XRGrabInteractable _grabInteractable;
    private bool _isGrabbed = false;

    private void Awake()
    {
        if (stage == null)
        {
            stage = GetComponentInParent<AnatomyDisplayStage>();
        }

        if (handleRenderer == null)
        {
            handleRenderer = GetComponent<Renderer>();
        }

        if (handleRenderer != null)
        {
            _handleMaterialInstance = handleRenderer.material;
            SetHandleColor(normalColor);
        }

        SetupInteractable();
    }

    private void SetupInteractable()
    {
        // 1. Check if master stage already has XRGrabInteractable
        if (stage != null)
        {
            XRGrabInteractable stageGrab = stage.GetComponent<XRGrabInteractable>();
            if (stageGrab != null)
            {
                Collider myCol = GetComponent<Collider>();
                if (myCol != null && !stageGrab.colliders.Contains(myCol))
                {
                    stageGrab.colliders.Add(myCol);
                }

                _grabInteractable = stageGrab;
                _grabInteractable.firstHoverEntered.AddListener(OnHoverEnter);
                _grabInteractable.lastHoverExited.AddListener(OnHoverExit);
                _grabInteractable.selectEntered.AddListener(OnGrabEnter);
                _grabInteractable.selectExited.AddListener(OnGrabExit);
                return;
            }
        }

        // 2. Otherwise attach grab interactable to handle and configure kinematic stage movement
        _grabInteractable = GetComponent<XRGrabInteractable>();
        if (_grabInteractable == null)
        {
            _grabInteractable = gameObject.AddComponent<XRGrabInteractable>();
        }

        _grabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;
        _grabInteractable.throwOnDetach = false;
        _grabInteractable.trackPosition = true;
        _grabInteractable.trackRotation = true;

        _grabInteractable.firstHoverEntered.AddListener(OnHoverEnter);
        _grabInteractable.lastHoverExited.AddListener(OnHoverExit);
        _grabInteractable.selectEntered.AddListener(OnGrabEnter);
        _grabInteractable.selectExited.AddListener(OnGrabExit);
    }

    private void OnDestroy()
    {
        if (_grabInteractable != null)
        {
            _grabInteractable.firstHoverEntered.RemoveListener(OnHoverEnter);
            _grabInteractable.lastHoverExited.RemoveListener(OnHoverExit);
            _grabInteractable.selectEntered.RemoveListener(OnGrabEnter);
            _grabInteractable.selectExited.RemoveListener(OnGrabExit);
        }

        if (_handleMaterialInstance != null)
        {
            Destroy(_handleMaterialInstance);
        }
    }

    private void OnHoverEnter(HoverEnterEventArgs args)
    {
        if (!_isGrabbed)
        {
            SetHandleColor(hoverColor);
        }
    }

    private void OnHoverExit(HoverExitEventArgs args)
    {
        if (!_isGrabbed)
        {
            SetHandleColor(normalColor);
        }
    }

    private void OnGrabEnter(SelectEnterEventArgs args)
    {
        _isGrabbed = true;
        SetHandleColor(grabbedColor);
    }

    private void OnGrabExit(SelectExitEventArgs args)
    {
        _isGrabbed = false;
        SetHandleColor(hoverColor);

        // Snap stage to floor height on release
        if (stage != null && stage.lockToFloorHeight)
        {
            Vector3 pos = stage.transform.position;
            pos.y = stage.floorHeight;
            stage.transform.position = pos;
        }
    }

    private void SetHandleColor(Color col)
    {
        if (_handleMaterialInstance != null)
        {
            _handleMaterialInstance.color = col;
            if (_handleMaterialInstance.HasProperty("_EmissionColor"))
            {
                _handleMaterialInstance.SetColor("_EmissionColor", col * 0.5f);
                _handleMaterialInstance.EnableKeyword("_EMISSION");
            }
        }
    }
}
