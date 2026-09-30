using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls the 'Fold / Unfold / Separate' anatomical layer separation mechanic.
/// Students can pull anatomical layers apart (Skin → Muscles → Organs → Bones)
/// to inspect deep internal structures in 3D space, and fold them back together.
/// </summary>
public class AnatomyExplodedView : MonoBehaviour
{
    [System.Serializable]
    public class ExplodedLayer
    {
        public string layerName;
        [Tooltip("The root GameObject of this anatomical layer.")]
        public GameObject layerRoot;

        [Tooltip("Offset direction and maximum distance when fully unfolded (distance at separationAmount = 1.0).")]
        public Vector3 separationOffset = Vector3.forward * 0.4f;

        [HideInInspector]
        public Vector3 initialLocalPosition;
        [HideInInspector]
        public Quaternion initialLocalRotation;
        [HideInInspector]
        public bool isInitialized = false;
    }

    [Header("Anatomical Layers (Outermost to Innermost)")]
    [Tooltip("Define anatomical layers from skin outward to deep bones.")]
    public List<ExplodedLayer> layers = new List<ExplodedLayer>();

    [Header("Exploded View State")]
    [Range(0f, 1f)]
    [Tooltip("0.0 = Completely folded (assembled), 1.0 = Fully separated / exploded.")]
    [SerializeField] private float separationAmount = 0f;

    [Header("Animation")]
    [Tooltip("Duration of smooth folding/unfolding animation in seconds.")]
    public float animationDuration = 0.6f;

    [Tooltip("Animation curve for smooth deceleration.")]
    public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Coroutine _animCoroutine;

    private void Awake()
    {
        InitializeLayers();
    }

    /// <summary>
    /// Captures original resting transforms of all layers.
    /// </summary>
    public void InitializeLayers()
    {
        if (layers == null || layers.Count == 0)
        {
            AutoDetectLayers();
        }

        foreach (var layer in layers)
        {
            if (layer.layerRoot != null && !layer.isInitialized)
            {
                layer.initialLocalPosition = layer.layerRoot.transform.localPosition;
                layer.initialLocalRotation = layer.layerRoot.transform.localRotation;
                layer.isInitialized = true;
            }
        }
    }

    /// <summary>
    /// Auto-detects standard anatomy layers if not configured manually.
    /// </summary>
    public void AutoDetectLayers()
    {
        if (layers == null) layers = new List<ExplodedLayer>();
        layers.Clear();

        // Look for common anatomy roots in hierarchy
        Transform root = transform;
        Transform skin = FindChildRecursive(root, "male", "skin", "body");
        Transform muscle = FindChildRecursive(root, "muscul");
        Transform visceral = FindChildRecursive(root, "viscer", "organ");
        Transform circulatory = FindChildRecursive(root, "circulat", "cardio");
        Transform skeleton = FindChildRecursive(root, "skelet", "bone");
        Transform nervous = FindChildRecursive(root, "nerv");

        // Skin pulls farthest forward (+Z)
        if (skin != null)
        {
            layers.Add(new ExplodedLayer { layerName = "Skin / Body", layerRoot = skin.gameObject, separationOffset = new Vector3(0f, 0f, 0.65f) });
        }

        // Muscles pull forward (+Z intermediate)
        if (muscle != null)
        {
            layers.Add(new ExplodedLayer { layerName = "Muscular System", layerRoot = muscle.gameObject, separationOffset = new Vector3(0f, 0f, 0.35f) });
        }

        // Visceral organs shift forward and slightly right (+X, +Z)
        if (visceral != null)
        {
            layers.Add(new ExplodedLayer { layerName = "Visceral Organs", layerRoot = visceral.gameObject, separationOffset = new Vector3(0.25f, 0f, 0.15f) });
        }

        // Circulatory vessels shift forward and slightly left (-X, +Z)
        if (circulatory != null)
        {
            layers.Add(new ExplodedLayer { layerName = "Circulatory System", layerRoot = circulatory.gameObject, separationOffset = new Vector3(-0.25f, 0f, 0.15f) });
        }

        // Nervous shifts slightly back/up
        if (nervous != null)
        {
            layers.Add(new ExplodedLayer { layerName = "Nervous System", layerRoot = nervous.gameObject, separationOffset = new Vector3(0f, 0f, -0.15f) });
        }

        // Skeleton stays at the anchor core (0 offset)
        if (skeleton != null)
        {
            layers.Add(new ExplodedLayer { layerName = "Skeletal System", layerRoot = skeleton.gameObject, separationOffset = Vector3.zero });
        }
    }

    private Transform FindChildRecursive(Transform parent, params string[] keywords)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            string childName = child.name.ToLowerInvariant();
            foreach (string kw in keywords)
            {
                if (childName.Contains(kw))
                    return child;
            }

            Transform deeper = FindChildRecursive(child, keywords);
            if (deeper != null) return deeper;
        }
        return null;
    }

    // -------------------------------------------------------------------------
    // Fold / Unfold Controls
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sets separation amount immediately (0.0 = completely folded, 1.0 = fully separated).
    /// </summary>
    public void SetSeparation(float amount)
    {
        separationAmount = Mathf.Clamp01(amount);
        ApplySeparation();
    }

    /// <summary>
    /// Returns current separation amount (0.0 to 1.0).
    /// </summary>
    public float GetSeparation()
    {
        return separationAmount;
    }

    /// <summary>
    /// Smoothly unfolds / separates all layers apart.
    /// </summary>
    public void UnfoldAll()
    {
        AnimateTo(1.0f);
    }

    /// <summary>
    /// Smoothly folds all layers back into the anatomical whole.
    /// </summary>
    public void FoldAll()
    {
        AnimateTo(0.0f);
    }

    /// <summary>
    /// Toggles between fully folded and fully unfolded.
    /// </summary>
    public void ToggleFold()
    {
        float target = (separationAmount > 0.5f) ? 0.0f : 1.0f;
        AnimateTo(target);
    }

    /// <summary>
    /// Steps separation by an increment (e.g. +0.25f or -0.25f).
    /// </summary>
    public void StepSeparation(float step)
    {
        AnimateTo(Mathf.Clamp01(separationAmount + step));
    }

    private void AnimateTo(float target)
    {
        if (_animCoroutine != null)
        {
            StopCoroutine(_animCoroutine);
        }
        _animCoroutine = StartCoroutine(AnimateSeparationRoutine(target));
    }

    private IEnumerator AnimateSeparationRoutine(float target)
    {
        float start = separationAmount;
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            float curveT = (animationCurve != null) ? animationCurve.Evaluate(t) : t;

            separationAmount = Mathf.Lerp(start, target, curveT);
            ApplySeparation();
            yield return null;
        }

        separationAmount = target;
        ApplySeparation();
        _animCoroutine = null;
    }

    private void ApplySeparation()
    {
        foreach (var layer in layers)
        {
            if (layer.layerRoot == null) continue;

            if (!layer.isInitialized)
            {
                layer.initialLocalPosition = layer.layerRoot.transform.localPosition;
                layer.initialLocalRotation = layer.layerRoot.transform.localRotation;
                layer.isInitialized = true;
            }

            Vector3 targetLocalPos = layer.initialLocalPosition + (layer.separationOffset * separationAmount);
            layer.layerRoot.transform.localPosition = targetLocalPos;
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            ApplySeparation();
        }
    }
}
