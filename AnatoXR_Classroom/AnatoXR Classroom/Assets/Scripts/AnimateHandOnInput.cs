using UnityEngine;
using UnityEngine.InputSystem;

public class AnimateHandOnInput : MonoBehaviour
{
    [Header("Hand Inputs")]
    public InputActionProperty triggerValue;
    public InputActionProperty gripValue;
    public InputActionProperty pointValue;

    [Header("Hand Animator")]
    public Animator handAnimator;

    private void OnEnable()
    {
        if (triggerValue.action != null)
            triggerValue.action.Enable();

        if (gripValue.action != null)
            gripValue.action.Enable();

        if (pointValue.action != null)
            pointValue.action.Enable();
    }

    private void OnDisable()
    {
        if (triggerValue.action != null)
            triggerValue.action.Disable();

        if (gripValue.action != null)
            gripValue.action.Disable();

        if (pointValue.action != null)
            pointValue.action.Disable();
    }

    private void Awake()
    {
        if (handAnimator == null)
        {
            handAnimator = GetComponentInChildren<Animator>();
        }
    }

    private void Update()
    {
        if (handAnimator == null)
            return;

        // Read Trigger
        float trigger = 0f;

        if (triggerValue.action != null && triggerValue.action.enabled)
        {
            trigger = triggerValue.action.ReadValue<float>();
        }

        // Read Grip
        float grip = 0f;

        if (gripValue.action != null && gripValue.action.enabled)
        {
            grip = gripValue.action.ReadValue<float>();
        }

        // Read Point button/value
        float point = 0f;

        if (pointValue.action != null && pointValue.action.enabled)
        {
            point = pointValue.action.ReadValue<float>();
        }

        // Send values to Animator
        handAnimator.SetFloat("Trigger", trigger);
        handAnimator.SetFloat("Grip", grip);
        handAnimator.SetFloat("PointY", point);
    }
}