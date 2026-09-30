using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// XR Hand / Controller Menu providing instant access to:
/// 1. ✋ Info Button (displays selected object's Name, Function, Description, Images/Animation).
/// 2. Hide / Show Layer Toggles (Skin, Muscles, Organs, Bones, Vessels, Nerves).
/// 3. Hide Selected Mesh button (peel away any specific structure).
/// 4. Fold / Unfold / Separate (Exploded view) controls.
/// 5. Scale Up / Down and Reset Transform buttons.
/// </summary>
public class AnatomyHandMenu : MonoBehaviour
{
    [Header("Menu Root & Anchor")]
    [Tooltip("The root Canvas or GameObject for the hand menu.")]
    public GameObject menuPanel;

    [Tooltip("Transform to follow (e.g. Left Hand Controller or Left Wrist). If null, can float in front of user.")]
    public Transform handAnchor;

    [Tooltip("Offset from the hand anchor.")]
    public Vector3 localOffset = new Vector3(0.08f, 0.05f, 0.05f);

    [Header("Gesture Activation")]
    [Tooltip("Open menu automatically when looking at the palm/hand.")]
    public bool openOnPalmLook = true;

    [Tooltip("Angle threshold (degrees) between hand palm-up and camera direction to trigger menu.")]
    public float palmLookAngleThreshold = 45f;

    [Header("System References")]
    public AnatomySystemController systemController;
    public AnatomyExplodedView explodedView;
    public AnatomyTransformManipulator transformManipulator;
    public BodyPartInfoUI infoUI;

    [Header("UI Buttons")]
    [Tooltip("✋ Info Button to toggle anatomical details for the selected object.")]
    public Button infoButton;
    public TMP_Text infoButtonLabel;

    [Tooltip("Button to hide only the currently selected anatomical mesh.")]
    public Button hideSelectedButton;

    [Tooltip("Button to restore all hidden layers and parts.")]
    public Button showAllButton;

    [Header("Layer Visibility Toggles")]
    public Button toggleSkinButton;
    public Button toggleMusclesButton;
    public Button toggleOrgansButton;
    public Button toggleSkeletonButton;
    public Button toggleCirculatoryButton;
    public Button toggleNervousButton;

    [Header("Fold / Unfold Layer Separation")]
    public Button unfoldButton;
    public Button foldButton;
    public Slider separationSlider;

    [Header("Scale & Height Controls")]
    public Button scaleUpButton;
    public Button scaleDownButton;
    public Button resetModelButton;
    [Tooltip("Calibrates player height to natural adult standing eye level.")]
    public Button calibrateHeightButton;

    [Header("Audio Feedback")]
    public AudioClip buttonClickSound;
    public AudioSource audioSource;

    private Camera _mainCamera;
    private bool _isMenuOpen = true;

    // Quest 3S Touch Plus Controller State Tracking
    private bool _prevLeftSecondary = false; // Y button
    private bool _prevLeftMenu = false;      // Menu button
    private bool _prevLeftPrimary = false;   // X button
    private bool _prevRightPrimary = false;  // A button

    private void Awake()
    {
        _mainCamera = Camera.main;

        if (menuPanel == null)
        {
            menuPanel = gameObject;
        }

        // Auto-find controllers if unassigned
        if (systemController == null)
            systemController = FindFirstObjectByType<AnatomySystemController>();

        if (explodedView == null)
            explodedView = FindFirstObjectByType<AnatomyExplodedView>();

        if (transformManipulator == null)
            transformManipulator = FindFirstObjectByType<AnatomyTransformManipulator>();

        if (infoUI == null)
            infoUI = FindFirstObjectByType<BodyPartInfoUI>();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        // Auto-find Quest 3S Left Hand / Controller anchor if not assigned
        if (handAnchor == null)
        {
            AutoFindQuestHandAnchor();
        }

        BindUIButtons();
    }

