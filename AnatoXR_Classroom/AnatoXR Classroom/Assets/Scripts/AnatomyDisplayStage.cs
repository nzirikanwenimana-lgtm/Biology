using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Master movable presentation stage for human anatomy in AnatoXR Classroom.
/// 
/// Hierarchy:
/// AnatomyDisplayStage
/// ├── DisplayCircle           (visual presentation dais & grab handle)
/// ├── AnatomyCenter           (exact (0,0,0) reference anchor for all systems)
/// ├── HumanBody               (contains all anatomical systems aligned to AnatomyCenter)
/// ├── SystemVisualizations    (system-wide visual overlays, pathways, indicators)
/// └── AnatomyEffects          (pedestal lighting, accents, holographic effects)
/// </summary>
[DisallowMultipleComponent]
public class AnatomyDisplayStage : MonoBehaviour
{
    public static AnatomyDisplayStage Instance { get; private set; }

    [Header("Stage Hierarchy Structure")]
    [Tooltip("Visual representation of the stage on the classroom floor (dais/ring).")]
    public Transform displayCircle;

    [Tooltip("Dedicated handle used by the user to move and reposition the stage.")]
    public Transform stageHandle;

    [Tooltip("Exact center point of the stage. All anatomical systems reference this anchor.")]
    public Transform anatomyCenter;

    [Tooltip("The human body root containing all anatomy systems.")]
    public Transform humanBody;

    [Tooltip("Container for system-wide visualizations, callout indicators, or flow overlays.")]
    public Transform systemVisualizations;

    [Tooltip("Container for stage lighting, holographic pedestals, or visual effects.")]
    public Transform anatomyEffects;

    [Header("Floor Alignment & Constraints")]
    [Tooltip("If true, moving the stage keeps it resting on the classroom floor plane.")]
    public bool lockToFloorHeight = true;

    [Tooltip("The designated floor Y elevation in meters.")]
    public float floorHeight = 0f;

