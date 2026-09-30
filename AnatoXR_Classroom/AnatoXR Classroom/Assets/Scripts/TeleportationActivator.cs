using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class TeleportationActivator : MonoBehaviour
{
    public XRRayInteractor teleportInteractor;
    public InputActionProperty teleportActivatorAction;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (teleportInteractor != null)
            teleportInteractor.gameObject.SetActive(false);

        if (teleportActivatorAction.action != null)
            teleportActivatorAction.action.performed += Action_performed;
    }

    private void OnDestroy()
    {
        if (teleportActivatorAction.action != null)
            teleportActivatorAction.action.performed -= Action_performed;
    }

    private void Action_performed(InputAction.CallbackContext obj)
    {
        if (teleportInteractor != null)
            teleportInteractor.gameObject.SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {
        if (teleportActivatorAction.action != null && teleportActivatorAction.action.WasReleasedThisFrame())
        {
            if (teleportInteractor != null)
                teleportInteractor.gameObject.SetActive(false);
        }
    }
}