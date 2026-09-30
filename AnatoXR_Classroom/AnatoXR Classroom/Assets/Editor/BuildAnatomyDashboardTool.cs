using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Unity Editor Tool to construct or upgrade the scene's UI into the modern
/// Holographic Anatomy VR Dashboard matching the reference design:
/// - Header Bar: "ANATOMY VR" / "HUMAN ANATOMY INTERACTIVE LEARNING"
/// - Left Sidebar: Tabs (Home, Systems, Layers, Search, Quiz, Settings)
/// - Center Card: Dual Anatomy Silhouette / Model view
/// - Right Panel: "ANATOMICAL SYSTEMS" 4x3 Grid (12 System Cards with icons & labels)
/// - Quick Actions: Isolate System, Hide Others, Reset View
/// - Bottom Status Bar: Quest 3 & Clock + Instruction Hint Pill
/// </summary>
public static class BuildAnatomyDashboardTool
{
    private static readonly Color BgCanvasColor = new Color(0.04f, 0.07f, 0.13f, 0.88f);
    private static readonly Color CyanGlow = new Color(0.18f, 0.76f, 0.98f, 1f);      // #2EC4FA
    private static readonly Color CyanSub = new Color(0.35f, 0.82f, 1f, 0.80f);
    private static readonly Color DarkGlassCard = new Color(0.08f, 0.13f, 0.22f, 0.90f);
    private static readonly Color DarkGlassSidebar = new Color(0.05f, 0.09f, 0.16f, 0.92f);
    private static readonly Color BorderDefault = new Color(0.20f, 0.35f, 0.50f, 0.45f);
    private static readonly Color ActionButtonBg = new Color(0.10f, 0.18f, 0.30f, 0.90f);

    [MenuItem("Anatomy/UI/Build Modern AnatomyVR Grid Dashboard", false, 1)]
    public static void BuildModernDashboardMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Build Dashboard", "Cannot modify scene while in Play Mode. Please exit Play Mode first.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Build Dashboard", "Please open a scene first.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Build AnatomyVR Grid Dashboard");
        int group = Undo.GetCurrentGroup();

        // 1. Ensure procedural icon assets exist and are imported as Single Sprites
        EnsureSystemIconsExist();

        // 2. Locate Left Hand Model anchor
        Transform leftHandModel = FindLeftHandModel();

        // 3. Ensure Poke Interactor exists on controller for physical finger poke interaction
        EnsurePokeInteractorsInScene();

        // 4. Locate or create Main World Space Canvas anchored to Left Hand Model
        Canvas canvas = FindOrCreateDashboardCanvas(leftHandModel);

        // 5. Construct the complete Futuristic Dashboard Layout with Gaze & Palm Affordance
        GameObject dashboardRoot = BuildDashboardHierarchy(canvas, leftHandModel);

        // 6. Force immediate texture reload and assignment to cards
        RefreshIconsInScene();

        // 7. Connect UI Dashboard to 3D Body Anatomy
        SetupHandDashboardTool.ConnectDashboardToBodyAnatomy(false);

        Undo.CollapseUndoOperations(group);
        if (!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        EditorUtility.DisplayDialog(
            "AnatomyVR Dashboard Built",
            "The AnatomyVR Dashboard has been successfully anchored to your Left Hand Model!\n\n" +
            (leftHandModel != null 
                ? $"• Anchored to Left Hand Model: '{leftHandModel.name}'\n" 
                : "• Hand Model not found in scene: positioned in front of Camera (will auto-link to Left Hand at runtime)\n") +
            "• Dual XR Interaction: Point with Ray Interactor OR physically Poke with finger\n" +
            "• Gaze & Palm Affordance: Turns on when left palm points towards your head and gaze aligns\n" +
            "• Pin Feature: Click 'PIN VIEW' in header to pin dashboard floating in world space\n" +
            "• Desktop Shortcuts: Press [H] in Play Mode to toggle affordance, or [P] to pin.",
            "OK"
        );
    }

    [MenuItem("Anatomy/UI/Refresh Icons in Scene (Fix Blank / Blue Icons)", false, 2)]
    public static void RefreshIconsInScene()
    {
        EnsureSystemIconsExist();

        // Find existing dashboard in scene
        AnatomyDashboardUI dashboardUI = UnityEngine.Object.FindFirstObjectByType<AnatomyDashboardUI>(FindObjectsInactive.Include);
        if (dashboardUI == null)
        {
            Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas != null)
            {
                Transform dTrans = canvas.transform.Find("AnatomyVR_Dashboard");
                if (dTrans != null) dashboardUI = dTrans.GetComponent<AnatomyDashboardUI>();
            }
        }

        int updatedCount = 0;
        if (dashboardUI != null)
        {
            Undo.RecordObject(dashboardUI, "Refresh Dashboard Icons");

            if (dashboardUI.systemController == null)
            {
                dashboardUI.systemController = UnityEngine.Object.FindFirstObjectByType<AnatomySystemController>(FindObjectsInactive.Include);
                if (dashboardUI.systemController == null)
                    dashboardUI.systemController = dashboardUI.GetComponentInParent<AnatomySystemController>();
            }

            // Clean up any old raycast-blocking GlowBorder children and hook up Outline directly to card buttons
            foreach (var card in dashboardUI.systemCards)
            {
                if (card != null && card.cardButton != null)
                {
                    Transform oldBorder = card.cardButton.transform.Find("GlowBorder");
                    if (oldBorder != null)
                        Undo.DestroyObjectImmediate(oldBorder.gameObject);

                    Outline ol = card.cardButton.GetComponent<Outline>();
                    if (ol == null) ol = Undo.AddComponent<Outline>(card.cardButton.gameObject);
                    ol.effectColor = BorderDefault;
                    ol.effectDistance = new Vector2(1.5f, -1.5f);
                    card.cardOutline = ol;
                }
            }

            // Ensure all child labels don't intercept raycasts intended for buttons
            var allTMP = dashboardUI.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in allTMP)
            {
                t.raycastTarget = false;
            }

