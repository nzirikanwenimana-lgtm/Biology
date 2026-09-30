using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Runtime controller for the modern holographic Anatomy VR Dashboard.
/// Implements:
/// - 4x3 Card Grid for Anatomical Systems with visual selection and toggle states.
/// - Quick Action Bar: Isolate System, Hide Others, Reset View.
/// - Left Sidebar Navigation Tabs: Home, Systems, Layers, Search, Quiz, Settings.
/// - Bottom Info Status Bar: Quest 3 device info, live digital clock, and contextual hints.
/// - Direct integration with AnatomySystemController, QuizUI, and BodyPartInfoUI.
/// </summary>
[DisallowMultipleComponent]
public class AnatomyDashboardUI : MonoBehaviour
{
    public static AnatomyDashboardUI Instance { get; private set; }

    [System.Serializable]
    public class SystemCard
    {
        public string systemId;
        public string displayName;
        public Button cardButton;
        public Image cardBackground;
        public Image cardIcon;
        public Outline cardOutline;
        public Image cardGlowBorder;
        public TMP_Text labelText;
        [HideInInspector] public bool isSelected = false;
        [HideInInspector] public bool isVisible = true;
    }

    [Header("Controllers")]
    public AnatomySystemController systemController;
    public QuizUI quizUI;
    public BodyPartInfoUI infoUI;
    public AnatomyDashboardHandAffordance handAffordance;

    [Header("System Cards (4x3 Grid)")]
    public List<SystemCard> systemCards = new List<SystemCard>();

    [Header("Quick Action Buttons")]
    public Button isolateSystemButton;
    public Button hideOthersButton;
    public Button resetViewButton;
    public Button fitToViewButton;

    [Header("Navigation Tabs")]
    public Button homeTabButton;
    public Button systemsTabButton;
    public Button layersTabButton;
    public Button searchTabButton;
    public Button quizTabButton;
    public Button settingsTabButton;

    [Header("Tab Panels / Views")]
    public GameObject systemsPanel;
    public GameObject quizPanel;

    [Header("Status & Info Bar")]
    public TMP_Text timeText;
    public TMP_Text deviceStatusText;
    public TMP_Text hintBannerText;

    [Header("Styling Colors")]
    public Color normalCardBg = new Color(0.06f, 0.10f, 0.18f, 0.85f);
    public Color selectedBorderColor = new Color(0.18f, 0.76f, 0.98f, 1f); // Cyan Glow #2EC4FA
    public Color normalBorderColor = new Color(0.20f, 0.35f, 0.50f, 0.40f);
    public Color dimTextColor = new Color(0.60f, 0.72f, 0.85f, 1f);
    public Color activeTextColor = Color.white;

    private string _activeSelectedSystemId = "skin";
    private float _clockTimer = 0f;
    private bool _isLayersMode = false;

    private void Awake()
    {
        Instance = this;

        ConnectToBodyAnatomy();
        EnsureRuntimeInteractionsAndAffordance();

        BindNavigationTabs();
        BindSystemCards();
        BindActionButtons();
    }

    private void OnEnable()
    {
        BodyPartSelector.OnPartSelected += HandleBodyPartSelected;
    }

    private void OnDisable()
    {
        BodyPartSelector.OnPartSelected -= HandleBodyPartSelected;
    }

    private void Start()
    {
        UpdateClock();
        ConnectToBodyAnatomy();
        SelectSystemCard("skin");
        RefreshCardVisibilityStates();
    }

    /// <summary>
    /// Connects the UI Dashboard directly to the 3D Body Anatomy system controller and UI managers.
    /// </summary>
    public void ConnectToBodyAnatomy()
    {
        if (systemController == null)
            systemController = FindFirstObjectByType<AnatomySystemController>(FindObjectsInactive.Include);

        if (systemController == null)
        {
            // Check HumanBody or AnatomyDisplayStage
            GameObject humanBody = GameObject.Find("HumanBody");
            if (humanBody == null)
            {
                var stage = FindFirstObjectByType<AnatomyDisplayStage>(FindObjectsInactive.Include);
                if (stage != null && stage.humanBody != null)
                    humanBody = stage.humanBody.gameObject;
            }

            if (humanBody != null)
            {
                systemController = humanBody.GetComponent<AnatomySystemController>();
                if (systemController == null)
                    systemController = humanBody.AddComponent<AnatomySystemController>();
            }
        }

        if (systemController != null)
        {
            systemController.AutoFindLayers();
        }

        if (quizUI == null)
            quizUI = FindFirstObjectByType<QuizUI>(FindObjectsInactive.Include);

        if (infoUI == null)
            infoUI = FindFirstObjectByType<BodyPartInfoUI>(FindObjectsInactive.Include);

        if (handAffordance == null)
            handAffordance = GetComponent<AnatomyDashboardHandAffordance>();
    }

