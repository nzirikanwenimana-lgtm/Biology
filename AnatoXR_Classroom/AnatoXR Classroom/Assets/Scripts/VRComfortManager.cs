using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

/// <summary>
/// VR Comfort and Recenter Manager (Build Book Phase 8.1 & 8.5).
/// Provides easy reset/recenter for the player and controls comfort locomotion settings.
/// </summary>
public class VRComfortManager : MonoBehaviour
{
    [Header("XR Rig / Origin")]
    [Tooltip("Reference to the XROrigin. Auto-detected if unassigned.")]
    [SerializeField] private XROrigin xrOrigin;

    [Header("Recenter Spawn Point")]
    [Tooltip("Target transform where the player should be recentered. If null, resets to initial position.")]
    [SerializeField] private Transform spawnPoint;

    private Vector3 _initialOriginPosition;
    private Quaternion _initialOriginRotation;

    private void Awake()
    {
        if (xrOrigin == null)
        {
            xrOrigin = FindFirstObjectByType<XROrigin>();
        }

        if (xrOrigin != null)
        {
            _initialOriginPosition = xrOrigin.transform.position;
            _initialOriginRotation = xrOrigin.transform.rotation;
        }
    }

    /// <summary>
    /// Recenters the player at the designated spawn point or initial position.
    /// </summary>
    public void RecenterPlayer()
    {
        if (xrOrigin == null) return;

        if (spawnPoint != null)
        {
            xrOrigin.MoveCameraToWorldLocation(spawnPoint.position);
            xrOrigin.MatchOriginUpCameraForward(spawnPoint.up, spawnPoint.forward);
        }
        else
        {
            xrOrigin.transform.position = _initialOriginPosition;
            xrOrigin.transform.rotation = _initialOriginRotation;
        }

        Debug.Log("[VRComfortManager] Player position recentered.");
    }
}