    [Header("Adult Human Scale")]
    [Tooltip("Target adult human height in meters (standard is ~1.75m - 1.78m).")]
    public float targetHumanHeight = 1.78f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        EnsureHierarchy();
        AlignAllSystemsToCenter();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        // Enforce floor constraint if enabled so stage never floats or tilts accidentally
        if (lockToFloorHeight)
        {
            Vector3 pos = transform.position;
            if (Mathf.Abs(pos.y - floorHeight) > 0.001f)
            {
                pos.y = floorHeight;
                transform.position = pos;
            }

            // Ensure stage stays level with the floor (yaw rotation only)
            Vector3 euler = transform.eulerAngles;
            if (Mathf.Abs(euler.x) > 0.05f || Mathf.Abs(euler.z) > 0.05f)
            {
                transform.eulerAngles = new Vector3(0f, euler.y, 0f);
            }
        }
    }

    /// <summary>
    /// Ensures all required child anchors and structures exist in the stage hierarchy.
    /// </summary>
    public void EnsureHierarchy()
    {
        if (displayCircle == null)
        {
            displayCircle = transform.Find("DisplayCircle");
        }

        if (anatomyCenter == null)
        {
            anatomyCenter = transform.Find("AnatomyCenter");
            if (anatomyCenter == null)
            {
                GameObject centerGo = new GameObject("AnatomyCenter");
                centerGo.transform.SetParent(transform, false);
                centerGo.transform.localPosition = Vector3.zero;
                centerGo.transform.localRotation = Quaternion.identity;
                centerGo.transform.localScale = Vector3.one;
                anatomyCenter = centerGo.transform;
            }
        }

        if (humanBody == null)
        {
            humanBody = transform.Find("HumanBody");
            if (humanBody == null)
            {
                // Check if existing model is in scene or under AnatomyCenter
                GameObject existingModel = GameObject.Find("Human male anatomy");
                if (existingModel != null && existingModel.transform.parent != transform)
                {
                    existingModel.name = "HumanBody";
                    existingModel.transform.SetParent(transform, false);
                    humanBody = existingModel.transform;
                }
                else
                {
                    GameObject bodyGo = new GameObject("HumanBody");
                    bodyGo.transform.SetParent(transform, false);
                    bodyGo.transform.localPosition = Vector3.zero;
                    bodyGo.transform.localRotation = Quaternion.identity;
                    bodyGo.transform.localScale = Vector3.one;
                    humanBody = bodyGo.transform;
                }
            }
        }

        if (systemVisualizations == null)
        {
            systemVisualizations = transform.Find("SystemVisualizations");
            if (systemVisualizations == null)
            {
                GameObject vizGo = new GameObject("SystemVisualizations");
                vizGo.transform.SetParent(transform, false);
                vizGo.transform.localPosition = Vector3.zero;
                vizGo.transform.localRotation = Quaternion.identity;
                vizGo.transform.localScale = Vector3.one;
                systemVisualizations = vizGo.transform;
            }
        }

        if (anatomyEffects == null)
        {
            anatomyEffects = transform.Find("AnatomyEffects");
            if (anatomyEffects == null)
            {
                GameObject fxGo = new GameObject("AnatomyEffects");
                fxGo.transform.SetParent(transform, false);
                fxGo.transform.localPosition = Vector3.zero;
                fxGo.transform.localRotation = Quaternion.identity;
                fxGo.transform.localScale = Vector3.one;
                anatomyEffects = fxGo.transform;
            }
        }

        if (stageHandle == null && displayCircle != null)
        {
            stageHandle = displayCircle.Find("StageGrabHandle");
        }
    }

    /// <summary>
    /// Enforces that all anatomy systems (Skeletal, Muscular, Circulatory, Visceral, Nervous, Joints, etc.)
    /// share the exact same reference center point at AnatomyCenter.
    /// </summary>
    public void AlignAllSystemsToCenter()
    {
        if (humanBody == null) return;

        // Position HumanBody relative to AnatomyCenter
        Vector3 centerLocalPos = (anatomyCenter != null) ? anatomyCenter.localPosition : Vector3.zero;
        humanBody.localPosition = centerLocalPos;
        humanBody.localRotation = Quaternion.identity;

        // Ensure all system child layers under HumanBody are zeroed to AnatomyCenter
        for (int i = 0; i < humanBody.childCount; i++)
        {
            Transform layer = humanBody.GetChild(i);
            string lname = layer.name.ToLower();
            if (lname.Contains("canvas") || lname.Contains("camera") || lname.Contains("light")) continue;

            layer.localPosition = Vector3.zero;
            layer.localRotation = Quaternion.identity;
        }
    }

    /// <summary>
    /// Calibrates the full anatomy model to life-size adult human scale (~1.78m height).
    /// </summary>
    public void CalibrateAdultScale()
    {
        if (humanBody == null) return;

        Renderer[] renderers = humanBody.GetComponentsInChildren<Renderer>(false);
        if (renderers.Length == 0) return;

        Bounds totalBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i].gameObject.activeInHierarchy)
            {
                totalBounds.Encapsulate(renderers[i].bounds);
            }
        }

        float currentHeight = totalBounds.size.y;
        if (currentHeight > 0.05f && Mathf.Abs(currentHeight - targetHumanHeight) > 0.08f)
        {
            float scaleFactor = targetHumanHeight / currentHeight;
            humanBody.localScale *= scaleFactor;
            Debug.Log($"[AnatomyDisplayStage] Calibrated adult human scale from {currentHeight:F2}m to {targetHumanHeight:F2}m (Factor: {scaleFactor:F3}).");
        }
    }

    /// <summary>
    /// Repositions the stage to the given world position, preserving floor grounding.
    /// </summary>
    public void SetStageWorldPosition(Vector3 newPos)
    {
        if (lockToFloorHeight)
        {
            newPos.y = floorHeight;
        }
        transform.position = newPos;
    }

    /// <summary>
    /// Rotates the entire stage presentation around the Y axis by the given degrees.
    /// </summary>
    public void RotateStageYaw(float degrees)
    {
        transform.Rotate(Vector3.up, degrees, Space.World);
    }
}