    private void HandleBodyPartSelected(BodyPartSelector part)
    {
        if (part == null) return;

        string sys = part.systemName ?? "";
        string matchedSystemId = MapSystemNameToId(sys);
        if (!string.IsNullOrEmpty(matchedSystemId))
        {
            SelectSystemCard(matchedSystemId);
            RefreshCardVisibilityStates();
        }

        string partName = part.GetDisplayName();
        if (!string.IsNullOrEmpty(sys))
            SetHint($"Inspecting: <b>{partName}</b> • <color=#2EC4FA>{sys}</color>");
        else
            SetHint($"Inspecting: <b>{partName}</b>");
    }

    private string MapSystemNameToId(string systemName)
    {
        if (string.IsNullOrEmpty(systemName)) return null;
        string s = systemName.ToLower();
        if (s.Contains("skelet") || s.Contains("bone")) return "skeleton";
        if (s.Contains("insert")) return "muscular_insertions";
        if (s.Contains("muscul") || s.Contains("muscle")) return "muscular";
        if (s.Contains("circulat") || s.Contains("cardio") || s.Contains("vascular") || s.Contains("aorta") || s.Contains("heart")) return "circulatory";
        if (s.Contains("nerv") || s.Contains("sensory") || s.Contains("brain")) return "nervous";
        if (s.Contains("joint") || s.Contains("articul") || s.Contains("ligament")) return "joints";
        if (s.Contains("skin") || s.Contains("integument") || s.Contains("body")) return "skin";
        if (s.Contains("viscer") || s.Contains("organ") || s.Contains("digest") || s.Contains("respir") || s.Contains("lung") || s.Contains("endocr") || s.Contains("renal") || s.Contains("kidney")) return "visceral";
        return null;
    }

    private void Update()
    {
        _clockTimer += Time.deltaTime;
        if (_clockTimer >= 1f)
        {
            _clockTimer = 0f;
            UpdateClock();
        }
    }

    private void UpdateClock()
    {
        if (timeText != null)
        {
            timeText.text = DateTime.Now.ToString("h:mm tt");
        }
    }

    private void BindNavigationTabs()
    {
        if (homeTabButton != null)
            homeTabButton.onClick.AddListener(() => SwitchToTab("systems"));

        if (systemsTabButton != null)
            systemsTabButton.onClick.AddListener(() => SwitchToTab("systems"));

        if (quizTabButton != null)
            quizTabButton.onClick.AddListener(() => SwitchToTab("quiz"));

        if (layersTabButton != null)
            layersTabButton.onClick.AddListener(() =>
            {
                SetHint("Layers view active. Toggle systems on the grid to filter visibility.");
                SwitchToTab("systems");
            });

        if (searchTabButton != null)
            searchTabButton.onClick.AddListener(() =>
            {
                SetHint("Point with VR ray and click any anatomical organ to inspect details.");
            });

        if (settingsTabButton != null)
            settingsTabButton.onClick.AddListener(() =>
            {
                SetHint("Anatomy VR - Meta Quest 3 / Room-Scale VR Mode.");
            });
    }

    public void SwitchToTab(string tabName)
    {
        _isLayersMode = string.Equals(tabName, "layers", StringComparison.OrdinalIgnoreCase);

        if (tabName == "quiz")
        {
            if (systemsPanel != null) systemsPanel.SetActive(false);
            if (quizPanel != null) quizPanel.SetActive(true);
            SetHint("Anatomy Quiz active. Test your knowledge with 4-choice questions!");
        }
        else
        {
            if (systemsPanel != null) systemsPanel.SetActive(true);
            if (quizPanel != null) quizPanel.SetActive(false);

            if (_isLayersMode)
                SetHint("Layers view active. Click any system card to toggle its visibility on the 3D body.");
            else
                SetHint("Select a system to begin exploring. Point with your controller and press trigger.");
        }
    }

