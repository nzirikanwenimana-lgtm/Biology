using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Custom Teleportation Area designed for single-mesh room models and carpets.
/// It filters teleportation so that the player can only teleport on horizontal floor and carpet surfaces,
/// preventing teleportation onto walls, ceilings, and rooftops.
/// </summary>
[AddComponentMenu("XR/Locomotion/Floor Only Teleportation Area")]
public class FloorOnlyTeleportationArea : TeleportationArea
{
    [Header("Floor Slope Filtering")]
    [Tooltip("Maximum allowed slope angle in degrees relative to straight up (Vector3.up). Surfaces steeper than this (like walls at 90° and ceilings at 180°) will be blocked.")]
    [Range(0f, 90f)]
    [SerializeField]
    private float m_MaxSlopeAngle = 35f;

    [Header("Carpet & Additional Colliders")]
    [Tooltip("Automatically finds and includes any colliders on child objects.")]
    [SerializeField]
    private bool m_IncludeChildColliders = true;

    [Tooltip("Automatically finds and includes any GameObjects named 'Carpet' or 'Rug' in the scene.")]
    [SerializeField]
    private bool m_AutoFindCarpets = true;

    [Tooltip("Manually assign additional colliders (such as separate Carpet or Rug objects) to allow teleporting on them.")]
    [SerializeField]
    private List<Collider> m_AdditionalColliders = new List<Collider>();

    [Tooltip("Extra height tolerance (in meters) to allow carpets and rugs that sit slightly above the floor.")]
    [SerializeField]
    private float m_CarpetHeightTolerance = 0.15f;

    [Header("Height Filtering (Optional)")]
    [Tooltip("Enable to prevent teleporting onto elevated horizontal surfaces like table tops, beds, shelves, or rooftops.")]
    [SerializeField]
    private bool m_UseHeightFilter = false;

    [Tooltip("If true, uses world Y coordinate. If false, uses local Y coordinate relative to this GameObject.")]
    [SerializeField]
    private bool m_UseWorldHeight = true;

    [Tooltip("Minimum allowed Y height for valid floor teleportation.")]
    [SerializeField]
    private float m_MinFloorHeight = -0.5f;

    [Tooltip("Maximum allowed Y height for valid floor teleportation.")]
    [SerializeField]
    private float m_MaxFloorHeight = 0.5f;

    [Header("Debug Visuals")]
    [Tooltip("Draw wireframe gizmo in Scene View showing the valid floor bounds when this object is selected.")]
    [SerializeField]
    private bool m_DrawGizmos = true;

    public float maxSlopeAngle
    {
        get => m_MaxSlopeAngle;
        set => m_MaxSlopeAngle = Mathf.Clamp(value, 0f, 90f);
    }

    public bool includeChildColliders
    {
        get => m_IncludeChildColliders;
        set => m_IncludeChildColliders = value;
    }

    public bool autoFindCarpets
    {
        get => m_AutoFindCarpets;
        set => m_AutoFindCarpets = value;
    }

    public List<Collider> additionalColliders
    {
        get => m_AdditionalColliders;
        set => m_AdditionalColliders = value;
    }

    public float carpetHeightTolerance
    {
        get => m_CarpetHeightTolerance;
        set => m_CarpetHeightTolerance = Mathf.Max(0f, value);
    }

    public bool useHeightFilter
    {
        get => m_UseHeightFilter;
        set => m_UseHeightFilter = value;
    }

    public bool useWorldHeight
    {
        get => m_UseWorldHeight;
        set => m_UseWorldHeight = value;
    }

    public float minFloorHeight
    {
        get => m_MinFloorHeight;
        set => m_MinFloorHeight = value;
    }

    public float maxFloorHeight
    {
        get => m_MaxFloorHeight;
        set => m_MaxFloorHeight = value;
    }

