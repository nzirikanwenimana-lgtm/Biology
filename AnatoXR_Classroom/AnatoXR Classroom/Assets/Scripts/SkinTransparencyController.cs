using UnityEngine;

/// <summary>
/// Controls skin layer transparency/x-ray mode (Build Book Phase 7.2).
/// Switches skin renderers to a transparent-capable URP material when enabled,
/// and restores original materials when disabled without memory leaks.
/// </summary>
public class SkinTransparencyController : MonoBehaviour
{
    [Header("Skin Renderers")]
    [Tooltip("Renderers representing the outer skin/body. Auto-detected from children if empty.")]
    [SerializeField] private Renderer[] skinRenderers;

    [Header("Transparency Settings")]
    [Range(0.05f, 0.95f)]
    [SerializeField] private float transparentAlpha = 0.25f;

    [SerializeField] private Color transparentColor = new Color(0.8f, 0.85f, 1f, 0.25f);

    private Material[][] _originalMaterials;
    private Material _transparentMaterial;
    private bool _isTransparent = false;

    private void Awake()
    {
        InitializeRenderers();
    }

    private void InitializeRenderers()
    {
        if (skinRenderers == null || skinRenderers.Length == 0)
        {
            skinRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (skinRenderers != null && skinRenderers.Length > 0)
        {
            _originalMaterials = new Material[skinRenderers.Length][];
            for (int i = 0; i < skinRenderers.Length; i++)
            {
                if (skinRenderers[i] != null)
                {
                    _originalMaterials[i] = skinRenderers[i].sharedMaterials;
                }
            }
        }
    }

    private Material GetOrCreateTransparentMaterial()
    {
        if (_transparentMaterial == null)
        {
            Shader uLit = Shader.Find("Universal Render Pipeline/Lit");
            if (uLit == null) uLit = Shader.Find("Standard");

            if (uLit != null)
            {
                _transparentMaterial = new Material(uLit)
                {
                    name = "MAT_Skin_Transparent_Shared",
                    hideFlags = HideFlags.DontSave
                };

                // Configure for transparent surface in URP
                _transparentMaterial.SetFloat("_Surface", 1); // Transparent
                _transparentMaterial.SetFloat("_Blend", 0);   // Alpha blend
                _transparentMaterial.SetFloat("_ZWrite", 0);
                _transparentMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                _transparentMaterial.SetOverrideTag("RenderType", "Transparent");

                Color c = transparentColor;
                c.a = transparentAlpha;
                _transparentMaterial.color = c;
                if (_transparentMaterial.HasProperty("_BaseColor"))
                    _transparentMaterial.SetColor("_BaseColor", c);
            }
        }

        return _transparentMaterial;
    }

    /// <summary>
    /// Toggles skin transparency on or off.
    /// </summary>
    public void ToggleTransparency()
    {
        SetTransparency(!_isTransparent);
    }

    /// <summary>
    /// Enables or disables transparent skin mode.
    /// </summary>
    public void SetTransparency(bool enableTransparent)
    {
        if (_isTransparent == enableTransparent) return;
        _isTransparent = enableTransparent;

        if (skinRenderers == null || skinRenderers.Length == 0)
        {
            InitializeRenderers();
            if (skinRenderers == null || skinRenderers.Length == 0) return;
        }

        if (!enableTransparent)
        {
            // Restore original materials
            for (int i = 0; i < skinRenderers.Length; i++)
            {
                if (skinRenderers[i] != null && _originalMaterials != null && i < _originalMaterials.Length)
                {
                    if (_originalMaterials[i] != null)
                    {
                        skinRenderers[i].sharedMaterials = _originalMaterials[i];
                    }
                }
            }
            return;
        }

        // Apply shared transparent material
        Material transMat = GetOrCreateTransparentMaterial();
        if (transMat != null)
        {
            for (int i = 0; i < skinRenderers.Length; i++)
            {
                if (skinRenderers[i] != null && _originalMaterials != null && i < _originalMaterials.Length)
                {
                    int count = _originalMaterials[i] != null ? _originalMaterials[i].Length : 1;
                    Material[] arr = new Material[count];
                    for (int m = 0; m < count; m++)
                    {
                        arr[m] = transMat;
                    }
                    skinRenderers[i].sharedMaterials = arr;
                }
            }
        }
    }

    public bool IsTransparent => _isTransparent;
}