    private void BindSystemCards()
    {
        foreach (var card in systemCards)
        {
            if (card == null || card.cardButton == null) continue;

            string id = card.systemId;
            card.cardButton.onClick.AddListener(() => OnSystemCardClicked(id));
        }
    }

    private void BindActionButtons()
    {
        if (isolateSystemButton != null)
            isolateSystemButton.onClick.AddListener(OnIsolateClicked);

        if (hideOthersButton != null)
            hideOthersButton.onClick.AddListener(OnHideOthersClicked);

        if (resetViewButton != null)
            resetViewButton.onClick.AddListener(OnResetViewClicked);

        if (fitToViewButton != null)
            fitToViewButton.onClick.AddListener(OnFitToViewClicked);
    }

    public void OnFitToViewClicked()
    {
        var manipulator = FindFirstObjectByType<AnatomyTransformManipulator>();
        if (manipulator != null)
        {
            manipulator.ResetTransform();
        }
        SetHint("Fit to View: Model position and scale centered to eye level.");
    }

    public void OnSystemCardClicked(string systemId)
    {
        SelectSystemCard(systemId);

        if (systemController == null)
            ConnectToBodyAnatomy();

        if (systemController != null)
        {
            if (_isLayersMode)
            {
                // In layers mode, toggle the clicked system
                systemController.ToggleSystem(systemId);
            }
            else
            {
                // Focus and show only this anatomical system on the 3D body
                systemController.ShowSystem(systemId);
            }
            RefreshCardVisibilityStates();
        }

        var card = systemCards.Find(c => string.Equals(c.systemId, systemId, StringComparison.OrdinalIgnoreCase));
        string name = card != null ? card.displayName : systemId;

        if (_isLayersMode)
        {
            bool isVis = systemController != null && systemController.IsSystemVisible(systemId);
            SetHint($"{name} is now {(isVis ? "Visible" : "Hidden")}. Click other cards to toggle.");
        }
        else
        {
            SetHint($"Viewing {name}. Click 'Show All' to restore full body.");
        }
    }

    public void SelectSystemCard(string systemId)
    {
        _activeSelectedSystemId = systemId;

        foreach (var card in systemCards)
        {
            bool match = string.Equals(card.systemId, systemId, StringComparison.OrdinalIgnoreCase);
            card.isSelected = match;

            if (card.cardOutline != null)
            {
                card.cardOutline.effectColor = match ? selectedBorderColor : normalBorderColor;
                card.cardOutline.effectDistance = match ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
            }

            if (card.cardBackground != null)
            {
                card.cardBackground.color = match ? new Color(0.12f, 0.26f, 0.46f, 0.95f) : normalCardBg;
            }

            if (card.labelText != null)
            {
                card.labelText.color = match ? activeTextColor : dimTextColor;
                card.labelText.fontStyle = match ? FontStyles.Bold : FontStyles.Normal;
            }
        }
    }

    public void OnIsolateClicked()
    {
        if (string.IsNullOrEmpty(_activeSelectedSystemId)) return;

        if (systemController != null)
        {
            systemController.ShowSystem(_activeSelectedSystemId);
            RefreshCardVisibilityStates();
        }

        var card = systemCards.Find(c => string.Equals(c.systemId, _activeSelectedSystemId, StringComparison.OrdinalIgnoreCase));
        string name = card != null ? card.displayName : _activeSelectedSystemId;
        SetHint($"Isolated {name}. All other anatomical systems hidden.");
    }

    public void OnHideOthersClicked()
    {
        OnIsolateClicked();
    }

    public void OnResetViewClicked()
    {
        if (systemController != null)
        {
            systemController.ShowAll();
            RefreshCardVisibilityStates();
        }

        SelectSystemCard("skin");
        SetHint("Show All: All anatomical layers and systems restored.");
    }

