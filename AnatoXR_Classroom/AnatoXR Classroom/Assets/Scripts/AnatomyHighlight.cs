using UnityEngine;

/// <summary>
/// Quest-optimized anatomy mesh highlighting component (Build Book Phase 5.4 & Appendix B).
/// Caches original shared materials and uses a single static shared highlight material
/// to eliminate runtime GC allocations and draw-call batching breaks.
/// </summary>
[DisallowMultipleComponent]
public class AnatomyHighlight : MonoBehaviour
{
    [Header("Renderers to Highlight")]
    [Tooltip("Renderers affected by highlight. Auto-detected from children if empty.")]
    [SerializeField] private Renderer[] renderersToHighlight;

    [Header("Highlight Appearance")]
    [SerializeField] private Color highlightColor = new Color(0.25f, 0.75f, 1f, 1f);

    private Material[][] _originalMaterials;
    private bool _isHighlighted;

    private static Material _sharedHighlightMaterial;

    private void Awake()
    {
        InitializeRenderers();
    }

    private void InitializeRenderers()
    {
        if (renderersToHighlight == null || renderersToHighlight.Length == 0)
        {
            renderersToHighlight = GetComponentsInChildren<Renderer>(true);
        }

        if (renderersToHighlight != null && renderersToHighlight.Length > 0)
        {
            _originalMaterials = new Material[renderersToHighlight.Length][];
            for (int i = 0; i < renderersToHighlight.Length; i++)
            {
                if (renderersToHighlight[i] != null)
                {
                    _originalMaterials[i] = renderersToHighlight[i].sharedMaterials;
                }
            }
        }
    }

    /// <summary>
    /// Returns or creates the single shared highlight material used across all anatomy meshes.
    /// </summary>
    public static Material GetSharedHighlightMaterial(Color color)
    {
        if (_sharedHighlightMaterial == null)
        {
            // Use URP Lit or standard fallback shader
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            if (shader != null)
            {
                _sharedHighlightMaterial = new Material(shader)
                {
                    name = "MAT_AnatomyHighlight_Shared",
                    hideFlags = HideFlags.DontSave
                };
            }
        }

        if (_sharedHighlightMaterial != null)
        {
            _sharedHighlightMaterial.color = color;
            if (_sharedHighlightMaterial.HasProperty("_BaseColor"))
                _sharedHighlightMaterial.SetColor("_BaseColor", color);

            if (_sharedHighlightMaterial.HasProperty("_EmissionColor"))
            {
                _sharedHighlightMaterial.EnableKeyword("_EMISSION");
                _sharedHighlightMaterial.SetColor("_EmissionColor", color * 0.4f);
            }
        }

        return _sharedHighlightMaterial;
    }

    public void SetSelected(bool selected)
    {
        if (_isHighlighted == selected) return;
        _isHighlighted = selected;

        if (renderersToHighlight == null || renderersToHighlight.Length == 0)
        {
            InitializeRenderers();
            if (renderersToHighlight == null || renderersToHighlight.Length == 0) return;
        }

        if (!selected)
        {
            // Restore original shared materials
            for (int i = 0; i < renderersToHighlight.Length; i++)
            {
                if (renderersToHighlight[i] != null && _originalMaterials != null && i < _originalMaterials.Length)
                {
                    if (_originalMaterials[i] != null)
                    {
                        renderersToHighlight[i].sharedMaterials = _originalMaterials[i];
                    }
                }
            }
            return;
        }

        // Apply shared highlight material (Zero heap allocations!)
        Material highlightMat = GetSharedHighlightMaterial(highlightColor);
        if (highlightMat != null)
        {
            for (int i = 0; i < renderersToHighlight.Length; i++)
            {
                if (renderersToHighlight[i] != null && _originalMaterials != null && i < _originalMaterials.Length)
                {
                    int matCount = _originalMaterials[i] != null ? _originalMaterials[i].Length : 1;
                    Material[] highlightArray = new Material[matCount];
                    for (int m = 0; m < matCount; m++)
                    {
                        highlightArray[m] = highlightMat;
                    }
                    renderersToHighlight[i].sharedMaterials = highlightArray;
                }
            }
        }
    }
}