            // Mapping from card name / system ID to icon file
            var iconMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "skin", "icon_full_body" },
                { "skeleton", "icon_skeleton" },
                { "muscular", "icon_muscular" },
                { "muscular_insertions", "icon_insertions" },
                { "circulatory", "icon_circulatory" },
                { "nervous", "icon_nervous" },
                { "joints", "icon_joints" },
                { "visceral", "icon_digestive" },
                { "respiratory", "icon_respiratory" },
                { "digestive", "icon_digestive" },
                { "endocrine", "icon_endocrine" },
                { "renal", "icon_renal" },
                { "reproductive", "icon_reproductive" }
            };

            // Look through all SystemIcon images in the dashboard hierarchy
            Image[] allImages = dashboardUI.GetComponentsInChildren<Image>(true);
            foreach (var img in allImages)
            {
                // Disable raycast target on all decorative icons
                if (img.GetComponent<Button>() == null)
                {
                    img.raycastTarget = false;
                }

                if (img.name == "SystemIcon")
                {
                    Transform card = img.transform.parent;
                    if (card != null)
                    {
                        string cardName = card.name.Replace("Card_", "").Trim().ToLower();
                        // Also check systemCards list if possible
                        var matchedCard = dashboardUI.systemCards.Find(c => c.cardButton != null && c.cardButton.gameObject == card.gameObject);
                        string iconFileName = null;

                        if (matchedCard != null && !string.IsNullOrEmpty(matchedCard.displayName))
                        {
                            string lowerDisplay = matchedCard.displayName.ToLower();
                            if (lowerDisplay.Contains("skin") || lowerDisplay.Contains("full body")) iconFileName = "icon_full_body";
                            else if (lowerDisplay.Contains("skeleton")) iconFileName = "icon_skeleton";
                            else if (lowerDisplay.Contains("insertions")) iconFileName = "icon_insertions";
                            else if (lowerDisplay.Contains("muscular")) iconFileName = "icon_muscular";
                            else if (lowerDisplay.Contains("circulatory")) iconFileName = "icon_circulatory";
                            else if (lowerDisplay.Contains("respiratory")) iconFileName = "icon_respiratory";
                            else if (lowerDisplay.Contains("digestive")) iconFileName = "icon_digestive";
                            else if (lowerDisplay.Contains("endocrine")) iconFileName = "icon_endocrine";
                            else if (lowerDisplay.Contains("nervous")) iconFileName = "icon_nervous";
                            else if (lowerDisplay.Contains("renal")) iconFileName = "icon_renal";
                            else if (lowerDisplay.Contains("reproductive")) iconFileName = "icon_reproductive";
                            else if (lowerDisplay.Contains("joint")) iconFileName = "icon_joints";
                        }

                        if (string.IsNullOrEmpty(iconFileName) && iconMap.ContainsKey(cardName))
                        {
                            iconFileName = iconMap[cardName];
                        }

                        if (!string.IsNullOrEmpty(iconFileName))
                        {
                            Sprite loadedSprite = Resources.Load<Sprite>("AnatomyGridIcons/" + iconFileName);
                            if (loadedSprite == null)
                            {
                                // Fallback: load as asset from project
                                loadedSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/AnatomyGridIcons/" + iconFileName + ".png");
                            }

                            if (loadedSprite != null)
                            {
                                Undo.RecordObject(img, "Assign Sprite");
                                img.sprite = loadedSprite;
                                img.color = Color.white;
                                img.preserveAspect = true;
                                EditorUtility.SetDirty(img);
                                updatedCount++;
                            }
                        }
                    }
                }
                else if (img.name == "BodyPreviewGraphic")
                {
                    Sprite bodySprite = Resources.Load<Sprite>("AnatomyGridIcons/icon_full_body") ??
                                        AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/AnatomyGridIcons/icon_full_body.png");
                    if (bodySprite != null)
                    {
                        Undo.RecordObject(img, "Assign Body Sprite");
                        img.sprite = bodySprite;
                        img.color = Color.white;
                        img.preserveAspect = true;
                        EditorUtility.SetDirty(img);
                    }
                }
                else if (img.name == "TabIcon")
                {
                    Transform tab = img.transform.parent;
                    if (tab != null)
                    {
                        string tabTitle = tab.name.Replace("Tab_", "").ToLower();
                        Sprite s = null;
                        if (tabTitle.Contains("home"))
                            s = LoadSpriteWithFallbacks("icon_home", "tab_icon_home", "tab_ icon_home");
                        else if (tabTitle.Contains("system"))
                            s = LoadSpriteWithFallbacks("icon_systems", "tab_icon_systems");
                        else if (tabTitle.Contains("layer"))
                            s = LoadSpriteWithFallbacks("icon_layers", "tab_icon_layers");
                        else if (tabTitle.Contains("search"))
                            s = LoadSpriteWithFallbacks("icon_search", "tab_icon_search");
                        else if (tabTitle.Contains("quiz"))
                            s = LoadSpriteWithFallbacks("icon_quiz", "tab_icon_quiz");
                        else if (tabTitle.Contains("setting"))
                            s = LoadSpriteWithFallbacks("icon_settings", "tab_icon_settings");

                        if (s != null)
                        {
                            Undo.RecordObject(img, "Assign Tab Sprite");
                            img.sprite = s;
                            img.color = Color.white;
                            img.preserveAspect = true;
                            EditorUtility.SetDirty(img);
                            updatedCount++;
                        }
                    }
                }
                else if (img.name == "ActionIcon")
                {
                    Transform btn = img.transform.parent;
                    if (btn != null)
                    {
                        string btnName = btn.name.ToLower();
                        Sprite s = null;
                        if (btnName.Contains("show") || btnName.Contains("all") || btnName.Contains("isolate"))
                            s = LoadSpriteWithFallbacks("icon_show_all", "action_icon_show_all", "icon_isolate");
                        else if (btnName.Contains("hide") || btnName.Contains("other"))
                            s = LoadSpriteWithFallbacks("icon_hide_others", "action_icon_hide_others");
                        else if (btnName.Contains("reset"))
                            s = LoadSpriteWithFallbacks("icon_reset_view", "action_icon_reset_view", "icon_reset");
                        else if (btnName.Contains("fit"))
                            s = LoadSpriteWithFallbacks("icon_fit_to_view", "action_icon_fit_to_view", "icon_fit");

                        if (s != null)
                        {
                            Undo.RecordObject(img, "Assign Action Sprite");
                            img.sprite = s;
                            img.color = Color.white;
                            img.preserveAspect = true;
                            EditorUtility.SetDirty(img);
                            updatedCount++;
                        }
                    }
                }
            }

            // Ensure Canvas has TrackedDeviceGraphicRaycaster for dual Ray and Poke interaction
            Canvas canvas = dashboardUI.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                {
                    Undo.AddComponent<TrackedDeviceGraphicRaycaster>(canvas.gameObject);
                }

                // If Left Hand Model exists and canvas is not parented to it, re-parent to Left Hand Model
                Transform leftHand = FindLeftHandModel();
                if (leftHand != null && canvas.transform.parent != leftHand)
                {
                    Undo.SetTransformParent(canvas.transform, leftHand, "Parent Canvas to Left Hand Model");
                    canvas.transform.localPosition = new Vector3(-0.02f, 0.08f, 0.12f);
                    canvas.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
                    canvas.transform.localScale = Vector3.one * 0.00048f;
                }
            }

            // Ensure BoxCollider exists for physical poke interaction
            BoxCollider col = dashboardUI.GetComponent<BoxCollider>();
            if (col == null)
            {
                col = Undo.AddComponent<BoxCollider>(dashboardUI.gameObject);
                col.size = new Vector3(1240f, 800f, 10f);
                col.center = Vector3.zero;
            }

            // Ensure CanvasGroup exists
            CanvasGroup cg = dashboardUI.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = Undo.AddComponent<CanvasGroup>(dashboardUI.gameObject);
            }

            // Ensure AnatomyDashboardHandAffordance is present
            AnatomyDashboardHandAffordance affordance = dashboardUI.GetComponent<AnatomyDashboardHandAffordance>();
            if (affordance == null)
            {
                affordance = Undo.AddComponent<AnatomyDashboardHandAffordance>(dashboardUI.gameObject);
                affordance.handAnchor = FindLeftHandModel();
                affordance.canvasGroup = cg;
                affordance.palmAxis = AnatomyDashboardHandAffordance.PalmAxisOption.Up;
                affordance.palmOpenThreshold = 0.45f;
                affordance.palmCloseThreshold = 0.30f;
                affordance.requireGaze = true;
                affordance.gazeOpenThreshold = 0.50f;
                affordance.gazeCloseThreshold = 0.35f;
                dashboardUI.handAffordance = affordance;
            }

            // Ensure Poke Interactor exists on controller
            EnsurePokeInteractorsInScene();

            EditorUtility.SetDirty(dashboardUI.gameObject);
        }

        Debug.Log($"[BuildAnatomyDashboardTool] Updated {updatedCount} icon cards in the active scene.");
    }

    [MenuItem("Anatomy/UI/Upgrade Existing System Menu to 4-Column Card Grid", false, 3)]
    public static void UpgradeExistingMenuToGrid()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded) return;

        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Upgrade Menu", "No Canvas found in scene. Use 'Build Modern AnatomyVR Grid Dashboard' instead.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Upgrade Existing Menu to Grid");
        int group = Undo.GetCurrentGroup();

        EnsureSystemIconsExist();

        Transform existingPanel = canvas.transform.Find("SystemMenuPanel");
        if (existingPanel != null)
        {
            // Convert existing panel's layout
            VerticalLayoutGroup vlg = existingPanel.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) Undo.DestroyObjectImmediate(vlg);

            RectTransform prt = existingPanel.GetComponent<RectTransform>();
            prt.sizeDelta = new Vector2(640f, 680f);

            GridLayoutGroup glg = existingPanel.GetComponent<GridLayoutGroup>();
            if (glg == null) glg = Undo.AddComponent<GridLayoutGroup>(existingPanel.gameObject);
            glg.cellSize = new Vector2(135f, 120f);
            glg.spacing = new Vector2(12f, 12f);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 4;
            glg.childAlignment = TextAnchor.UpperCenter;

            // Re-style existing buttons inside it
            var buttons = existingPanel.GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                RectTransform brt = btn.GetComponent<RectTransform>();
                brt.sizeDelta = new Vector2(135f, 120f);
                LayoutElement le = btn.GetComponent<LayoutElement>();
                if (le != null) Undo.DestroyObjectImmediate(le);

                Image img = btn.GetComponent<Image>();
                if (img != null) img.color = DarkGlassCard;
            }
        }
        else
        {
            BuildDashboardHierarchy(canvas);
        }

        Undo.CollapseUndoOperations(group);
        if (!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }
        EditorUtility.DisplayDialog("Upgrade Complete", "Successfully converted systems menu to 4-column Grid Layout!", "OK");
    }

    private static Canvas FindOrCreateDashboardCanvas(Transform leftHandModel = null)
    {
        if (leftHandModel == null) leftHandModel = FindLeftHandModel();
        // Check for existing AnatomyVR_Dashboard_Canvas
        Canvas existingCanvas = null;
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            if (c.name.Equals("AnatomyVR_Dashboard_Canvas", StringComparison.OrdinalIgnoreCase))
            {
                existingCanvas = c;
                break;
            }
        }

        GameObject canvasGO;
        Canvas canvas;
        if (existingCanvas != null)
        {
            canvas = existingCanvas;
            canvasGO = existingCanvas.gameObject;
        }
        else
        {
            canvasGO = new GameObject("AnatomyVR_Dashboard_Canvas");
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Dashboard Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // Ensure TrackedDeviceGraphicRaycaster is attached for dual Ray and Poke interaction
        var trackedRaycaster = canvasGO.GetComponent<TrackedDeviceGraphicRaycaster>();
        if (trackedRaycaster == null)
        {
            trackedRaycaster = Undo.AddComponent<TrackedDeviceGraphicRaycaster>(canvasGO);
        }

        RectTransform rt = canvasGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1280f, 840f);
        rt.localScale = Vector3.one * 0.00048f; // Ergonomic ~0.61m width in VR

        if (leftHandModel != null)
        {
            Undo.SetTransformParent(canvasGO.transform, leftHandModel, "Parent Dashboard Canvas to Left Hand Model");
            rt.localPosition = new Vector3(-0.02f, 0.08f, 0.12f);
            rt.localRotation = Quaternion.Euler(35f, 0f, 0f);
        }
        else
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                rt.position = cam.transform.position + cam.transform.forward * 0.70f + cam.transform.up * -0.1f;
                rt.rotation = Quaternion.LookRotation(cam.transform.forward);
            }
            else
            {
                rt.position = new Vector3(-0.2f, 1.3f, 0.7f);
            }
        }

        return canvas;
    }

    private static GameObject BuildDashboardHierarchy(Canvas canvas, Transform leftHandModel = null)
    {
        if (leftHandModel == null) leftHandModel = FindLeftHandModel();
        // Deactivate or remove old SystemMenuPanel if present to avoid duplicate overlays
        Transform oldMenu = canvas.transform.Find("SystemMenuPanel");
        if (oldMenu != null)
        {
            Undo.DestroyObjectImmediate(oldMenu.gameObject);
        }

        Transform existingDashboard = canvas.transform.Find("AnatomyVR_Dashboard");
        if (existingDashboard != null)
        {
            Undo.DestroyObjectImmediate(existingDashboard.gameObject);
        }

        // 1. Root Dashboard Window Container
        GameObject dashRoot = CreateUIObject("AnatomyVR_Dashboard", canvas.transform);
        RectTransform dashRT = dashRoot.GetComponent<RectTransform>();
        dashRT.anchorMin = new Vector2(0.5f, 0.5f);
        dashRT.anchorMax = new Vector2(0.5f, 0.5f);
        dashRT.pivot     = new Vector2(0.5f, 0.5f);
        dashRT.anchoredPosition = Vector2.zero;
        dashRT.sizeDelta = new Vector2(1240f, 800f);

        Image dashBg = dashRoot.AddComponent<Image>();
        dashBg.color = BgCanvasColor;

        // Glowing cyan frame outline
        GameObject outlineObj = CreateUIObject("CyanGlowFrame", dashRoot.transform);
        RectTransform outlineRT = outlineObj.GetComponent<RectTransform>();
        outlineRT.anchorMin = Vector2.zero;
        outlineRT.anchorMax = Vector2.one;
        outlineRT.sizeDelta = Vector2.zero;
        Outline outline = outlineObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.18f, 0.76f, 0.98f, 0.40f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Add CanvasGroup for smooth affordance fading
        CanvasGroup canvasGroup = dashRoot.AddComponent<CanvasGroup>();

        // Add BoxCollider for physical poke collision and raycast occlusion
        BoxCollider boxCol = dashRoot.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(1240f, 800f, 10f);
        boxCol.center = Vector3.zero;

        // Add Runtime Dashboard UI Script
        AnatomyDashboardUI dashboardUI = dashRoot.AddComponent<AnatomyDashboardUI>();
        dashboardUI.systemController = UnityEngine.Object.FindFirstObjectByType<AnatomySystemController>();
        dashboardUI.ConnectToBodyAnatomy();

        // Add Gaze & Palm Affordance System
        AnatomyDashboardHandAffordance affordance = dashRoot.AddComponent<AnatomyDashboardHandAffordance>();
        affordance.handAnchor = leftHandModel;
        affordance.canvasGroup = canvasGroup;
        affordance.palmAxis = AnatomyDashboardHandAffordance.PalmAxisOption.Up;
        affordance.palmOpenThreshold = 0.45f;
        affordance.palmCloseThreshold = 0.30f;
        affordance.requireGaze = true;
        affordance.gazeOpenThreshold = 0.50f;
        affordance.gazeCloseThreshold = 0.35f;
        dashboardUI.handAffordance = affordance;

        // 2. Top Header Bar (with Pin button)
        BuildHeaderBar(dashRoot.transform, affordance);

        // 3. Middle Content Container (Sidebar + Center Silhouette + Right Grid)
        GameObject contentRow = CreateUIObject("DashboardContentRow", dashRoot.transform);
        RectTransform rowRT = contentRow.GetComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0f, 0f);
        rowRT.anchorMax = new Vector2(1f, 1f);
        rowRT.offsetMin = new Vector2(24f, 70f);   // bottom margin for status bar
        rowRT.offsetMax = new Vector2(-24f, -70f); // top margin for header
        HorizontalLayoutGroup rowHLG = contentRow.AddComponent<HorizontalLayoutGroup>();
        rowHLG.spacing = 18f;
        rowHLG.childControlWidth = false;
        rowHLG.childControlHeight = true;
        rowHLG.childForceExpandWidth = false;
        rowHLG.childForceExpandHeight = true;

        // 4. Left Sidebar Navigation Rail (Width: 130px)
        BuildSidebarRail(contentRow.transform, dashboardUI);

        // 5. Center Silhouette / Anatomy Graphic Card (Width: 320px)
        BuildCenterFigureCard(contentRow.transform);

        // 6. Right Section: "ANATOMICAL SYSTEMS" 4x3 Grid + Quick Actions (Width: 680px)
        BuildRightGridSection(contentRow.transform, dashboardUI);

        // 7. Bottom Status & Info Bar
        BuildBottomStatusBar(dashRoot.transform, dashboardUI);

        return dashRoot;
    }

    private static void BuildHeaderBar(Transform parent, AnatomyDashboardHandAffordance affordance)
    {
        GameObject headerObj = CreateUIObject("HeaderBar", parent);
        RectTransform hRT = headerObj.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0f, 1f);
        hRT.anchorMax = new Vector2(1f, 1f);
        hRT.pivot     = new Vector2(0.5f, 1f);
        hRT.anchoredPosition = new Vector2(0f, -16f);
        hRT.sizeDelta = new Vector2(-48f, 50f);

        // Left Header: ANATOMY VR
        GameObject leftTitle = CreateUIObject("LeftTitle", headerObj.transform);
        RectTransform ltRT = leftTitle.GetComponent<RectTransform>();
        ltRT.anchorMin = new Vector2(0f, 0.5f);
        ltRT.anchorMax = new Vector2(0f, 0.5f);
        ltRT.pivot     = new Vector2(0f, 0.5f);
        ltRT.anchoredPosition = new Vector2(10f, 0f);
        ltRT.sizeDelta = new Vector2(360f, 48f);

        TextMeshProUGUI tmpLeft = leftTitle.AddComponent<TextMeshProUGUI>();
        tmpLeft.raycastTarget = false;
        tmpLeft.text = "<size=24><b>ANATOMY</b><color=#2EC4FA>VR</color></size>\n<size=11><color=#7AA2C8>EXPLORE  •  LEARN  •  UNDERSTAND</color></size>";
        tmpLeft.alignment = TextAlignmentOptions.MidlineLeft;

        // Right Header: HUMAN ANATOMY
        GameObject rightTitle = CreateUIObject("RightTitle", headerObj.transform);
        RectTransform rtRT = rightTitle.GetComponent<RectTransform>();
        rtRT.anchorMin = new Vector2(1f, 0.5f);
        rtRT.anchorMax = new Vector2(1f, 0.5f);
        rtRT.pivot     = new Vector2(1f, 0.5f);
        rtRT.anchoredPosition = new Vector2(-155f, 0f);
        rtRT.sizeDelta = new Vector2(260f, 48f);

        TextMeshProUGUI tmpRight = rightTitle.AddComponent<TextMeshProUGUI>();
        tmpRight.raycastTarget = false;
        tmpRight.text = "<size=15><b><color=#E0EEFF>HUMAN ANATOMY</color></b></size>\n<size=11><color=#5B8BB8>INTERACTIVE LEARNING</color></size>";
        tmpRight.alignment = TextAlignmentOptions.MidlineRight;

        // Pin Button: allows student to pin the dashboard in space
        GameObject pinObj = CreateUIObject("PinButton", headerObj.transform);
        RectTransform pinRT = pinObj.GetComponent<RectTransform>();
        pinRT.anchorMin = new Vector2(1f, 0.5f);
        pinRT.anchorMax = new Vector2(1f, 0.5f);
        pinRT.pivot     = new Vector2(1f, 0.5f);
        pinRT.anchoredPosition = new Vector2(-10f, 0f);
        pinRT.sizeDelta = new Vector2(130f, 36f);

        Image pinBg = pinObj.AddComponent<Image>();
        pinBg.color = ActionButtonBg;
        pinBg.raycastTarget = true;

        Outline pinOutline = pinObj.AddComponent<Outline>();
        pinOutline.effectColor = BorderDefault;
        pinOutline.effectDistance = new Vector2(1.5f, -1.5f);

        Button pinBtn = pinObj.AddComponent<Button>();

        GameObject pinTextObj = CreateUIObject("PinLabel", pinObj.transform);
        RectTransform ptRT = pinTextObj.GetComponent<RectTransform>();
        ptRT.anchorMin = Vector2.zero;
        ptRT.anchorMax = Vector2.one;
        ptRT.sizeDelta = Vector2.zero;

        TextMeshProUGUI pinTMP = pinTextObj.AddComponent<TextMeshProUGUI>();
        pinTMP.raycastTarget = false;
        pinTMP.text = "<size=13><b><color=#2EC4FA>PIN</color> VIEW</b></size>";
        pinTMP.alignment = TextAlignmentOptions.Center;

        if (affordance != null)
        {
            affordance.pinButton = pinBtn;
            affordance.pinIcon = pinBg;
        }
    }

    public static Sprite LoadSpriteWithFallbacks(params string[] candidateNames)
    {
        foreach (var name in candidateNames)
        {
            if (string.IsNullOrEmpty(name)) continue;
            Sprite s = Resources.Load<Sprite>("AnatomyGridIcons/" + name);
            if (s != null) return s;
            s = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/AnatomyGridIcons/" + name + ".png");
            if (s != null) return s;
        }
        return null;
    }

    private static void BuildSidebarRail(Transform parent, AnatomyDashboardUI ui)
    {
        GameObject sidebarObj = CreateUIObject("SidebarRail", parent);
        RectTransform sRT = sidebarObj.GetComponent<RectTransform>();
        sRT.sizeDelta = new Vector2(150f, 0f);

        Image sBg = sidebarObj.AddComponent<Image>();
        sBg.color = DarkGlassSidebar;

        VerticalLayoutGroup vlg = sidebarObj.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(10, 10, 16, 16);
        vlg.spacing = 10f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ui.homeTabButton     = CreateSidebarTab(sidebarObj.transform, "Home",     new[] { "icon_home", "tab_icon_home", "tab_ icon_home" }, true);
        ui.systemsTabButton  = CreateSidebarTab(sidebarObj.transform, "Systems",  new[] { "icon_systems", "tab_icon_systems" },             false);
        ui.layersTabButton   = CreateSidebarTab(sidebarObj.transform, "Layers",   new[] { "icon_layers", "tab_icon_layers" },               false);
        ui.searchTabButton   = CreateSidebarTab(sidebarObj.transform, "Search",   new[] { "icon_search", "tab_icon_search" },               false);
        ui.quizTabButton     = CreateSidebarTab(sidebarObj.transform, "Quiz",     new[] { "icon_quiz", "tab_icon_quiz" },                   false);
        ui.settingsTabButton = CreateSidebarTab(sidebarObj.transform, "Settings", new[] { "icon_settings", "tab_icon_settings" },         false);
    }

    private static Button CreateSidebarTab(Transform parent, string title, string[] candidateIconNames, bool isActive)
    {
        GameObject tabObj = CreateUIObject("Tab_" + title, parent);
        RectTransform tRT = tabObj.GetComponent<RectTransform>();
        tRT.sizeDelta = new Vector2(130f, 44f);

        Image img = tabObj.AddComponent<Image>();
        img.color = isActive ? new Color(0.12f, 0.40f, 0.70f, 0.95f) : new Color(0.10f, 0.16f, 0.25f, 0.70f);

        Button btn = tabObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0.20f, 0.55f, 0.90f, 1f);
        cb.pressedColor = CyanGlow;
        btn.colors = cb;

        // Glowing cyan outline on active tab
        if (isActive)
        {
            Outline tabOutline = tabObj.AddComponent<Outline>();
            tabOutline.effectColor = new Color(0.18f, 0.76f, 0.98f, 0.85f);
            tabOutline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        // Tab Icon on the left
        GameObject iconGO = CreateUIObject("TabIcon", tabObj.transform);
        RectTransform iRT = iconGO.GetComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 0.5f);
        iRT.anchorMax = new Vector2(0f, 0.5f);
        iRT.pivot     = new Vector2(0f, 0.5f);
        iRT.anchoredPosition = new Vector2(8f, 0f);
        iRT.sizeDelta = new Vector2(28f, 28f);

        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.raycastTarget = false;
        Sprite iconSprite = LoadSpriteWithFallbacks(candidateIconNames);

        if (iconSprite != null)
        {
            iconImg.sprite = iconSprite;
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
        }

        // Tab Text on the right
        GameObject textObj = CreateUIObject("Label", tabObj.transform);
        RectTransform textRT = textObj.GetComponent<RectTransform>();
        textRT.anchorMin = new Vector2(0f, 0f);
        textRT.anchorMax = new Vector2(1f, 1f);
        textRT.offsetMin = new Vector2(44f, 0f);
        textRT.offsetMax = new Vector2(-6f, 0f);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.raycastTarget = false;
        tmp.text = title;
        tmp.fontSize = 13;
        tmp.fontStyle = isActive ? FontStyles.Bold : FontStyles.Normal;
        tmp.color = isActive ? Color.white : new Color(0.70f, 0.82f, 0.95f, 1f);
        tmp.alignment = TextAlignmentOptions.MidlineLeft;

        return btn;
    }

    private static void BuildCenterFigureCard(Transform parent)
    {
        GameObject cardObj = CreateUIObject("CenterFigureCard", parent);
        RectTransform cRT = cardObj.GetComponent<RectTransform>();
        cRT.sizeDelta = new Vector2(320f, 0f);

        Image cBg = cardObj.AddComponent<Image>();
        cBg.color = DarkGlassCard;

        // Try load anatomy preview sprite if exists, otherwise display styled placeholder
        GameObject imgObj = CreateUIObject("BodyPreviewGraphic", cardObj.transform);
        RectTransform imgRT = imgObj.GetComponent<RectTransform>();
        imgRT.anchorMin = Vector2.zero;
        imgRT.anchorMax = Vector2.one;
        imgRT.offsetMin = new Vector2(12f, 12f);
        imgRT.offsetMax = new Vector2(-12f, -12f);

        Image previewImg = imgObj.AddComponent<Image>();
        Sprite humanSprite = Resources.Load<Sprite>("AnatomyGridIcons/icon_full_body");
        if (humanSprite != null)
        {
            previewImg.sprite = humanSprite;
            previewImg.preserveAspect = true;
            previewImg.color = Color.white;
        }
        else
        {
            previewImg.color = new Color(0.12f, 0.22f, 0.35f, 0.40f);
        }

        // Subtitle badge inside card
        GameObject badgeObj = CreateUIObject("BadgeText", cardObj.transform);
        RectTransform bRT = badgeObj.GetComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0f, 0f);
        bRT.anchorMax = new Vector2(1f, 0f);
        bRT.pivot     = new Vector2(0.5f, 0f);
        bRT.anchoredPosition = new Vector2(0f, 10f);
        bRT.sizeDelta = new Vector2(0f, 28f);

        TextMeshProUGUI badgeTMP = badgeObj.AddComponent<TextMeshProUGUI>();
        badgeTMP.text = "<size=10><color=#5A88B4>DUAL LAYER REALTIME VISUALIZATION</color></size>";
        badgeTMP.alignment = TextAlignmentOptions.Center;
    }

    private static void BuildRightGridSection(Transform parent, AnatomyDashboardUI ui)
    {
        GameObject rightSection = CreateUIObject("SystemsSection", parent);
        RectTransform rsRT = rightSection.GetComponent<RectTransform>();
        rsRT.sizeDelta = new Vector2(690f, 0f);

        VerticalLayoutGroup sVLG = rightSection.AddComponent<VerticalLayoutGroup>();
        sVLG.padding = new RectOffset(6, 6, 4, 4);
        sVLG.spacing = 10f;
        sVLG.childControlWidth = true;
        sVLG.childControlHeight = false;
        sVLG.childForceExpandWidth = true;
        sVLG.childForceExpandHeight = false;

        ui.systemsPanel = rightSection;

        // Section Title: ANATOMICAL SYSTEMS
        GameObject titleObj = CreateUIObject("SectionHeader", rightSection.transform);
        RectTransform tRT = titleObj.GetComponent<RectTransform>();
        tRT.sizeDelta = new Vector2(0f, 32f);

        TextMeshProUGUI tmpTitle = titleObj.AddComponent<TextMeshProUGUI>();
        tmpTitle.text = "<size=17><b><color=#2EC4FA>ANATOMICAL SYSTEMS</color></b></size>";
        tmpTitle.alignment = TextAlignmentOptions.MidlineLeft;

        // The 4x3 Grid Container
        GameObject gridObj = CreateUIObject("CardGrid_4x3", rightSection.transform);
        RectTransform gRT = gridObj.GetComponent<RectTransform>();
        gRT.sizeDelta = new Vector2(0f, 410f);

        GridLayoutGroup glg = gridObj.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(158f, 122f);
        glg.spacing = new Vector2(12f, 12f);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 4;
        glg.childAlignment = TextAnchor.UpperCenter;

        // Define 12 systems exactly from the target mockup
        var systemDefs = new[]
        {
            new { id = "skin",                name = "Full Body\n/ Skin",           icon = "icon_full_body" },
            new { id = "skeleton",            name = "Skeletal\nSystem",            icon = "icon_skeleton" },
            new { id = "muscular",            name = "Muscular\nSystem",            icon = "icon_muscular" },
            new { id = "muscular_insertions", name = "Muscular\nInsertions",        icon = "icon_insertions" },
            new { id = "circulatory",         name = "Circulatory\nSystem",         icon = "icon_circulatory" },
            new { id = "visceral",            name = "Respiratory\nSystem",         icon = "icon_respiratory" },
            new { id = "visceral",            name = "Digestive\nSystem",           icon = "icon_digestive" },
            new { id = "visceral",            name = "Endocrine\nSystem",           icon = "icon_endocrine" },
            new { id = "nervous",             name = "Nervous &\nSensory",          icon = "icon_nervous" },
            new { id = "visceral",            name = "Renal\nSystem",               icon = "icon_renal" },
            new { id = "visceral",            name = "Reproductive\nSystem",        icon = "icon_reproductive" },
            new { id = "joints",              name = "Joints &\nArticulations",     icon = "icon_joints" }
        };

        ui.systemCards.Clear();

        foreach (var def in systemDefs)
        {
            AnatomyDashboardUI.SystemCard card = CreateSystemCard(gridObj.transform, def.id, def.name, def.icon);
            ui.systemCards.Add(card);
        }

        // Quick Action Buttons Bar (Isolate System, Hide Others, Reset View)
        BuildQuickActionsBar(rightSection.transform, ui);
    }

    private static AnatomyDashboardUI.SystemCard CreateSystemCard(Transform gridParent, string systemId, string displayName, string iconName)
    {
        AnatomyDashboardUI.SystemCard card = new AnatomyDashboardUI.SystemCard();
        card.systemId = systemId;
        card.displayName = displayName.Replace("\n", " ");

        GameObject cardGO = CreateUIObject("Card_" + systemId, gridParent);
        RectTransform cRT = cardGO.GetComponent<RectTransform>();
        cRT.sizeDelta = new Vector2(158f, 122f);

        Image bgImg = cardGO.AddComponent<Image>();
        bgImg.color = DarkGlassCard;
        card.cardBackground = bgImg;

        Button btn = cardGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0.18f, 0.35f, 0.55f, 1f);
        cb.pressedColor = CyanGlow;
        btn.colors = cb;
        card.cardButton = btn;

        // Glowing border / Outline directly on button card
        Outline outline = cardGO.AddComponent<Outline>();
        outline.effectColor = BorderDefault;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        card.cardOutline = outline;

        // Icon Graphic
        GameObject iconGO = CreateUIObject("SystemIcon", cardGO.transform);
        RectTransform iRT = iconGO.GetComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0.5f, 1f);
        iRT.anchorMax = new Vector2(0.5f, 1f);
        iRT.pivot     = new Vector2(0.5f, 1f);
        iRT.anchoredPosition = new Vector2(0f, -10f);
        iRT.sizeDelta = new Vector2(58f, 54f);

        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.raycastTarget = false; // Never block button click
        Sprite iconSprite = Resources.Load<Sprite>("AnatomyGridIcons/" + iconName);
        if (iconSprite != null)
        {
            iconImg.sprite = iconSprite;
            iconImg.preserveAspect = true;
            iconImg.color = Color.white;
        }
        else
        {
            iconImg.color = new Color(0.25f, 0.65f, 0.95f, 0.75f);
        }
        card.cardIcon = iconImg;

        // System Label
        GameObject labelGO = CreateUIObject("SystemLabel", cardGO.transform);
        RectTransform lRT = labelGO.GetComponent<RectTransform>();
        lRT.anchorMin = new Vector2(0f, 0f);
        lRT.anchorMax = new Vector2(1f, 0f);
        lRT.pivot     = new Vector2(0.5f, 0f);
        lRT.anchoredPosition = new Vector2(0f, 6f);
        lRT.sizeDelta = new Vector2(0f, 44f);

        TextMeshProUGUI tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.raycastTarget = false; // Never block button click
        tmp.text = displayName;
        tmp.fontSize = 11;
        tmp.color = new Color(0.85f, 0.92f, 1f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.lineSpacing = -15f;
        card.labelText = tmp;

        return card;
    }

    private static void BuildQuickActionsBar(Transform parent, AnatomyDashboardUI ui)
    {
        GameObject barGO = CreateUIObject("QuickActionsBar", parent);
        RectTransform bRT = barGO.GetComponent<RectTransform>();
        bRT.sizeDelta = new Vector2(0f, 54f);

        HorizontalLayoutGroup hlg = barGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 14f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        ui.isolateSystemButton = CreateActionButton(barGO.transform, "Show All",    "Restore all layers",  new[] { "icon_show_all", "action_icon_show_all", "icon_isolate" });
        ui.hideOthersButton    = CreateActionButton(barGO.transform, "Hide Others", "Focus on selection", new[] { "icon_hide_others", "action_icon_hide_others" });
        ui.resetViewButton     = CreateActionButton(barGO.transform, "Reset View",  "Reset model layers",  new[] { "icon_reset_view", "action_icon_reset_view", "icon_reset" });
        ui.fitToViewButton     = CreateActionButton(barGO.transform, "Fit to View", "Center to eye level", new[] { "icon_fit_to_view", "action_icon_fit_to_view", "icon_fit" });
    }

    private static Button CreateActionButton(Transform parent, string title, string sub, string[] candidateIconNames)
    {
        GameObject btnObj = CreateUIObject("Btn_" + title.Replace(" ", ""), parent);
        Image bg = btnObj.AddComponent<Image>();
        bg.color = ActionButtonBg;

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0.20f, 0.45f, 0.75f, 1f);
        cb.pressedColor = CyanGlow;
        btn.colors = cb;

        // Action Icon on the left
        GameObject iconGO = CreateUIObject("ActionIcon", btnObj.transform);
        RectTransform iRT = iconGO.GetComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 0.5f);
        iRT.anchorMax = new Vector2(0f, 0.5f);
        iRT.pivot     = new Vector2(0f, 0.5f);
        iRT.anchoredPosition = new Vector2(8f, 0f);
        iRT.sizeDelta = new Vector2(24f, 24f);

        Image iconImg = iconGO.AddComponent<Image>();
        iconImg.raycastTarget = false; // Never block button click
        Sprite iconSprite = LoadSpriteWithFallbacks(candidateIconNames);
        if (iconSprite != null)
        {
            iconImg.sprite = iconSprite;
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
        }

        GameObject textObj = CreateUIObject("Text", btnObj.transform);
        RectTransform tRT = textObj.GetComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0f, 0f);
        tRT.anchorMax = new Vector2(1f, 1f);
        tRT.offsetMin = new Vector2(36f, 0f);
        tRT.offsetMax = new Vector2(-4f, 0f);

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.raycastTarget = false; // Never block button click
        tmp.text = $"<b><size=12><color=#E0F0FF>{title}</color></size></b>\n<size=8><color=#6E9CC8>{sub}</color></size>";
        tmp.alignment = TextAlignmentOptions.MidlineLeft;

        return btn;
    }

    private static void BuildBottomStatusBar(Transform parent, AnatomyDashboardUI ui)
    {
        GameObject statusObj = CreateUIObject("BottomStatusBar", parent);
        RectTransform sRT = statusObj.GetComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0f, 0f);
        sRT.anchorMax = new Vector2(1f, 0f);
        sRT.pivot     = new Vector2(0.5f, 0f);
        sRT.anchoredPosition = new Vector2(0f, 16f);
        sRT.sizeDelta = new Vector2(-48f, 44f);

        // Left: Time & Device
        GameObject leftInfo = CreateUIObject("LeftInfoPill", statusObj.transform);
        RectTransform lRT = leftInfo.GetComponent<RectTransform>();
        lRT.anchorMin = new Vector2(0f, 0.5f);
        lRT.anchorMax = new Vector2(0f, 0.5f);
        lRT.pivot     = new Vector2(0f, 0.5f);
        lRT.anchoredPosition = new Vector2(0f, 0f);
        lRT.sizeDelta = new Vector2(140f, 40f);

        Image pillBg = leftInfo.AddComponent<Image>();
        pillBg.color = DarkGlassSidebar;

        GameObject leftTextObj = CreateUIObject("TimeDeviceText", leftInfo.transform);
        RectTransform ltRT = leftTextObj.GetComponent<RectTransform>();
        ltRT.anchorMin = Vector2.zero;
        ltRT.anchorMax = Vector2.one;
        ltRT.sizeDelta = Vector2.zero;

        TextMeshProUGUI lTMP = leftTextObj.AddComponent<TextMeshProUGUI>();
        lTMP.text = "<size=11><b>10:42 AM</b>\n<color=#6A95BD>Quest 3</color></size>";
        lTMP.alignment = TextAlignmentOptions.Center;
        ui.timeText = lTMP;

        // Center Hint Pill
        GameObject centerHint = CreateUIObject("HintBannerPill", statusObj.transform);
        RectTransform cRT = centerHint.GetComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0.5f, 0.5f);
        cRT.anchorMax = new Vector2(0.5f, 0.5f);
        cRT.pivot     = new Vector2(0.5f, 0.5f);
        cRT.anchoredPosition = new Vector2(60f, 0f);
        cRT.sizeDelta = new Vector2(560f, 40f);

        Image hBg = centerHint.AddComponent<Image>();
        hBg.color = new Color(0.06f, 0.12f, 0.22f, 0.90f);

        GameObject hintTextObj = CreateUIObject("HintText", centerHint.transform);
        RectTransform htRT = hintTextObj.GetComponent<RectTransform>();
        htRT.anchorMin = Vector2.zero;
        htRT.anchorMax = Vector2.one;
        htRT.sizeDelta = Vector2.zero;

        TextMeshProUGUI hTMP = hintTextObj.AddComponent<TextMeshProUGUI>();
        hTMP.text = "<size=12><color=#2EC4FA><b>(i) Select a system to begin exploring</b></color>\n<size=10><color=#8AB4DA>Point with your controller and press trigger to select</color></size></size>";
        hTMP.alignment = TextAlignmentOptions.Center;
        ui.hintBannerText = hTMP;

        // Right Slogan
        GameObject sloganObj = CreateUIObject("RightSlogan", statusObj.transform);
        RectTransform slgRT = sloganObj.GetComponent<RectTransform>();
        slgRT.anchorMin = new Vector2(1f, 0.5f);
        slgRT.anchorMax = new Vector2(1f, 0.5f);
        slgRT.pivot     = new Vector2(1f, 0.5f);
        slgRT.anchoredPosition = new Vector2(0f, 0f);
        slgRT.sizeDelta = new Vector2(200f, 40f);

        TextMeshProUGUI sTMP = sloganObj.AddComponent<TextMeshProUGUI>();
        sTMP.text = "<i><color=#38BDF8><size=13>Knowledge\nBrings Life Closer</size></color></i>";
        sTMP.alignment = TextAlignmentOptions.MidlineRight;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return go;
    }

    public static Transform FindLeftHandModel()
    {
        var allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        
        // Priority 1: GameObject tagged 'Left Hand' or named 'Left Hand' (Tracked XR Root with clean axes)
        foreach (var t in allTransforms)
        {
            if (t.CompareTag("Left Hand") || t.name.Equals("Left Hand", StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }
        }

        // Priority 2: Left Hand Model prefab instance (Oculus Hands)
        foreach (var t in allTransforms)
        {
            string lower = t.name.ToLower();
            if (lower.Contains("left") && lower.Contains("hand") && lower.Contains("model") && !lower.Contains("canvas") && !lower.Contains("dashboard"))
            {
                return t;
            }
        }

        // Priority 3: Left hand wrist bone (hands:b_l_hand)
        foreach (var t in allTransforms)
        {
            if (t.name == "hands:b_l_hand")
            {
                return t;
            }
        }

        return null;
    }

    public static void EnsurePokeInteractorsInScene()
    {
        var existingPoke = UnityEngine.Object.FindFirstObjectByType<XRPokeInteractor>(FindObjectsInactive.Include);
        if (existingPoke != null) return;

        // Find Right Hand
        Transform rightHand = null;
        var allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in allTransforms)
        {
            string lower = t.name.ToLower();
            if (lower.Equals("right hand") || (lower.Contains("right") && (lower.Contains("controller") || lower.Contains("hand")) && !lower.Contains("model") && !lower.Contains("ray") && !lower.Contains("canvas")))
            {
                rightHand = t;
                break;
            }
        }

        if (rightHand != null)
        {
            GameObject pokePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Prefabs/Interactors/Poke Interactor.prefab");
            if (pokePrefab != null)
            {
                GameObject pokeInstance = (GameObject)PrefabUtility.InstantiatePrefab(pokePrefab, rightHand);
                Undo.RegisterCreatedObjectUndo(pokeInstance, "Add Poke Interactor to Right Hand");
                pokeInstance.name = "Poke Interactor";
                pokeInstance.transform.localPosition = new Vector3(0f, 0f, 0.05f);
                pokeInstance.transform.localRotation = Quaternion.identity;
                Debug.Log($"[BuildAnatomyDashboardTool] Added Poke Interactor to Right Hand: '{rightHand.name}'");
            }
        }
    }

    private static void EnsureSystemIconsExist()
    {
        string dir = Path.Combine(Application.dataPath, "Resources", "AnatomyGridIcons");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string[] iconNames = {
            "icon_full_body", "icon_skeleton", "icon_muscular", "icon_insertions",
            "icon_circulatory", "icon_respiratory", "icon_digestive", "icon_endocrine",
            "icon_nervous", "icon_renal", "icon_reproductive", "icon_joints",
            "tab_icon_home", "tab_icon_systems", "tab_icon_layers",
            "tab_icon_search", "tab_icon_quiz", "tab_icon_settings",
            "icon_show_all", "icon_hide_others", "icon_reset_view", "icon_fit_to_view"
        };

        bool anyCreated = false;
        foreach (var name in iconNames)
        {
            string filePath = Path.Combine(dir, name + ".png");
            if (!File.Exists(filePath))
            {
                byte[] bytes = GenerateMedicalIconPNG(name);
                File.WriteAllBytes(filePath, bytes);
                anyCreated = true;
            }
        }

        if (anyCreated)
        {
            AssetDatabase.Refresh();
        }

        // Always ensure ALL png images in AnatomyGridIcons are configured as Single Sprites with transparency
        var allPngFiles = Directory.GetFiles(dir, "*.png");
        foreach (var fullPath in allPngFiles)
        {
            string fileName = Path.GetFileName(fullPath);
            string assetPath = "Assets/Resources/AnatomyGridIcons/" + fileName;
            TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti != null)
            {
                bool dirty = false;
                if (ti.textureType != TextureImporterType.Sprite)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    dirty = true;
                }
                if (ti.spriteImportMode != SpriteImportMode.Single)
                {
                    ti.spriteImportMode = SpriteImportMode.Single;
                    dirty = true;
                }
                if (!ti.alphaIsTransparency)
                {
                    ti.alphaIsTransparency = true;
                    dirty = true;
                }
                if (ti.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                    dirty = true;
                }

                if (dirty)
                {
                    ti.SaveAndReimport();
                }
            }
        }
    }

    private static byte[] GenerateMedicalIconPNG(string iconType)
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color transparent = new Color(0, 0, 0, 0);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, transparent);
            }
        }

        Color mainColor = new Color(0.35f, 0.78f, 0.98f, 0.95f); // Crisp medical cyan
        Color accentColor = new Color(0.95f, 0.45f, 0.45f, 0.95f); // Coral/muscle accent

        // Procedural vector-like silhouette drawing
        int cx = size / 2;
        int cy = size / 2;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - cx) / (float)size;
                float dy = (y - cy) / (float)size;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                bool drawPixel = false;
                Color col = mainColor;

                switch (iconType)
                {
                    case "icon_skeleton":
                        // Skull shape
                        float skullDist = Mathf.Sqrt(dx * dx * 1.3f + (dy - 0.06f) * (dy - 0.06f));
                        bool jaw = Mathf.Abs(dx) < 0.12f && dy < 0.02f && dy > -0.22f;
                        drawPixel = skullDist < 0.26f || jaw;
                        // Eye sockets cut out
                        if ((Mathf.Abs(dx - 0.09f) < 0.045f && Mathf.Abs(dy - 0.04f) < 0.05f) ||
                            (Mathf.Abs(dx + 0.09f) < 0.045f && Mathf.Abs(dy - 0.04f) < 0.05f))
                            drawPixel = false;
                        break;

                    case "icon_circulatory":
                        // Heart shape
                        float hx = dx * 2.2f;
                        float hy = dy * 2.2f + 0.15f;
                        float heartEq = (hx * hx + hy * hy - 1f);
                        drawPixel = (heartEq * heartEq * heartEq - hx * hx * hy * hy * hy) <= 0f && dist < 0.35f;
                        col = accentColor;
                        break;

                    case "icon_nervous":
                        // Brain lobes
                        float bDist = Mathf.Sqrt(dx * dx * 1.1f + dy * dy * 1.4f);
                        drawPixel = bDist < 0.28f;
                        if (Mathf.Abs(dx) < 0.015f && dy > -0.1f) drawPixel = false; // hemispheric fissure
                        col = new Color(0.60f, 0.75f, 1f, 0.95f);
                        break;

                    case "icon_respiratory":
                        // Two lung lobes
                        float leftLung = Mathf.Sqrt((dx + 0.12f) * (dx + 0.12f) * 1.5f + dy * dy);
                        float rightLung = Mathf.Sqrt((dx - 0.12f) * (dx - 0.12f) * 1.5f + dy * dy);
                        drawPixel = leftLung < 0.22f || rightLung < 0.22f;
                        col = new Color(0.40f, 0.85f, 0.95f, 0.95f);
                        break;

                    case "tab_icon_home":
                        // Roof triangle
                        bool roof = dy > 0.01f && dy < 0.32f && Mathf.Abs(dx) < (0.32f - dy) * 1.25f;
                        // House body
                        bool house = dy <= 0.01f && dy > -0.26f && Mathf.Abs(dx) < 0.22f;
                        // Door cutout
                        bool door = dy <= -0.04f && dy > -0.26f && Mathf.Abs(dx) < 0.07f;
                        drawPixel = (roof || house) && !door;
                        col = Color.white;
                        break;

                    case "tab_icon_systems":
                        // Stacked horizontal layers
                        bool layer1 = Mathf.Abs(dy - 0.16f) < 0.05f && Mathf.Abs(dx) < 0.25f;
                        bool layer2 = Mathf.Abs(dy) < 0.05f && Mathf.Abs(dx) < 0.25f;
                        bool layer3 = Mathf.Abs(dy + 0.16f) < 0.05f && Mathf.Abs(dx) < 0.25f;
                        drawPixel = layer1 || layer2 || layer3;
                        col = Color.white;
                        break;

                    case "tab_icon_layers":
                        // Eye outline + center pupil
                        float eyeRadius = (dx * dx * 2.5f + dy * dy * 8.5f);
                        bool outerEye = eyeRadius < 0.38f && eyeRadius > 0.20f;
                        bool pupil = (dx * dx + dy * dy) < 0.035f;
                        drawPixel = outerEye || pupil;
                        col = Color.white;
                        break;

                    case "tab_icon_search":
                        // Magnifying glass lens + diagonal handle
                        float lensDist = Mathf.Sqrt((dx + 0.06f) * (dx + 0.06f) + (dy - 0.06f) * (dy - 0.06f));
                        bool lensRing = lensDist < 0.24f && lensDist > 0.15f;
                        bool handle = dx > 0.05f && dy < -0.05f && Mathf.Abs(dx + dy) < 0.045f && dist < 0.38f;
                        drawPixel = lensRing || handle;
                        col = Color.white;
                        break;

                    case "tab_icon_quiz":
                        // Graduation cap diamond + skullcap
                        float capX = Mathf.Abs(dx) * 1.6f;
                        float capY = Mathf.Abs(dy - 0.08f) * 2.8f;
                        bool diamond = (capX + capY) < 0.42f;
                        bool capBand = dy < 0.08f && dy > -0.10f && Mathf.Abs(dx) < 0.18f;
                        bool capTassel = dx > 0.20f && dx < 0.25f && dy < 0.10f && dy > -0.22f;
                        drawPixel = diamond || capBand || capTassel;
                        col = Color.white;
                        break;

                    case "tab_icon_settings":
                        // Cog gear with teeth and center hole
                        float angle = Mathf.Atan2(dy, dx);
                        float teeth = Mathf.Cos(angle * 6f);
                        bool outerGear = dist < 0.24f || (dist < 0.33f && teeth > 0.25f);
                        bool hole = dist < 0.09f;
                        drawPixel = outerGear && !hole;
                        col = Color.white;
                        break;

                    case "icon_show_all":
                        // 3 horizontal glowing bars / layers
                        bool all1 = Mathf.Abs(dy - 0.16f) < 0.045f && Mathf.Abs(dx) < 0.28f;
                        bool all2 = Mathf.Abs(dy) < 0.045f && Mathf.Abs(dx) < 0.28f;
                        bool all3 = Mathf.Abs(dy + 0.16f) < 0.045f && Mathf.Abs(dx) < 0.28f;
                        drawPixel = all1 || all2 || all3;
                        col = new Color(0.35f, 0.85f, 1f, 1f);
                        break;

                    case "icon_hide_others":
                        // Eye with diagonal slash
                        float hEyeRadius = (dx * dx * 2.5f + dy * dy * 8.5f);
                        bool hOuterEye = hEyeRadius < 0.36f && hEyeRadius > 0.20f;
                        bool hSlash = Mathf.Abs(dx + dy) < 0.04f && dist < 0.35f;
                        drawPixel = hOuterEye || hSlash;
                        col = new Color(0.95f, 0.55f, 0.55f, 1f);
                        break;

                    case "icon_reset_view":
                        // Circular reload arrow
                        bool circleRing = dist > 0.16f && dist < 0.28f && !(dx > 0.08f && dy > 0.08f);
                        bool arrow = dx > 0.05f && dx < 0.25f && dy > 0.14f && dy < 0.30f && (dx + dy > 0.30f);
                        drawPixel = circleRing || arrow;
                        col = new Color(0.40f, 0.88f, 1f, 1f);
                        break;

                    case "icon_fit_to_view":
                        // 4 corner framing brackets [ ]
                        bool cLeft = dx < -0.16f && dx > -0.28f;
                        bool cRight = dx > 0.16f && dx < 0.28f;
                        bool cTop = dy > 0.16f && dy < 0.28f;
                        bool cBottom = dy < -0.16f && dy > -0.28f;
                        bool corners = (cLeft || cRight) && (Mathf.Abs(dy) > 0.12f && Mathf.Abs(dy) < 0.28f) ||
                                       (cTop || cBottom) && (Mathf.Abs(dx) > 0.12f && Mathf.Abs(dx) < 0.28f);
                        bool centerDot = dist < 0.05f;
                        drawPixel = corners || centerDot;
                        col = new Color(0.40f, 0.88f, 1f, 1f);
                        break;

                    default:
                        // Torso / Body / Muscle rounded capsule icon
                        float bodyCapsule = Mathf.Sqrt(dx * dx * 1.8f + dy * dy);
                        drawPixel = bodyCapsule < 0.28f;
                        if (iconType == "icon_muscular" || iconType == "icon_insertions")
                            col = accentColor;
                        break;
                }

                if (drawPixel)
                {
                    tex.SetPixel(x, y, col);
                }
            }
        }

        tex.Apply();
        return tex.EncodeToPNG();
    }
}