    private void AutoFindQuestHandAnchor()
    {
        string[] candidateNames = { "Left Controller", "LeftHand Controller", "LeftHand", "Left Base Controller", "LeftHand Anchor" };
        var allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var t in allTransforms)
        {
            string lower = t.name.ToLower();
            if (lower.Contains("left") && (lower.Contains("controller") || lower.Contains("hand")))
            {
                handAnchor = t;
                openOnPalmLook = true;
                Debug.Log($"[AnatomyHandMenu] Auto-attached Hand Menu to Meta Quest Left Controller: '{t.name}'");
                break;
            }
        }
    }

    private void Start()
    {
        UpdateStatusLabels();
    }

    private void Update()
    {
        // 1. Follow Hand Anchor if assigned
        if (handAnchor != null)
        {
            transform.position = handAnchor.position + handAnchor.TransformDirection(localOffset);
            transform.rotation = handAnchor.rotation;
        }

        // 2. Palm-up gesture detection
        if (openOnPalmLook && handAnchor != null && _mainCamera != null)
        {
            Vector3 handUp = handAnchor.up;
            Vector3 camToHand = (handAnchor.position - _mainCamera.transform.position).normalized;
            float angle = Vector3.Angle(handUp, -camToHand);

            bool shouldBeOpen = (angle < palmLookAngleThreshold);
            if (menuPanel.activeSelf != shouldBeOpen)
            {
                menuPanel.SetActive(shouldBeOpen);
            }
        }

        // 3. Meta Quest 3S Touch Plus Controller Inputs
        HandleQuest3SControllerInputs();

        // 4. Desktop / Testing Keyboard Shortcuts
        HandleDesktopShortcuts();
    }

    /// <summary>
    /// Checks native Meta Quest Touch Plus buttons (X, Y, Menu, A)
    /// </summary>
    private void HandleQuest3SControllerInputs()
    {
        // Left Controller: Y button (SecondaryButton) & Menu Button toggle menu; X button triggers Info
        var leftHand = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
        if (leftHand.isValid)
        {
            // Y Button: Toggle Hand Menu
            if (leftHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool yPressed))
            {
                if (yPressed && !_prevLeftSecondary)
                {
                    ToggleMenu();
                    SendQuestHaptic(leftHand, 0.4f, 0.06f);
                }
                _prevLeftSecondary = yPressed;
            }

            // Menu Button: Toggle Hand Menu
            if (leftHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool menuPressed))
            {
                if (menuPressed && !_prevLeftMenu)
                {
                    ToggleMenu();
                    SendQuestHaptic(leftHand, 0.4f, 0.06f);
                }
                _prevLeftMenu = menuPressed;
            }

            // X Button: ✋ Info Button
            if (leftHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool xPressed))
            {
                if (xPressed && !_prevLeftPrimary)
                {
                    OnInfoButtonPressed();
                    SendQuestHaptic(leftHand, 0.5f, 0.08f);
                }
                _prevLeftPrimary = xPressed;
            }
        }

        // Right Controller: A Button (PrimaryButton) triggers ✋ Info Button
        var rightHand = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
        if (rightHand.isValid)
        {
            if (rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool aPressed))
            {
                if (aPressed && !_prevRightPrimary)
                {
                    OnInfoButtonPressed();
                    SendQuestHaptic(rightHand, 0.5f, 0.08f);
                }
                _prevRightPrimary = aPressed;
            }
        }
    }

    private void SendQuestHaptic(UnityEngine.XR.InputDevice device, float amplitude, float duration)
    {
        if (device.isValid)
        {
            device.SendHapticImpulse(0u, amplitude, duration);
        }
    }

    private void BindUIButtons()
    {
        if (infoButton != null)
            infoButton.onClick.AddListener(OnInfoButtonPressed);

        if (hideSelectedButton != null)
            hideSelectedButton.onClick.AddListener(OnHideSelectedPressed);

        if (showAllButton != null)
            showAllButton.onClick.AddListener(OnShowAllPressed);

        // Layer toggles
        if (toggleSkinButton != null)
            toggleSkinButton.onClick.AddListener(() => ToggleLayer("skin"));

        if (toggleMusclesButton != null)
            toggleMusclesButton.onClick.AddListener(() => ToggleLayer("muscular"));

        if (toggleOrgansButton != null)
            toggleOrgansButton.onClick.AddListener(() => ToggleLayer("visceral"));

        if (toggleSkeletonButton != null)
            toggleSkeletonButton.onClick.AddListener(() => ToggleLayer("skeleton"));

        if (toggleCirculatoryButton != null)
            toggleCirculatoryButton.onClick.AddListener(() => ToggleLayer("circulatory"));

        if (toggleNervousButton != null)
            toggleNervousButton.onClick.AddListener(() => ToggleLayer("nervous"));

        // Exploded view
        if (unfoldButton != null)
            unfoldButton.onClick.AddListener(OnUnfoldPressed);

        if (foldButton != null)
            foldButton.onClick.AddListener(OnFoldPressed);

        if (separationSlider != null)
            separationSlider.onValueChanged.AddListener(OnSeparationSliderChanged);

        // Scale & Reset
        if (scaleUpButton != null)
            scaleUpButton.onClick.AddListener(OnScaleUpPressed);

        if (scaleDownButton != null)
            scaleDownButton.onClick.AddListener(OnScaleDownPressed);

        if (resetModelButton != null)
            resetModelButton.onClick.AddListener(OnResetModelPressed);

        if (calibrateHeightButton != null)
            calibrateHeightButton.onClick.AddListener(OnCalibrateHeightPressed);
    }

    // -------------------------------------------------------------------------
    // ✋ Info Button Action
    // -------------------------------------------------------------------------

    /// <summary>
    /// Student presses the ✋ Info button on hand/controller.
    /// Opens the detailed Info Panel about the selected object.
    /// </summary>
    public void OnInfoButtonPressed()
    {
        PlaySound();

        BodyPartSelector selected = BodyPartSelector.CurrentlySelected;

        if (selected != null)
        {
            selected.ShowInfo();
        }
        else if (infoUI != null)
        {
            infoUI.TogglePanel();
        }
    }

    // -------------------------------------------------------------------------
    // Hide / Show Actions
    // -------------------------------------------------------------------------

    /// <summary>
    /// Peels away / hides the currently selected anatomy mesh.
    /// </summary>
    public void OnHideSelectedPressed()
    {
        PlaySound();
        if (BodyPartSelector.CurrentlySelected != null)
        {
            BodyPartSelector.CurrentlySelected.HideThisPart();
            if (infoUI != null) infoUI.HidePanel();
        }
    }

    /// <summary>
    /// Restores all anatomical systems and parts to full visibility.
    /// </summary>
    public void OnShowAllPressed()
    {
        PlaySound();
        if (systemController != null)
        {
            systemController.ShowAll();
        }

        // Reactivate any inactive BodyPartSelectors
        BodyPartSelector[] allParts = Resources.FindObjectsOfTypeAll<BodyPartSelector>();
        foreach (var p in allParts)
        {
            if (p != null && !p.gameObject.activeSelf && p.gameObject.scene.isLoaded)
            {
                p.gameObject.SetActive(true);
            }
        }
    }

    public void ToggleLayer(string layerId)
    {
        PlaySound();
        if (systemController != null)
        {
            systemController.ToggleSystem(layerId);
        }
    }

    // -------------------------------------------------------------------------
    // Fold / Unfold / Separate Actions
    // -------------------------------------------------------------------------

    public void OnUnfoldPressed()
    {
        PlaySound();
        if (explodedView != null)
        {
            explodedView.UnfoldAll();
            if (separationSlider != null) separationSlider.SetValueWithoutNotify(1f);
        }
    }

    public void OnFoldPressed()
    {
        PlaySound();
        if (explodedView != null)
        {
            explodedView.FoldAll();
            if (separationSlider != null) separationSlider.SetValueWithoutNotify(0f);
        }
    }

    public void OnSeparationSliderChanged(float val)
    {
        if (explodedView != null)
        {
            explodedView.SetSeparation(val);
        }
    }

    // -------------------------------------------------------------------------
    // Scale & Reset Actions
    // -------------------------------------------------------------------------

    public void OnScaleUpPressed()
    {
        PlaySound();
        if (transformManipulator != null)
        {
            transformManipulator.ScaleUp(1.25f);
        }
    }

    public void OnScaleDownPressed()
    {
        PlaySound();
        if (transformManipulator != null)
        {
            transformManipulator.ScaleDown(0.8f);
        }
    }

    public void OnResetModelPressed()
    {
        PlaySound();
        if (transformManipulator != null)
        {
            transformManipulator.ResetTransform();
        }
        if (explodedView != null)
        {
            explodedView.FoldAll();
            if (separationSlider != null) separationSlider.SetValueWithoutNotify(0f);
        }
    }

    public void OnCalibrateHeightPressed()
    {
        PlaySound();
        if (VRPlayerHeightManager.Instance != null)
        {
            VRPlayerHeightManager.Instance.CalibrateStandingHeight();
        }
    }

    // -------------------------------------------------------------------------
    // Toggle Menu & Desktop Shortcuts
    // -------------------------------------------------------------------------

    public void ToggleMenu()
    {
        _isMenuOpen = !_isMenuOpen;
        if (menuPanel != null)
        {
            menuPanel.SetActive(_isMenuOpen);
        }
    }

    private void HandleDesktopShortcuts()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;

        // 'I' key: ✋ Info Button
        if (kb.iKey.wasPressedThisFrame)
        {
            OnInfoButtonPressed();
        }

        // 'M' key: Toggle Hand Menu
        if (kb.mKey.wasPressedThisFrame)
        {
            ToggleMenu();
        }

        // 'F' key: Toggle Fold / Unfold
        if (kb.fKey.wasPressedThisFrame && explodedView != null)
        {
            explodedView.ToggleFold();
        }

        // 'H' key: Hide Selected
        if (kb.hKey.wasPressedThisFrame)
        {
            OnHideSelectedPressed();
        }
#else
        if (Input.GetKeyDown(KeyCode.I)) OnInfoButtonPressed();
        if (Input.GetKeyDown(KeyCode.M)) ToggleMenu();
        if (Input.GetKeyDown(KeyCode.F) && explodedView != null) explodedView.ToggleFold();
        if (Input.GetKeyDown(KeyCode.H)) OnHideSelectedPressed();
#endif
    }

    private void PlaySound()
    {
        if (buttonClickSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(buttonClickSound, 0.6f);
        }
    }

    private void UpdateStatusLabels()
    {
        if (infoButtonLabel != null)
        {
            infoButtonLabel.text = "✋ Info";
        }
    }
}
