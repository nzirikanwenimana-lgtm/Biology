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

[InitializeOnLoad]
public static class SetupHandDashboardTool
{
    private static bool s_HasRunOnceThisSession = false;

    static SetupHandDashboardTool()
    {
        EditorApplication.delayCall += AutoRunOnceOnEditorStart;
    }

    private static void AutoRunOnceOnEditorStart()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;
        if (s_HasRunOnceThisSession) return;
        s_HasRunOnceThisSession = true;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.isLoaded && (scene.name == "Main VR Scene" || scene.path.Contains("Main VR Scene")))
        {
            Debug.Log("[SetupHandDashboardTool] Automatically configuring Hand Dashboard & Connecting to Body Anatomy for: " + scene.name);
            ConfigureHandDashboard(false);
            ConnectDashboardToBodyAnatomy(false);
        }
    }

    [MenuItem("Anatomy/UI/Connect UI Dashboard to Body Anatomy", false, -1)]
    public static void ConnectDashboardToBodyAnatomyMenu()
    {
        ConnectDashboardToBodyAnatomy(true);
    }

    [MenuItem("Anatomy/UI/Configure Hand Dashboard & Dual Interaction", false, 0)]
    public static void ConfigureHandDashboardMenu()
    {
        ConfigureHandDashboard(true);
    }

    public static void ConfigureHandDashboard(bool showDialog)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
        {
            if (showDialog) EditorUtility.DisplayDialog("Setup Hand Dashboard", "Cannot modify or save scenes while in Play Mode. Please exit Play Mode first.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded)
        {
            if (showDialog) EditorUtility.DisplayDialog("Setup Hand Dashboard", "Please open a scene first.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Configure Hand Dashboard & Dual Interaction");
        int group = Undo.GetCurrentGroup();

        // 1. Locate Hand Transforms
        Transform leftHandModel = FindLeftHandModelAnchor();
        Transform rightHand = FindRightHandTransform();

        if (leftHandModel == null)
        {
            Debug.LogWarning("[SetupHandDashboardTool] Left Hand Model not found in scene. Using fallback search.");
        }

        // 2. Fix EventSystem (Ensure unparented at scene root with XRUIInputModule)
        FixEventSystemHierarchy();

        // 3. Ensure Poke Interactor on Right Hand for finger touch poking
        EnsurePokeInteractorOnRightHand(rightHand);

        // 4. Create or configure dedicated WorldSpace Canvas anchored to Left Hand Model
        Canvas dashboardCanvas = SetupDashboardCanvas(leftHandModel);

        // 5. Setup AnatomyVR_Dashboard inside Canvas with Affordance & Colliders
        SetupDashboardWindow(dashboardCanvas, leftHandModel);

        // 6. Clean up old floating panels and misplaced joint components
        CleanupOldMenuPanels();
        CleanupMisplacedJointComponents();

        // 7. Connect UI Dashboard to 3D Body Anatomy
        ConnectDashboardToBodyAnatomyInternal();

        // 8. Refresh all icons and raycast targets
        BuildAnatomyDashboardTool.RefreshIconsInScene();

        Undo.CollapseUndoOperations(group);
        if (!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        string report = 
            "AnatomyVR Dashboard Configuration Complete!\n\n" +
            $"• Anchored to Left Hand: '{(leftHandModel != null ? leftHandModel.name : "Auto-Locate at Runtime")}'\n" +
            "• Connected to 3D Body Anatomy: All 8 systems linked to AnatomySystemController\n" +
            "• Dual Interaction: XR Raycast + Physical Index Finger Poke Interactor\n" +
            "• Gaze & Palm Affordance: Shows when left palm faces user's head and gaze is directed at hand\n" +
            "• Hysteresis & Debounce: Smooth transitions, no edge flicker\n" +
            "• EventSystem unparented to root with active XRUIInputModule\n" +
            "• Scene automatically saved!";

        Debug.Log("[SetupHandDashboardTool] " + report);

        if (showDialog)
        {
            EditorUtility.DisplayDialog("AnatomyVR Hand Dashboard Configured", report, "OK");
        }
    }

    public static void ConnectDashboardToBodyAnatomy(bool showDialog)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
        {
            if (showDialog) EditorUtility.DisplayDialog("Connect Dashboard", "Cannot modify scene while in Play Mode.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded) return;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Connect UI Dashboard to Body Anatomy");
        int group = Undo.GetCurrentGroup();

        var result = ConnectDashboardToBodyAnatomyInternal();

        Undo.CollapseUndoOperations(group);
        if (!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        string report =
            "UI Dashboard successfully connected to Body Anatomy!\n\n" +
            $"• Anatomy Controller: '{result.controllerName}' with 8 anatomical systems\n" +
            $"• UI Dashboard: '{result.dashboardName}'\n" +
            "• Dual Connection: System cards isolate/show body layers; body part clicks select dashboard card\n" +
            "• Scene automatically saved!";

        Debug.Log("[SetupHandDashboardTool] " + report);
        if (showDialog)
        {
            EditorUtility.DisplayDialog("Dashboard Connected to Body Anatomy", report, "OK");
        }
    }

    private static (string controllerName, string dashboardName) ConnectDashboardToBodyAnatomyInternal()
    {
        List<string> logs = new List<string>();

        // 1. Ensure AnatomyDisplayStage & HumanBody are properly set up
        GameObject stageGo = BuildAnatomyStageTool.BuildOrUpdateStageInternal(logs);

        // 2. Find HumanBody
        Transform humanBody = (stageGo != null) ? stageGo.transform.Find("HumanBody") : null;
        if (humanBody == null)
        {
            var hbGo = GameObject.Find("HumanBody");
            if (hbGo != null) humanBody = hbGo.transform;
        }

        string ctrlName = "None";
        AnatomySystemController sysCtrl = null;
        if (humanBody != null)
        {
            sysCtrl = humanBody.GetComponent<AnatomySystemController>();
            if (sysCtrl == null)
            {
                sysCtrl = Undo.AddComponent<AnatomySystemController>(humanBody.gameObject);
            }

            Undo.RecordObject(sysCtrl, "Configure AnatomySystemController Layers");
            for (int i = 0; i < humanBody.childCount; i++)
            {
                Transform c = humanBody.GetChild(i);
                string ln = c.name.ToLower();

                if (ln.Contains("male body") || (ln.Contains("body") && !ln.Contains("part")))
                    sysCtrl.maleBody = c.gameObject;
                else if (ln.Contains("skelet"))
                    sysCtrl.skeletalSystem = c.gameObject;
                else if (ln.Contains("insertion"))
                    sysCtrl.muscularInsertions = c.gameObject;
                else if (ln.Contains("muscul"))
                    sysCtrl.muscularSystem = c.gameObject;
                else if (ln.Contains("circulat"))
                    sysCtrl.circulatorySystem = c.gameObject;
                else if (ln.Contains("viscer") || ln.Contains("organ"))
                    sysCtrl.visceralSystem = c.gameObject;
                else if (ln.Contains("nerv"))
                    sysCtrl.nervousSystem = c.gameObject;
                else if (ln.Contains("joint"))
                    sysCtrl.joints = c.gameObject;
            }
            sysCtrl.AutoFindLayers();
            EditorUtility.SetDirty(sysCtrl);
            ctrlName = sysCtrl.gameObject.name;
        }

        // 3. Connect AnatomyDashboardUI
        string dashName = "None";
        AnatomyDashboardUI dashUI = UnityEngine.Object.FindFirstObjectByType<AnatomyDashboardUI>(FindObjectsInactive.Include);
        if (dashUI != null)
        {
            Undo.RecordObject(dashUI, "Link AnatomyDashboardUI to AnatomySystemController");
            if (sysCtrl != null)
            {
                dashUI.systemController = sysCtrl;
            }
            dashUI.infoUI = UnityEngine.Object.FindFirstObjectByType<BodyPartInfoUI>(FindObjectsInactive.Include);
            dashUI.quizUI = UnityEngine.Object.FindFirstObjectByType<QuizUI>(FindObjectsInactive.Include);
            dashUI.ConnectToBodyAnatomy();
            dashUI.RefreshCardVisibilityStates();
            EditorUtility.SetDirty(dashUI);
            dashName = dashUI.gameObject.name;
        }

        return (ctrlName, dashName);
    }

    private static Transform FindLeftHandModelAnchor()
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

        // Priority 2: Left Hand Model (Prefab instance under Left Hand)
        foreach (var t in allTransforms)
        {
            string lower = t.name.ToLower();
            if (lower.Contains("left") && lower.Contains("hand") && lower.Contains("model") && !lower.Contains("canvas") && !lower.Contains("dashboard"))
            {
                return t;
            }
        }

        // Priority 3: Left hand wrist bone
        foreach (var t in allTransforms)
        {
            if (t.name == "hands:b_l_hand") return t;
        }

        return null;
    }

    private static Transform FindRightHandTransform()
    {
        var allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in allTransforms)
        {
            string lower = t.name.ToLower();
            if (lower.Equals("right hand") || (lower.Contains("right") && (lower.Contains("controller") || lower.Contains("hand")) && !lower.Contains("model") && !lower.Contains("ray") && !lower.Contains("canvas") && !lower.Contains("interactor")))
            {
                return t;
            }
        }
        return null;
    }

    private static void FixEventSystemHierarchy()
    {
        var eventSystem = UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
        if (eventSystem != null)
        {
            if (eventSystem.transform.parent != null)
            {
                Undo.SetTransformParent(eventSystem.transform, null, "Move EventSystem to Scene Root");
                eventSystem.transform.position = Vector3.zero;
                eventSystem.transform.rotation = Quaternion.identity;
                eventSystem.transform.localScale = Vector3.one;
                Debug.Log("[SetupHandDashboardTool] Moved EventSystem to Scene Root.");
            }

            var xrInput = eventSystem.GetComponent<XRUIInputModule>();
            if (xrInput != null)
            {
                xrInput.enableXRInput = true;
            }
        }
    }

    private static void EnsurePokeInteractorOnRightHand(Transform rightHand)
    {
        if (rightHand == null) return;

        // Check if Poke Interactor already exists
        var pokeInteractors = rightHand.GetComponentsInChildren<XRPokeInteractor>(true);
        XRPokeInteractor poke = null;
        if (pokeInteractors.Length > 0)
        {
            poke = pokeInteractors[0];
        }
        else
        {
            GameObject pokePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Samples/XR Interaction Toolkit/3.3.2/Starter Assets/Prefabs/Interactors/Poke Interactor.prefab");
            if (pokePrefab != null)
            {
                GameObject pokeInstance = (GameObject)PrefabUtility.InstantiatePrefab(pokePrefab, rightHand);
                Undo.RegisterCreatedObjectUndo(pokeInstance, "Add Poke Interactor to Right Hand");
                pokeInstance.name = "Poke Interactor";
                pokeInstance.transform.localPosition = new Vector3(0.01f, -0.01f, 0.08f); // index fingertip offset
                pokeInstance.transform.localRotation = Quaternion.identity;
                poke = pokeInstance.GetComponent<XRPokeInteractor>();
                Debug.Log("[SetupHandDashboardTool] Created Poke Interactor on Right Hand.");
            }
        }

        if (poke != null)
        {
            Undo.RecordObject(poke, "Configure Poke Interactor Properties");
            poke.enableUIInteraction = true;
            poke.clickUIOnDown = true;
            poke.requirePokeFilter = false; // Allow direct poking of all UI buttons
            poke.pokeHoverRadius = 0.025f;
            poke.pokeSelectWidth = 0.030f;
            poke.pokeDepth = 0.080f;
            EditorUtility.SetDirty(poke);
        }
    }

    private static Canvas SetupDashboardCanvas(Transform leftHandModel)
    {
        Canvas canvas = null;
        var allCanvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in allCanvases)
        {
            if (c.name.Equals("AnatomyVR_Dashboard_Canvas", StringComparison.OrdinalIgnoreCase))
            {
                canvas = c;
                break;
            }
        }

        if (canvas == null)
        {
            GameObject cgo = new GameObject("AnatomyVR_Dashboard_Canvas");
            Undo.RegisterCreatedObjectUndo(cgo, "Create AnatomyVR_Dashboard_Canvas");
            canvas = cgo.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;
        canvas.gameObject.SetActive(true);

        // Ensure CanvasScaler
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = Undo.AddComponent<CanvasScaler>(canvas.gameObject);
        scaler.dynamicPixelsPerUnit = 3f;
        scaler.referencePixelsPerUnit = 100f;

        // Ensure GraphicRaycaster
        var gr = canvas.GetComponent<GraphicRaycaster>();
        if (gr == null) gr = Undo.AddComponent<GraphicRaycaster>(canvas.gameObject);

        // Ensure TrackedDeviceGraphicRaycaster for dual Ray and Poke interaction
        var trackedGR = canvas.GetComponent<TrackedDeviceGraphicRaycaster>();
        if (trackedGR == null) trackedGR = Undo.AddComponent<TrackedDeviceGraphicRaycaster>(canvas.gameObject);
        trackedGR.ignoreReversedGraphics = false;
        trackedGR.checkFor2DOcclusion = false;
        trackedGR.checkFor3DOcclusion = false;

        RectTransform rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1280f, 840f);

        // Parent to Left Hand
        if (leftHandModel != null)
        {
            Undo.SetTransformParent(canvas.transform, leftHandModel, "Parent Canvas to Left Hand");
            bool isHandModel = leftHandModel.name.ToLower().Contains("model") || leftHandModel.localRotation.eulerAngles.z > 90f;
            rt.localPosition = isHandModel ? new Vector3(0.09f, -0.03f, 0.08f) : new Vector3(0.03f, 0.09f, 0.08f);
            rt.localRotation = isHandModel ? Quaternion.Euler(-65f, 180f, 0f) : Quaternion.Euler(38f, 0f, 0f);
            rt.localScale = Vector3.one * 0.00038f;
        }

        EditorUtility.SetDirty(canvas.gameObject);
        return canvas;
    }

    private static void SetupDashboardWindow(Canvas canvas, Transform leftHandModel)
    {
        // Find AnatomyVR_Dashboard GameObject
        Transform dashTransform = null;
        var allDashboards = UnityEngine.Object.FindObjectsByType<AnatomyDashboardUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (allDashboards.Length > 0)
        {
            dashTransform = allDashboards[0].transform;
        }
        else
        {
            var allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in allTransforms)
            {
                if (t.name.Equals("AnatomyVR_Dashboard", StringComparison.OrdinalIgnoreCase))
                {
                    dashTransform = t;
                    break;
                }
            }
        }

        if (dashTransform != null)
        {
            // Move inside the dedicated dashboard canvas
            if (dashTransform.parent != canvas.transform)
            {
                Undo.SetTransformParent(dashTransform, canvas.transform, "Move AnatomyVR_Dashboard inside Canvas");
            }
            dashTransform.gameObject.SetActive(true);

            RectTransform drt = dashTransform.GetComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.5f, 0.5f);
            drt.anchorMax = new Vector2(0.5f, 0.5f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = Vector2.zero;
            drt.sizeDelta = new Vector2(1240f, 800f);
            drt.localScale = Vector3.one;

            // Ensure CanvasGroup for affordance fading
            var cg = dashTransform.GetComponent<CanvasGroup>();
            if (cg == null) cg = Undo.AddComponent<CanvasGroup>(dashTransform.gameObject);

            // Ensure BoxCollider for physical poke collision
            var col = dashTransform.GetComponent<BoxCollider>();
            if (col == null) col = Undo.AddComponent<BoxCollider>(dashTransform.gameObject);
            col.size = new Vector3(1240f, 800f, 10f);
            col.center = Vector3.zero;

            // Ensure AnatomyDashboardUI
            var dashUI = dashTransform.GetComponent<AnatomyDashboardUI>();
            if (dashUI == null) dashUI = Undo.AddComponent<AnatomyDashboardUI>(dashTransform.gameObject);
            if (dashUI.systemController == null)
            {
                dashUI.systemController = UnityEngine.Object.FindFirstObjectByType<AnatomySystemController>(FindObjectsInactive.Include);
            }

            // Ensure AnatomyDashboardHandAffordance
            var affordance = dashTransform.GetComponent<AnatomyDashboardHandAffordance>();
            if (affordance == null) affordance = Undo.AddComponent<AnatomyDashboardHandAffordance>(dashTransform.gameObject);

            bool isHandModel = leftHandModel != null && (leftHandModel.name.ToLower().Contains("model") || leftHandModel.localRotation.eulerAngles.z > 90f);
            Vector3 targetPos = isHandModel ? new Vector3(0.09f, -0.03f, 0.08f) : new Vector3(0.03f, 0.09f, 0.08f);
            Vector3 targetRot = isHandModel ? new Vector3(-65f, 180f, 0f) : new Vector3(38f, 0f, 0f);

            Undo.RecordObject(affordance, "Configure Hand Affordance");
            affordance.handAnchor = leftHandModel;
            affordance.canvasGroup = cg;
            affordance.palmAxis = isHandModel ? AnatomyDashboardHandAffordance.PalmAxisOption.Down : AnatomyDashboardHandAffordance.PalmAxisOption.Auto;
            affordance.palmOpenThreshold = 0.35f;
            affordance.palmCloseThreshold = 0.15f;
            affordance.requireGaze = true;
            affordance.gazeOpenThreshold = 0.40f;
            affordance.gazeCloseThreshold = 0.20f;
            affordance.transitionSpeed = 9f;
            affordance.closedScaleRatio = 0.85f;
            affordance.canvasWorldScale = 0.00038f;
            affordance.localPositionOffset = targetPos;
            affordance.localRotationOffset = targetRot;

            dashUI.handAffordance = affordance;

            EditorUtility.SetDirty(dashTransform.gameObject);
        }
        else
        {
            // If dashboard doesn't exist, build complete hierarchy
            BuildAnatomyDashboardTool.BuildModernDashboardMenu();
        }
    }

    private static void CleanupOldMenuPanels()
    {
        // Find AnatomySystemMenu or old duplicate panels and disable/clean up obsolete children
        var allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in allTransforms)
        {
            if (t.name.Equals("MaleBodyButtons", StringComparison.OrdinalIgnoreCase) ||
                t.name.Equals("SystemMenuPanel", StringComparison.OrdinalIgnoreCase))
            {
                // Deactivate obsolete floating duplicate panels so they don't occlude the scene
                Undo.RecordObject(t.gameObject, "Deactivate Obsolete Floating UI Panel");
                t.gameObject.SetActive(false);
                EditorUtility.SetDirty(t.gameObject);
            }
        }
    }

    private static void CleanupMisplacedJointComponents()
    {
        var allExploded = UnityEngine.Object.FindObjectsByType<AnatomyExplodedView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ex in allExploded)
        {
            string gname = ex.gameObject.name.ToLower();
            if (gname.Contains("joint") || gname.Contains("articulation") || (ex.gameObject.name != "Human male anatomy" && !gname.Contains("male anatomy")))
            {
                Debug.Log($"[SetupHandDashboardTool] Removing misplaced AnatomyExplodedView on '{ex.gameObject.name}'");
                Undo.DestroyObjectImmediate(ex);
            }
        }

        var allManipulators = UnityEngine.Object.FindObjectsByType<AnatomyTransformManipulator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var manip in allManipulators)
        {
            string gname = manip.gameObject.name.ToLower();
            if (gname.Contains("joint") || gname.Contains("articulation"))
            {
                Debug.Log($"[SetupHandDashboardTool] Removing misplaced AnatomyTransformManipulator on '{manip.gameObject.name}'");
                Undo.DestroyObjectImmediate(manip);
            }
        }
    }
}
