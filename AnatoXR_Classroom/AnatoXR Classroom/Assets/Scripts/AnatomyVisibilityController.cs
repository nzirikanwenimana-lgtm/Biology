using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Anatomy layer visibility and isolation controller (Build Book Phase 7.1 & Appendix B).
/// Allows learners to peel away systems and inspect deeper anatomy without moving or destroying the model.
/// </summary>
public class AnatomyVisibilityController : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        public string id;
        public string displayName;
        public GameObject root;

        public Layer() { }
        public Layer(string id, string displayName, GameObject root)
        {
            this.id = id;
            this.displayName = displayName;
            this.root = root;
        }
    }

    [Header("Anatomical System Layers")]
    [SerializeField] private List<Layer> layers = new List<Layer>();

    [Header("Optional AnatomySystemController Link")]
    [Tooltip("If present, can sync layers from AnatomySystemController.")]
    [SerializeField] private AnatomySystemController systemController;

    private void Awake()
    {
        if (systemController == null)
        {
            systemController = FindFirstObjectByType<AnatomySystemController>();
        }
    }

    public void ShowAll()
    {
        foreach (var layer in layers)
        {
            if (layer.root != null)
                layer.root.SetActive(true);
        }

        if (systemController != null)
            systemController.ShowAll();
    }

    public void ShowOnly(string id)
    {
        foreach (var layer in layers)
        {
            if (layer.root != null)
                layer.root.SetActive(string.Equals(layer.id, id, System.StringComparison.OrdinalIgnoreCase));
        }

        if (systemController != null)
            systemController.ShowSystem(id);
    }

    public void Toggle(string id)
    {
        foreach (var layer in layers)
        {
            if (layer.root != null && string.Equals(layer.id, id, System.StringComparison.OrdinalIgnoreCase))
                layer.root.SetActive(!layer.root.activeSelf);
        }

        if (systemController != null)
            systemController.ToggleSystem(id);
    }

    public void Hide(string id)
    {
        foreach (var layer in layers)
        {
            if (layer.root != null && string.Equals(layer.id, id, System.StringComparison.OrdinalIgnoreCase))
                layer.root.SetActive(false);
        }

        if (systemController != null)
            systemController.HideSystem(id);
    }
}