    public void RefreshCardVisibilityStates()
    {
        if (systemController == null) return;

        foreach (var card in systemCards)
        {
            bool isVis = systemController.IsSystemVisible(card.systemId);
            card.isVisible = isVis;

            if (card.cardBackground != null)
            {
                Color c = normalCardBg;
                c.a = isVis ? 0.85f : 0.35f;
                card.cardBackground.color = c;
            }

            if (card.cardIcon != null)
            {
                Color ic = card.cardIcon.color;
                ic.a = isVis ? 1f : 0.35f;
                card.cardIcon.color = ic;
            }
        }
    }

    public void SetHint(string message)
    {
        if (hintBannerText != null)
        {
            hintBannerText.text = message;
        }
    }

    private void EnsureRuntimeInteractionsAndAffordance()
    {
        // 1. Ensure CanvasGroup for affordance fading
        var cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        // 2. Ensure BoxCollider for physical poke collision
        var col = GetComponent<BoxCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(1240f, 800f, 10f);
            col.center = Vector3.zero;
        }

        // 3. Ensure Hand Affordance
        if (handAffordance == null)
        {
            handAffordance = GetComponent<AnatomyDashboardHandAffordance>();
            if (handAffordance == null)
                handAffordance = gameObject.AddComponent<AnatomyDashboardHandAffordance>();
        }

        if (handAffordance != null)
        {
            handAffordance.canvasGroup = cg;
            if (handAffordance.handAnchor == null)
            {
                handAffordance.AutoLocateLeftHandAnchor();
            }
            handAffordance.palmAxis = AnatomyDashboardHandAffordance.PalmAxisOption.Auto;
            handAffordance.palmOpenThreshold = 0.35f;
            handAffordance.palmCloseThreshold = 0.15f;
            handAffordance.requireGaze = true;
            handAffordance.gazeOpenThreshold = 0.40f;
            handAffordance.gazeCloseThreshold = 0.20f;
        }

        // 4. Ensure TrackedDeviceGraphicRaycaster on Canvas for dual Ray and Poke
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            var trackedRaycaster = parentCanvas.GetComponent<TrackedDeviceGraphicRaycaster>();
            if (trackedRaycaster == null)
            {
                trackedRaycaster = parentCanvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            }
            trackedRaycaster.ignoreReversedGraphics = false;
            trackedRaycaster.checkFor2DOcclusion = false;
            trackedRaycaster.checkFor3DOcclusion = false;
        }

        // 5. Ensure EventSystem is not child of canvas
        var ev = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
        if (ev != null && ev.transform.parent != null)
        {
            ev.transform.SetParent(null, true);
        }

        // 6. Ensure Poke Interactor on Right Hand
        EnsureRightHandPokeInteractor();
    }

    private void EnsureRightHandPokeInteractor()
    {
        var existingPoke = FindFirstObjectByType<XRPokeInteractor>(FindObjectsInactive.Include);
        if (existingPoke != null) return;

        // Locate Right Hand
        Transform rightHand = null;
        var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in allTransforms)
        {
            string lower = t.name.ToLower();
            if (lower.Equals("right hand") || (lower.Contains("right") && (lower.Contains("controller") || lower.Contains("hand")) && !lower.Contains("model") && !lower.Contains("ray") && !lower.Contains("canvas") && !lower.Contains("interactor")))
            {
                rightHand = t;
                break;
            }
        }

        if (rightHand != null)
        {
            GameObject pokeObj = new GameObject("Poke Interactor");
            pokeObj.transform.SetParent(rightHand, false);
            pokeObj.transform.localPosition = new Vector3(0.01f, -0.01f, 0.08f);
            pokeObj.transform.localRotation = Quaternion.identity;

            GameObject pokePoint = new GameObject("Poke Point");
            pokePoint.transform.SetParent(pokeObj.transform, false);
            pokePoint.transform.localPosition = Vector3.zero;

            var poke = pokeObj.AddComponent<XRPokeInteractor>();
            poke.attachTransform = pokePoint.transform;
            poke.enableUIInteraction = true;
            poke.clickUIOnDown = true;
            poke.requirePokeFilter = false;
            poke.pokeHoverRadius = 0.025f;
            poke.pokeSelectWidth = 0.030f;
            poke.pokeDepth = 0.080f;
            Debug.Log("[AnatomyDashboardUI] Dynamically created Poke Interactor on Right Hand: " + rightHand.name);
        }
    }
}
