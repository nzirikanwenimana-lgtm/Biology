using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Unity Editor menu command to automatically configure all 7 classroom interactions:
/// 1. Grab & Move (AnatomyTransformManipulator + XRGrabInteractable)
/// 2. Scale Up / Down (Two-hand pinch, thumbstick, UI slider & buttons)
/// 3. Rotate (Wrist rotation, yaw buttons, and presentation turntable)
/// 4. Fold / Unfold / Separate (AnatomyExplodedView with layer offsets)
/// 5. Select a Mesh (Ray pointing & direct hand touch with persistent highlight)
/// 6. Information Button ✋ (Hand/wrist menu + Media diagram & 3D pulse animation)
/// 7. Hide / Show (Layer toggles and Hide Selected Part button)
/// </summary>
public class SetupAnatomyInteractions
{
    [MenuItem("Anatomy/Advanced/Setup Classroom Interactions (Grab, Explode, Turntable)", false, 30)]
    public static void SetupClassroomInteractions()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.isLoaded)
        {
            EditorUtility.DisplayDialog("Error", "No active scene loaded.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Setup Anatomy Classroom Interactions");
        int undoGroup = Undo.GetCurrentGroup();

        // 1. Clean any existing collider conflicts and calibrate player height
        AnatomySceneRepairTool.FixColliderConflictsAndPlayerHeightInternal(new List<string>());

        // 2. Locate Anatomy Root
        GameObject anatomyRoot = FindAnatomyRoot();
        if (anatomyRoot == null)
        {
            EditorUtility.DisplayDialog("Anatomy Root Not Found",
                "Could not automatically find an Anatomy Root in the active scene.\n\nPlease select the anatomy root GameObject (e.g. Anato_XR or Human Body) and run this tool again.", "OK");
            return;
        }

        Debug.Log($"[SetupAnatomyInteractions] Configuring interactions on Anatomy Root: '{anatomyRoot.name}'");

        // 3. Setup AnatomyTransformManipulator (Grab & Move, Scale Up/Down, Rotate)
        AnatomyTransformManipulator manipulator = anatomyRoot.GetComponent<AnatomyTransformManipulator>();
        if (manipulator == null)
        {
            manipulator = Undo.AddComponent<AnatomyTransformManipulator>(anatomyRoot);
        }
        manipulator.targetTransform = anatomyRoot.transform;
        manipulator.minScaleMultiplier = 0.2f;
        manipulator.maxScaleMultiplier = 6.0f;
        manipulator.enableDesktopControls = true;
        EditorUtility.SetDirty(manipulator);

        // Ensure XR Grab Interactable on the Anatomy Root if available
        SetupXRGrabOnRoot(anatomyRoot);

        // 3. Setup AnatomyExplodedView (Fold / Unfold / Separate)
        AnatomyExplodedView explodedView = anatomyRoot.GetComponent<AnatomyExplodedView>();
        if (explodedView == null)
        {
            explodedView = Undo.AddComponent<AnatomyExplodedView>(anatomyRoot);
        }
        explodedView.AutoDetectLayers();
        explodedView.InitializeLayers();
        EditorUtility.SetDirty(explodedView);

        // 4. Ensure AnatomySystemController is present
        AnatomySystemController systemController = Object.FindFirstObjectByType<AnatomySystemController>(FindObjectsInactive.Include);
        if (systemController == null)
        {
            systemController = Undo.AddComponent<AnatomySystemController>(anatomyRoot);
        }

        // 5. Setup BodyPartInfoUI & AnatomyMediaViewer
        BodyPartInfoUI infoUI = Object.FindFirstObjectByType<BodyPartInfoUI>(FindObjectsInactive.Include);
        if (infoUI != null)
        {
            AnatomyMediaViewer mediaViewer = infoUI.GetComponentInChildren<AnatomyMediaViewer>(true);
            if (mediaViewer == null)
            {
                mediaViewer = Undo.AddComponent<AnatomyMediaViewer>(infoUI.gameObject);
                infoUI.mediaViewer = mediaViewer;
            }
            EditorUtility.SetDirty(infoUI);
        }

        // 6. Setup or Link AnatomyHandMenu
        AnatomyHandMenu handMenu = Object.FindFirstObjectByType<AnatomyHandMenu>(FindObjectsInactive.Include);
        if (handMenu == null)
        {
            handMenu = CreateDefaultHandMenu(systemController, explodedView, manipulator, infoUI);
        }
        else
        {
            handMenu.systemController = systemController;
            handMenu.explodedView = explodedView;
            handMenu.transformManipulator = manipulator;
            handMenu.infoUI = infoUI;
            EditorUtility.SetDirty(handMenu);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(activeScene);

        string report = "Classroom Interactions Configured Successfully!\n\n" +
                        $"• Anatomy Root: {anatomyRoot.name}\n" +
                        $"• Grab & Move, Scale Up/Down, Rotate: Active\n" +
                        $"• Fold / Unfold / Separate: {explodedView.layers.Count} Layers configured\n" +
                        $"• Select a Mesh: Touch & Ray highlighting active\n" +
                        $"• ✋ Info Button & Hand Menu: Ready\n" +
                        $"• Hide / Show: Layer toggles wired\n\n" +
                        "Desktop Shortcuts for quick testing:\n" +
                        "  [I] ✋ Info Button\n" +
                        "  [M] Toggle Hand Menu\n" +
                        "  [F] Toggle Fold / Unfold\n" +
                        "  [H] Hide Selected Part\n" +
                        "  [Scroll Wheel] Scale Up / Down\n" +
                        "  [Right Mouse Drag] Rotate Model\n" +
                        "  [Middle Mouse Drag / Ctrl+Left Drag] Move Model";

        Debug.Log($"[SetupAnatomyInteractions] {report}");
        EditorUtility.DisplayDialog("Anatomy Interactions Ready", report, "OK");
    }

    private static GameObject FindAnatomyRoot()
    {
        if (Selection.activeGameObject != null)
        {
            // Climb up hierarchy to find the top-level anatomy root,
            // preventing placing grab interactables on intermediate sub-groups like Joints or individual bones
            Transform curr = Selection.activeGameObject.transform;
            while (curr.parent != null && 
                   !curr.parent.name.ToLower().Contains("room") && 
                   !curr.parent.name.ToLower().Contains("scene") &&
                   curr.parent.GetComponentInChildren<BodyPartSelector>(true) != null)
            {
                curr = curr.parent;
            }
            return curr.gameObject;
        }

        string[] keywords = { "male", "anato", "body", "human", "skeleton", "model" };
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (var root in roots)
        {
            if (root == null) continue;
            string lower = root.name.ToLowerInvariant();
            if (lower.Contains("camera") || lower.Contains("light") || lower.Contains("canvas") || lower.Contains("room") || lower.Contains("xr"))
                continue;

            foreach (var kw in keywords)
            {
                if (lower.Contains(kw))
                    return root;
            }
        }

        return roots.Length > 0 ? roots[0] : null;
    }

    private static void SetupXRGrabOnRoot(GameObject root)
    {
        // 1. Ensure any Rigidbody on root and descendants is strictly Kinematic to prevent PhysX errors
        Rigidbody[] rbs = root.GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in rbs)
        {
            if (rb != null)
            {
                Undo.RecordObject(rb, "Make Rigidbody Kinematic");
                rb.isKinematic = true;
                rb.useGravity = false;
                EditorUtility.SetDirty(rb);
            }
        }

        // Try adding XRGrabInteractable if XRI is installed
        var grabType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable, Unity.XR.Interaction.Toolkit");
        if (grabType == null)
        {
            grabType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable, Unity.XR.Interaction.Toolkit");
        }

        if (grabType != null)
        {
            Component existing = root.GetComponent(grabType);
            if (existing == null)
            {
                existing = Undo.AddComponent(root, grabType);
            }

        // Ensure a dedicated handle BoxCollider exists on root for grab interaction
        BoxCollider box = root.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = Undo.AddComponent<BoxCollider>(root);
            box.size = new Vector3(1f, 1.8f, 0.5f);
            box.center = new Vector3(0f, 0.9f, 0f);
            box.isTrigger = false;
            EditorUtility.SetDirty(box);
        }

        if (existing is UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable)
        {
            grabInteractable.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Kinematic;
            grabInteractable.throwOnDetach = false;
            // CRITICAL: Restrict colliders strictly to the root box collider.
            // Never allow it to capture child mesh colliders!
            grabInteractable.colliders.Clear();
            grabInteractable.colliders.Add(box);
            EditorUtility.SetDirty(grabInteractable);
        }
    }
}

    private static AnatomyHandMenu CreateDefaultHandMenu(
        AnatomySystemController systemCtrl,
        AnatomyExplodedView explode,
        AnatomyTransformManipulator manip,
        BodyPartInfoUI infoUI)
    {
        GameObject menuGO = new GameObject("AnatomyHandMenu_Canvas");
        Undo.RegisterCreatedObjectUndo(menuGO, "Create Hand Menu");

        Canvas canvas = menuGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        menuGO.AddComponent<CanvasScaler>();
        menuGO.AddComponent<GraphicRaycaster>();

        RectTransform rect = menuGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300, 420);
        rect.localScale = Vector3.one * 0.001f;

        // Position slightly in front of camera or world center
        Camera cam = Camera.main;
        if (cam != null)
        {
            rect.position = cam.transform.position + cam.transform.forward * 0.7f + cam.transform.right * -0.25f;
            rect.rotation = cam.transform.rotation;
        }
        else
        {
            rect.position = new Vector3(-0.3f, 1.2f, 1f);
        }

        AnatomyHandMenu handMenu = menuGO.AddComponent<AnatomyHandMenu>();
        handMenu.menuPanel = menuGO;
        handMenu.systemController = systemCtrl;
        handMenu.explodedView = explode;
        handMenu.transformManipulator = manip;
        handMenu.infoUI = infoUI;

        // Auto-link to Quest 3S Left Controller if present in the scene
        Transform leftController = null;
        var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in allTransforms)
        {
            string lower = t.name.ToLower();
            if (lower.Contains("left") && (lower.Contains("controller") || lower.Contains("hand")))
            {
                leftController = t;
                break;
            }
        }

        if (leftController != null)
        {
            handMenu.handAnchor = leftController;
            handMenu.openOnPalmLook = true;
            menuGO.transform.position = leftController.position + leftController.TransformDirection(handMenu.localOffset);
            menuGO.transform.rotation = leftController.rotation;
            Debug.Log($"[SetupAnatomyInteractions] Hand menu anchored to Quest Left Controller: '{leftController.name}'");
        }
        else
        {
            handMenu.openOnPalmLook = false; // defaults to floating world panel if no VR rig in scene
        }

        return handMenu;
    }
}