    private static readonly Regex CarpetWordRegex = new Regex(
        @"(^|[\s_\-\.\(\)])(carpet|rug|doormat|floormat|floor_mat|bathmat)([\s_\-\.\(\)]|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    protected override void Awake()
    {
        RegisterCarpetAndColliders();
        base.Awake();
    }

    /// <summary>
    /// Checks whether a collider represents a carpet/rug surface and does not belong to another interactable or anatomy object.
    /// </summary>
    private bool IsCarpetCollider(Collider col)
    {
        if (col == null || col.gameObject == gameObject) return false;

        // Never take colliders that are part of anatomy models or have their own interactable
        if (col.GetComponentInParent<BodyPartSelector>() != null) return false;
        var otherInteractable = col.GetComponentInParent<XRBaseInteractable>();
        if (otherInteractable != null && otherInteractable != this) return false;

        string colName = col.gameObject.name;

        // Ignore materials, meshes, UI, or anatomy components
        if (colName.IndexOf("material", StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("anatomy", StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("body", StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("muscle", StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("bone", StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("organ", StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("nerve", StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("artery", StringComparison.OrdinalIgnoreCase) >= 0 ||
            colName.IndexOf("vein", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        return CarpetWordRegex.IsMatch(colName);
    }

    /// <summary>
    /// Registers child and carpet colliders into the interactable's collider list.
    /// </summary>
    public void RegisterCarpetAndColliders()
    {
        // 1. Clean any destroyed, foreign interactables, or anatomy colliders from the list
        colliders.RemoveAll(col =>
        {
            if (col == null) return true;
            if (col.gameObject != gameObject)
            {
                if (col.GetComponentInParent<BodyPartSelector>() != null) return true;
                var otherInteractable = col.GetComponentInParent<XRBaseInteractable>();
                if (otherInteractable != null && otherInteractable != this) return true;
            }
            return false;
        });

        // 2. Child colliders (only room elements without their own interactables)
        if (m_IncludeChildColliders)
        {
            Collider[] childCols = GetComponentsInChildren<Collider>(true);
            foreach (var col in childCols)
            {
                if (col == null) continue;

                if (col.gameObject != gameObject)
                {
                    if (col.GetComponentInParent<BodyPartSelector>() != null) continue;
                    var otherInteractable = col.GetComponentInParent<XRBaseInteractable>();
                    if (otherInteractable != null && otherInteractable != this) continue;
                }

                if (!colliders.Contains(col))
                {
                    colliders.Add(col);
                }
            }
        }

        // 3. Auto-find carpets and rugs in the scene safely
        if (m_AutoFindCarpets)
        {
#if UNITY_2023_1_OR_NEWER
            Collider[] allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
#else
            Collider[] allColliders = FindObjectsOfType<Collider>();
#endif
            foreach (var col in allColliders)
            {
                if (IsCarpetCollider(col) && !colliders.Contains(col))
                {
                    colliders.Add(col);
                }
            }
        }

        // 4. Manually assigned Additional Colliders
        if (m_AdditionalColliders != null)
        {
            foreach (var col in m_AdditionalColliders)
            {
                if (col != null && !colliders.Contains(col))
                {
                    if (col.GetComponentInParent<BodyPartSelector>() != null) continue;
                    var otherInteractable = col.GetComponentInParent<XRBaseInteractable>();
                    if (otherInteractable != null && otherInteractable != this) continue;

                    colliders.Add(col);
                }
            }
        }
    }

    /// <summary>
    /// Validates whether the raycast hit is on a valid floor/carpet surface before generating a teleport request.
    /// </summary>
    protected override bool GenerateTeleportRequest(IXRInteractor interactor, RaycastHit raycastHit, ref TeleportRequest teleportRequest)
    {
        if (!IsValidFloorHit(raycastHit))
        {
            return false;
        }

        return base.GenerateTeleportRequest(interactor, raycastHit, ref teleportRequest);
    }

    /// <summary>
    /// Checks whether the ray interactor can hover over the current surface.
    /// Returns false if pointing at walls or ceiling, preventing the teleport ray/reticle from showing on them.
    /// </summary>
    public override bool IsHoverableBy(IXRHoverInteractor interactor)
    {
        if (!base.IsHoverableBy(interactor))
            return false;

        if (interactor is XRRayInteractor rayInteractor)
        {
            if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
            {
                return IsValidFloorHit(hit);
            }
        }

        return true;
    }

    /// <summary>
    /// Checks whether the ray interactor can select the current surface.
    /// </summary>
    public override bool IsSelectableBy(IXRSelectInteractor interactor)
    {
        if (!base.IsSelectableBy(interactor))
            return false;

        if (interactor is XRRayInteractor rayInteractor)
        {
            if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
            {
                return IsValidFloorHit(hit);
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether a RaycastHit point qualifies as a valid floor or carpet surface.
    /// </summary>
    public bool IsValidFloorHit(RaycastHit hit)
    {
        // 1. Surface Normal Check (Vector3.up is 0°, vertical walls are 90°, ceilings/roofs are 180°)
        float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
        if (slopeAngle > m_MaxSlopeAngle)
        {
            return false;
        }

        // 2. Optional: Height Range Check (includes carpet tolerance to allow slightly raised carpets/rugs)
        if (m_UseHeightFilter)
        {
            float hitY = m_UseWorldHeight ? hit.point.y : transform.InverseTransformPoint(hit.point).y;
            float maxAllowed = m_MaxFloorHeight + m_CarpetHeightTolerance;
            float minAllowed = m_MinFloorHeight - 0.05f;

            if (hitY < minAllowed || hitY > maxAllowed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Context menu helper to automatically calculate floor height from this object's collider bounds.
    /// Right-click the component header in Inspector to use.
    /// </summary>
    [ContextMenu("Auto-Set Height Filter from Collider")]
    public void AutoSetHeightFilterFromCollider()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Bounds b = col.bounds;
            if (m_UseWorldHeight)
            {
                m_MinFloorHeight = b.min.y - 0.1f;
                m_MaxFloorHeight = b.min.y + 0.6f;
            }
            else
            {
                Vector3 localMin = transform.InverseTransformPoint(b.min);
                m_MinFloorHeight = localMin.y - 0.1f;
                m_MaxFloorHeight = localMin.y + 0.6f;
            }
            m_UseHeightFilter = true;
            Debug.Log($"[FloorOnlyTeleportationArea] Auto-configured floor height range to: {m_MinFloorHeight:F2} to {m_MaxFloorHeight:F2}", this);
        }
        else
        {
            Debug.LogWarning("[FloorOnlyTeleportationArea] No Collider found on this GameObject to calculate bounds.", this);
        }
    }

    /// <summary>
    /// Context menu helper to find all carpet colliders in the scene and add them directly to Additional Colliders.
    /// </summary>
    [ContextMenu("Find and Add Carpets to Additional Colliders")]
    public void FindAndAddCarpetsToAdditionalColliders()
    {
#if UNITY_2023_1_OR_NEWER
        Collider[] allColliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
#else
        Collider[] allColliders = FindObjectsOfType<Collider>();
#endif
        int added = 0;
        foreach (var col in allColliders)
        {
            if (IsCarpetCollider(col))
            {
                if (!m_AdditionalColliders.Contains(col))
                {
                    m_AdditionalColliders.Add(col);
                    added++;
                }
            }
        }
        Debug.Log($"[FloorOnlyTeleportationArea] Added {added} carpet collider(s) to Additional Colliders.", this);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (!m_DrawGizmos) return;

        Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.4f);
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Bounds b = col.bounds;
            float bottomY = m_UseHeightFilter
                ? (m_UseWorldHeight ? (m_MinFloorHeight + m_MaxFloorHeight) * 0.5f : transform.TransformPoint(new Vector3(0, (m_MinFloorHeight + m_MaxFloorHeight) * 0.5f, 0)).y)
                : b.min.y + 0.1f;
            float height = m_UseHeightFilter ? Mathf.Max(0.1f, m_MaxFloorHeight - m_MinFloorHeight + m_CarpetHeightTolerance) : 0.2f;
            Vector3 center = new Vector3(b.center.x, bottomY, b.center.z);
            Vector3 size = new Vector3(b.size.x, height, b.size.z);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
