using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Media viewer component attached to the Info Panel.
/// Displays anatomical diagrams, reference images, and runs 3D inspection animations
/// (such as cardiac pulse or inspection turntable) for the selected body part.
/// </summary>
public class AnatomyMediaViewer : MonoBehaviour
{
    [Header("UI Display Components")]
    [Tooltip("UI Image component to display the anatomical diagram / photo.")]
    public Image diagramImage;

    [Tooltip("Optional RawImage component for video or web textures.")]
    public RawImage rawDiagramImage;

    [Tooltip("Caption or label below the media.")]
    public TMP_Text mediaCaption;

    [Tooltip("Container GameObject holding the media view (can be hidden if no media).")]
    public GameObject mediaContainer;

    [Header("Default Fallback Visuals")]
    [Tooltip("Default placeholder sprite used if no specific diagram is assigned.")]
    public Sprite defaultDiagramSprite;

    [Header("3D Inspection & Animation")]
    [Tooltip("Inspect animation button text / indicator.")]
    public TMP_Text animationButtonText;

    [Tooltip("Pulse scale speed for pulsating organs (e.g. Heart).")]
    public float pulseSpeed = 2.5f;

    [Tooltip("Pulse scale amplitude.")]
    public float pulseAmplitude = 0.08f;

    private Transform _inspectedTransform;
    private Vector3 _originalInspectedScale;
    private Coroutine _pulseCoroutine;
    private bool _isPulsing = false;

    private void Awake()
    {
        if (mediaContainer == null && diagramImage != null)
        {
            mediaContainer = diagramImage.gameObject;
        }
    }

    /// <summary>
    /// Loads and displays the diagram/image for the given body part.
    /// Supports direct Sprite assignment, Resources loading, or medical fallback icon.
    /// </summary>
    public void DisplayMedia(BodyPartSelector bodyPart)
    {
        if (bodyPart == null)
        {
            HideMedia();
            return;
        }

        _inspectedTransform = bodyPart.transform;
        _originalInspectedScale = bodyPart.transform.localScale;

        // Check if there is an image specified directly on the selector or in Resources
        Sprite partSprite = bodyPart.diagramSprite;

        if (partSprite == null)
        {
            // Attempt Resources load: e.g. "AnatomyDiagrams/<PartName>"
            partSprite = Resources.Load<Sprite>($"AnatomyDiagrams/{bodyPart.gameObject.name}");
        }

        if (partSprite == null)
        {
            partSprite = defaultDiagramSprite;
        }

        if (diagramImage != null)
        {
            if (partSprite != null)
            {
                diagramImage.sprite = partSprite;
                diagramImage.gameObject.SetActive(true);
                if (mediaContainer != null) mediaContainer.SetActive(true);
            }
            else
            {
                // Fallback: hide image if no diagram exists, or keep container active with system badge
                if (mediaContainer != null) mediaContainer.SetActive(false);
            }
        }

        if (mediaCaption != null)
        {
            mediaCaption.text = $"{bodyPart.GetDisplayName()} Diagram";
        }

        // Reset animation state
        StopAnimation();
    }

    /// <summary>
    /// Toggles 3D inspection animation (e.g. cardiac rhythm or anatomical oscillation).
    /// </summary>
    public void ToggleInspectionAnimation()
    {
        if (_isPulsing)
        {
            StopAnimation();
        }
        else
        {
            StartPulseAnimation();
        }
    }

    /// <summary>
    /// Starts a smooth anatomical pulse / rhythmic inspection animation on the selected structure.
    /// </summary>
    public void StartPulseAnimation()
    {
        if (_inspectedTransform == null) return;

        StopAnimation();
        _pulseCoroutine = StartCoroutine(PulseRoutine());
        _isPulsing = true;
        if (animationButtonText != null) animationButtonText.text = "Stop Animation";
    }

    /// <summary>
    /// Stops the 3D pulse / examination animation and restores normal scale.
    /// </summary>
    public void StopAnimation()
    {
        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }

        if (_isPulsing && _inspectedTransform != null)
        {
            _inspectedTransform.localScale = _originalInspectedScale;
        }

        _isPulsing = false;
        if (animationButtonText != null) animationButtonText.text = "Play Animation";
    }

    private IEnumerator PulseRoutine()
    {
        float timer = 0f;
        while (_inspectedTransform != null)
        {
            timer += Time.deltaTime * pulseSpeed;
            float scaleMod = 1f + Mathf.Sin(timer) * pulseAmplitude;
            _inspectedTransform.localScale = _originalInspectedScale * scaleMod;
            yield return null;
        }
    }

    public void HideMedia()
    {
        StopAnimation();
        if (mediaContainer != null)
        {
            mediaContainer.SetActive(false);
        }
    }

    private void OnDisable()
    {
        StopAnimation();
    }
}
