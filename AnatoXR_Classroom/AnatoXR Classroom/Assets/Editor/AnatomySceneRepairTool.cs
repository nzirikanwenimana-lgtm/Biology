using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Professional VR UI Auto-Layout and Scene Repair Utility (Build Book Phase 4, 6 & 8).
/// Solves the overlapping RectTransform problem by structuring the World-Space Canvas into:
/// 1. Left Column: Sleek 'SystemMenuPanel' with a VerticalLayoutGroup for all system buttons.
/// 2. Right Column: Beautiful 'BodyPartInfoPanel' card with clean typography, headers, and close button.
/// 3. Zero overlaps, zero manual tweaking required.
/// </summary>
public static class AnatomySceneRepairTool
{
    [MenuItem("Anatomy/Full Fix: Align Skeleton, Mount Hand Dashboard, Life-Size Scale", false, 0)]
    public static void FullSceneAndAnatomyFix()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.isLoaded) return;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Full Anatomy Scene Fix");
        int group = Undo.GetCurrentGroup();

        List<string> logs = new List<string>();

        // 1. Build or Update master movable AnatomyDisplayStage & align all systems to AnatomyCenter
        BuildAnatomyStageTool.BuildOrUpdateStageInternal(logs);

        // 2. Configure modern AnatomyVR Hand Dashboard directly on Left Hand
        ConfigureHandDashboard(logs);

        // 3. Fix Collider Conflicts & Player Height
        FixColliderConflictsAndPlayerHeightInternal(logs);

        // 4. Align Anatomy Inspector to Right-Front VR Layout
        AlignInspectorRightFrontInternal(logs);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);

        string report = string.Join("\n• ", logs);
        Debug.Log($"[AnatomySceneRepairTool] Full Fix Complete:\n• {report}");
        EditorUtility.DisplayDialog("Full Fix Complete", 
            $"Anatomy and VR Scene Fixed Successfully!\n\n• {report}", "OK");
    }

    [MenuItem("Anatomy/Cleanup/Remove Exploded View & Manipulator From Joints", false, 40)]
    public static void RemoveMisplacedJointComponentsMenu()
    {
        var allExploded = Object.FindObjectsByType<AnatomyExplodedView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int removedCount = 0;
        foreach (var ex in allExploded)
        {
            string gname = ex.gameObject.name.ToLower();
            if (gname.Contains("joint") || gname.Contains("articulation") || (ex.gameObject.name != "Human male anatomy" && !gname.Contains("male anatomy")))
            {
                Undo.DestroyObjectImmediate(ex);
                removedCount++;
            }
        }

        var allManipulators = Object.FindObjectsByType<AnatomyTransformManipulator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var manip in allManipulators)
        {
            string gname = manip.gameObject.name.ToLower();
            if (gname.Contains("joint") || gname.Contains("articulation"))
            {
                Undo.DestroyObjectImmediate(manip);
                removedCount++;
            }
        }

        var scene = SceneManager.GetActiveScene();
        if (scene.isLoaded)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        EditorUtility.DisplayDialog("Joint Components Cleanup",
            $"Cleaned up {removedCount} misplaced component(s) (Exploded View & Transform Manipulator) from Joints.\nJoints now retain only their visual meshes and colliders.", "OK");
    }

    [MenuItem("Anatomy/Presentation/Align Anatomy Inspector to Right-Front (+30°)", false, 10)]
    public static void AlignInspectorRightFrontMenu()
    {
        BodyPartInfoUI infoUI = Object.FindFirstObjectByType<BodyPartInfoUI>(FindObjectsInactive.Include);
        if (infoUI == null)
        {
            EditorUtility.DisplayDialog("Anatomy Inspector", "No BodyPartInfoUI found in scene.", "OK");
            return;
        }

        Undo.RecordObject(infoUI, "Align Anatomy Inspector to Right-Front");
        infoUI.enableVRPositioning = true;
        infoUI.normalHorizontalAngle = 30f;
        infoUI.rightSideHorizontalAngle = 42f;
        infoUI.distanceFromHeadset = 0.9f;
        infoUI.verticalOffset = -0.12f;
        infoUI.canvasWorldScale = new Vector3(0.0008f, 0.0008f, 0.0008f);

        infoUI.AlignInspectorInEditor();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Anatomy Inspector Aligned", 
            "Anatomy Inspector has been successfully aligned to the Right-Front VR Layout:\n\n" +
            "• Horizontal Angle: +30° (Right of forward viewing baseline)\n" +
            "• Distance from Headset: 0.9m\n" +
            "• Vertical Height: -0.12m (below eye level)\n" +
            "• Adaptive Offset: +42° for right-sided anatomy structures\n" +
            "• Body Model: Unobstructed & centered in front (0°)\n" +
            "• Stability: Rock-solid (no per-frame drift)", "OK");
    }

    private static void AlignInspectorRightFrontInternal(List<string> logs)
    {
        BodyPartInfoUI infoUI = Object.FindFirstObjectByType<BodyPartInfoUI>(FindObjectsInactive.Include);
        if (infoUI != null)
        {
            Undo.RecordObject(infoUI, "Align Anatomy Inspector to Right-Front");
            infoUI.enableVRPositioning = true;
            infoUI.normalHorizontalAngle = 30f;
            infoUI.rightSideHorizontalAngle = 42f;
            infoUI.distanceFromHeadset = 0.9f;
            infoUI.verticalOffset = -0.12f;
            infoUI.canvasWorldScale = new Vector3(0.0008f, 0.0008f, 0.0008f);

            infoUI.AlignInspectorInEditor();
            logs.Add("Aligned Anatomy Inspector to Right-Front (+30°) VR reading layout.");
        }
    }

    public static void FixColliderConflictsAndPlayerHeightInternal(List<string> logs)
    {
        // A. Remove XRGrabInteractable from intermediate anatomical group parents (like Joints, Muscular, etc.)
        var grabType = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable, Unity.XR.Interaction.Toolkit");
        if (grabType != null)
        {
            var allGrabs = Object.FindObjectsByType(grabType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var comp in allGrabs)
            {
                if (comp is UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab)
                {
                    GameObject go = grab.gameObject;
                    int childSelectors = go.GetComponentsInChildren<BodyPartSelector>(true).Length;
                    bool isSelfSelector = go.GetComponent<BodyPartSelector>() != null;

                    // If this object is an intermediate group (has children with BodyPartSelector, but is NOT an individual selector)
                    if (childSelectors > 1 && !isSelfSelector)
                    {
                        // If it has an anatomy parent (e.g. Joints under Anato_XR), it is an intermediate group
                        bool isIntermediateGroup = go.transform.parent != null && 
                            go.transform.parent.GetComponentInChildren<BodyPartSelector>(true) != null;

                        if (isIntermediateGroup)
                        {
                            Undo.DestroyObjectImmediate(grab);
                            logs.Add($"Removed conflicting XRGrabInteractable from intermediate group '{go.name}' ({childSelectors} child parts).");

                            Rigidbody rb = go.GetComponent<Rigidbody>();
                            if (rb != null)
                            {
                                Undo.DestroyObjectImmediate(rb);
                            }
                        }
                        else
                        {
                            // It is the top root: restrict its colliders so it NEVER claims child mesh colliders!
                            BoxCollider rootBox = go.GetComponent<BoxCollider>();
                            if (rootBox == null)
                            {
                                rootBox = Undo.AddComponent<BoxCollider>(go);
                                rootBox.size = new Vector3(1f, 1.8f, 0.5f);
                                rootBox.center = new Vector3(0f, 0.9f, 0f);
                            }

                            grab.colliders.Clear();
                            grab.colliders.Add(rootBox);
                            grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Kinematic;
                            EditorUtility.SetDirty(grab);
                            logs.Add($"Isolated XRGrabInteractable on root '{go.name}' to its own BoxCollider handle only.");
                        }
                    }
                }
            }
        }

        // B. Ensure all child XRSimpleInteractables are cleanly and uniquely linked to their own collider
        var simpleInteractables = Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int remappedCount = 0;
        foreach (var simple in simpleInteractables)
        {
            Collider myCol = simple.GetComponent<Collider>();
            if (myCol != null)
            {
                if (simple.colliders.Count != 1 || simple.colliders[0] != myCol)
                {
                    simple.colliders.Clear();
                    simple.colliders.Add(myCol);
                    EditorUtility.SetDirty(simple);
                    remappedCount++;
                }
            }
        }
        if (remappedCount > 0)
        {
            logs.Add($"Re-linked {remappedCount} anatomy mesh colliders directly to their own XRSimpleInteractables.");
        }

        // C. Fix Player Standing Height (1.68m eye level)
        var xrOrigin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include);
        if (xrOrigin != null)
        {
            Undo.RecordObject(xrOrigin, "Fix Player Height");
            xrOrigin.CameraYOffset = 1.68f;
            EditorUtility.SetDirty(xrOrigin);

            var heightMgr = xrOrigin.GetComponent<VRPlayerHeightManager>();
            if (heightMgr == null)
            {
                heightMgr = Undo.AddComponent<VRPlayerHeightManager>(xrOrigin.gameObject);
                heightMgr.targetStandingEyeHeight = 1.68f;
                EditorUtility.SetDirty(heightMgr);
            }

            var cc = xrOrigin.GetComponent<CharacterController>();
            if (cc != null)
            {
                Undo.RecordObject(cc, "Fix CharacterController Height");
                cc.height = 1.8f;
                cc.center = new Vector3(0f, 0.9f, 0f);
                EditorUtility.SetDirty(cc);
            }

            logs.Add("Fixed Player Height: Set camera height to 1.68m (standing adult eye level) and configured VRPlayerHeightManager.");
        }
    }

    private static void FixControllerTags(List<string> logs)
    {
        var allRoots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in allRoots)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                if (t.gameObject.CompareTag("Untagged")) continue;

                // Fix trailing space in tag
                if (t.gameObject.tag.EndsWith(" ") || t.gameObject.tag.StartsWith(" "))
                {
                    string oldTag = t.gameObject.tag;
                    Undo.RecordObject(t.gameObject, "Fix Tag");
                    t.gameObject.tag = "Untagged";
                    logs.Add($"Removed erroneous whitespace tag '{oldTag}' on '{t.name}'.");
                }
            }
        }
    }

    private static void ArrangeAndBeautifyUI(List<string> logs)
    {
        // 1. Find main Canvas
        Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null)
        {
            logs.Add("Warning: No Canvas found in scene.");
            return;
        }

        RectTransform canvasRT = canvas.GetComponent<RectTransform>();
        if (canvasRT != null)
        {
            // Ensure World Space canvas has reasonable VR dimensions
            if (canvasRT.sizeDelta.x < 1000) canvasRT.sizeDelta = new Vector2(1280, 880);
            if (canvasRT.sizeDelta.y < 800) canvasRT.sizeDelta = new Vector2(canvasRT.sizeDelta.x, 880);
        }

        // 2. Build or organize Left Column: SystemMenuPanel
        GameObject menuPanel = EnsureSystemMenuPanel(canvas, logs);

        // 3. Move all loose buttons on the canvas into SystemMenuPanel
        OrganizeSystemButtons(canvas, menuPanel, logs);

        // 4. Build and style Right Column: BodyPartInfoPanel
        EnsureBodyPartInfoPanel(canvas, logs);

        // Force layout refresh
        Canvas.ForceUpdateCanvases();
        if (menuPanel != null) LayoutRebuilder.ForceRebuildLayoutImmediate(menuPanel.GetComponent<RectTransform>());
    }

    private static GameObject EnsureSystemMenuPanel(Canvas canvas, List<string> logs)
    {
        Transform existing = canvas.transform.Find("SystemMenuPanel");
        GameObject panelObj;

        if (existing != null)
        {
            panelObj = existing.gameObject;
        }
        else
        {
            panelObj = new GameObject("SystemMenuPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            Undo.RegisterCreatedObjectUndo(panelObj, "Create SystemMenuPanel");
            panelObj.transform.SetParent(canvas.transform, false);
            logs.Add("Created dedicated 'SystemMenuPanel' container on left side of Canvas.");
        }

        RectTransform rt = panelObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot     = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(40f, 0f);
        rt.sizeDelta = new Vector2(360f, 820f);

        // Sleek dark glass card styling
        Image img = panelObj.GetComponent<Image>();
        img.color = new Color(0.06f, 0.09f, 0.16f, 0.92f); // Deep Slate

        // Vertical Layout Group
        VerticalLayoutGroup vlg = panelObj.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = Undo.AddComponent<VerticalLayoutGroup>(panelObj);
        vlg.padding = new RectOffset(16, 16, 20, 20);
        vlg.spacing = 10f;
        vlg.childControlWidth  = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        // Title Header
        Transform headerTrans = panelObj.transform.Find("MenuHeader");
        if (headerTrans == null)
        {
            GameObject headerObj = new GameObject("MenuHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(headerObj, "Create MenuHeader");
            headerObj.transform.SetParent(panelObj.transform, false);
            headerObj.transform.SetAsFirstSibling();

            RectTransform hRT = headerObj.GetComponent<RectTransform>();
            hRT.sizeDelta = new Vector2(320f, 40f);

            var tmp = headerObj.GetComponent<TextMeshProUGUI>();
            tmp.text = "ANATOMICAL SYSTEMS";
            tmp.fontSize = 20;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.22f, 0.74f, 0.97f); // Sky Blue #38BDF8
            tmp.alignment = TextAlignmentOptions.Center;
        }

        return panelObj;
    }

    private static void OrganizeSystemButtons(Canvas canvas, GameObject menuPanel, List<string> logs)
    {
        // Find all loose buttons that are direct children of Canvas (excluding menuPanel and infoPanel)
        List<Transform> buttonsToMove = new List<Transform>();
        for (int i = 0; i < canvas.transform.childCount; i++)
        {
            Transform child = canvas.transform.GetChild(i);
            if (child.gameObject == menuPanel) continue;
            if (child.name.IndexOf("InfoPanel", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

            Button btn = child.GetComponent<Button>();
            if (btn != null)
            {
                buttonsToMove.Add(child);
            }
        }

        int moved = 0;
        foreach (Transform btnTrans in buttonsToMove)
        {
            Undo.SetTransformParent(btnTrans, menuPanel.transform, "Move Button to SystemMenuPanel");
            moved++;

            // Standardize button appearance
            RectTransform btnRT = btnTrans.GetComponent<RectTransform>();
            btnRT.sizeDelta = new Vector2(320f, 52f);

            // Add LayoutElement to ensure correct height in VerticalLayoutGroup
            LayoutElement le = btnTrans.GetComponent<LayoutElement>();
            if (le == null) le = Undo.AddComponent<LayoutElement>(btnTrans.gameObject);
            le.minHeight = 50f;
            le.preferredHeight = 52f;
            le.flexibleHeight = 0f;

            Image btnImg = btnTrans.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.color = new Color(0.12f, 0.17f, 0.27f, 0.95f); // Modern slate button
            }

            // Ensure readable text
            TMP_Text tmp = btnTrans.GetComponentInChildren<TMP_Text>();
            if (tmp != null)
            {
                tmp.fontSize = 16;
                tmp.color = Color.white;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
            }
        }

        if (moved > 0)
        {
            logs.Add($"Arranged {moved} system buttons into the left SystemMenuPanel without overlap.");
        }
    }

    private static void EnsureBodyPartInfoPanel(Canvas canvas, List<string> logs)
    {
        BodyPartInfoUI infoUI = Object.FindFirstObjectByType<BodyPartInfoUI>(FindObjectsInactive.Include);
        GameObject panelObj = null;

        if (infoUI != null && infoUI.infoPanel != null)
        {
            panelObj = infoUI.infoPanel;
        }
        else
        {
            for (int i = 0; i < canvas.transform.childCount; i++)
            {
                Transform child = canvas.transform.GetChild(i);
                if (child.name.IndexOf("InfoPanel", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    panelObj = child.gameObject;
                    break;
                }
            }
        }

        if (panelObj == null)
        {
            panelObj = new GameObject("BodyPartInfoPanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            Undo.RegisterCreatedObjectUndo(panelObj, "Create BodyPartInfoPanel");
            panelObj.transform.SetParent(canvas.transform, false);
            logs.Add("Created BodyPartInfoPanel.");
        }

        if (infoUI == null)
        {
            infoUI = Undo.AddComponent<BodyPartInfoUI>(panelObj);
        }

        // Position on Right side of Canvas
        RectTransform rt = panelObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot     = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-40f, 0f);
        rt.sizeDelta = new Vector2(620f, 820f);

        // Dark medical card background
        Image img = panelObj.GetComponent<Image>();
        if (img != null)
        {
            img.color = new Color(0.06f, 0.09f, 0.16f, 0.95f);
        }

        // Configure Vertical Layout Group
        VerticalLayoutGroup vlg = panelObj.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = Undo.AddComponent<VerticalLayoutGroup>(panelObj);
        vlg.padding = new RectOffset(24, 24, 20, 20);
        vlg.spacing = 10f;
        vlg.childControlWidth  = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        // Ensure Top Header Bar (Badge + Close Button)
        EnsureHeaderBar(panelObj, infoUI);

        // Text Fields in neat sequential card rows
        TMP_Text nameText = EnsureFieldRow(panelObj, "BodyPartName", "Select a Body Part", 26, FontStyles.Bold, new Color(0.96f, 0.62f, 0.04f)); // Amber Gold
        TMP_Text sysText  = EnsureFieldRow(panelObj, "BodyPartSystem", "System Name", 16, FontStyles.Italic, new Color(0.02f, 0.71f, 0.83f)); // Cyan
        TMP_Text typeText = EnsureFieldRow(panelObj, "BodyPartType", "Classification / Type", 14, FontStyles.Normal, new Color(0.58f, 0.64f, 0.72f)); // Muted slate

        // Divider
        EnsureDivider(panelObj);

        // Description, Function, Location
        TMP_Text descText = EnsureSectionBlock(panelObj, "BodyPartDescription", "DESCRIPTION", "Point and click any anatomy structure to inspect details.", 15);
        TMP_Text funcText = EnsureSectionBlock(panelObj, "BodyPartFunction", "PHYSIOLOGICAL FUNCTION", "Functional details will appear here.", 14);
        TMP_Text locText  = EnsureSectionBlock(panelObj, "BodyPartLocation", "ANATOMICAL LOCATION", "Location details will appear here.", 14);

        // Wire references and VR Right-Front settings to BodyPartInfoUI
        Undo.RecordObject(infoUI, "Update BodyPartInfoUI References");
        infoUI.infoPanel                = panelObj;
        infoUI.bodyPartName             = nameText;
        infoUI.bodyPartSystem           = sysText;
        infoUI.bodyPartType             = typeText;
        infoUI.bodyPartDescription      = descText;
        infoUI.bodyPartFunction         = funcText;
        infoUI.bodyPartLocation         = locText;
        infoUI.enableVRPositioning      = true;
        infoUI.normalHorizontalAngle    = 30f;
        infoUI.rightSideHorizontalAngle = 42f;
        infoUI.distanceFromHeadset      = 0.9f;
        infoUI.verticalOffset           = -0.12f;
        infoUI.canvasWorldScale         = new Vector3(0.0008f, 0.0008f, 0.0008f);

        EditorUtility.SetDirty(infoUI);
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        logs.Add("Structured BodyPartInfoPanel with Right-Front VR layout (+30°), clean typography, sections, and close button.");
    }

    private static void EnsureHeaderBar(GameObject parent, BodyPartInfoUI infoUI)
    {
        Transform headerTrans = parent.transform.Find("HeaderBar");
        GameObject headerObj;

        if (headerTrans == null)
        {
            headerObj = new GameObject("HeaderBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            Undo.RegisterCreatedObjectUndo(headerObj, "Create HeaderBar");
            headerObj.transform.SetParent(parent.transform, false);
            headerObj.transform.SetAsFirstSibling();
        }
        else
        {
            headerObj = headerTrans.gameObject;
        }

        RectTransform hRT = headerObj.GetComponent<RectTransform>();
        hRT.sizeDelta = new Vector2(570f, 44f);

        LayoutElement hLE = headerObj.GetComponent<LayoutElement>();
        if (hLE == null) hLE = Undo.AddComponent<LayoutElement>(headerObj);
        hLE.minHeight = 44f;
        hLE.preferredHeight = 44f;

        HorizontalLayoutGroup hlg = headerObj.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = Undo.AddComponent<HorizontalLayoutGroup>(headerObj);
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;

        // Badge label
        Transform badgeTrans = headerObj.transform.Find("Badge");
        if (badgeTrans == null)
        {
            GameObject badge = new GameObject("Badge", typeof(RectTransform), typeof(TextMeshProUGUI));
            badge.transform.SetParent(headerObj.transform, false);
            var bTMP = badge.GetComponent<TextMeshProUGUI>();
            bTMP.text = "ANATOMY INSPECTOR";
            bTMP.fontSize = 14;
            bTMP.fontStyle = FontStyles.Bold;
            bTMP.color = new Color(0.58f, 0.64f, 0.72f);
            bTMP.alignment = TextAlignmentOptions.MidlineLeft;

            var le = badge.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
        }

        // Close Button
        Transform closeTrans = headerObj.transform.Find("CloseButton");
        GameObject closeObj;
        if (closeTrans == null)
        {
            closeObj = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeObj.transform.SetParent(headerObj.transform, false);

            var cLE = closeObj.AddComponent<LayoutElement>();
            cLE.minWidth = 36f;
            cLE.preferredWidth = 36f;
            cLE.minHeight = 36f;
            cLE.preferredHeight = 36f;

            var cImg = closeObj.GetComponent<Image>();
            cImg.color = new Color(0.93f, 0.27f, 0.27f, 0.9f); // Red

            GameObject xText = new GameObject("X", typeof(RectTransform), typeof(TextMeshProUGUI));
            xText.transform.SetParent(closeObj.transform, false);
            var xTMP = xText.GetComponent<TextMeshProUGUI>();
            xTMP.text = "X";
            xTMP.fontSize = 18;
            xTMP.color = Color.white;
            xTMP.alignment = TextAlignmentOptions.Center;

            Button btn = closeObj.GetComponent<Button>();
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, infoUI.HidePanel);
        }
    }

    private static void EnsureDivider(GameObject parent)
    {
        Transform div = parent.transform.Find("DividerLine");
        if (div == null)
        {
            GameObject divObj = new GameObject("DividerLine", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            divObj.transform.SetParent(parent.transform, false);

            var img = divObj.GetComponent<Image>();
            img.color = new Color(0.2f, 0.25f, 0.33f, 0.7f);

            var le = divObj.GetComponent<LayoutElement>();
            le.minHeight = 2f;
            le.preferredHeight = 2f;
            le.flexibleHeight = 0f;
        }
    }

    private static TMP_Text EnsureFieldRow(GameObject parent, string name, string placeholder, float fontSize, FontStyles style, Color color)
    {
        Transform child = parent.transform.Find(name);
        GameObject go = (child != null) ? child.gameObject : null;

        if (go == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent.transform, false);
        }

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = Undo.AddComponent<TextMeshProUGUI>(go);

        tmp.text = placeholder;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.textWrappingMode = TextWrappingModes.Normal;

        LayoutElement le = go.GetComponent<LayoutElement>();
        if (le == null) le = Undo.AddComponent<LayoutElement>(go);
        le.minHeight = fontSize + 8f;
        le.flexibleHeight = 0f;

        return tmp;
    }

    private static TMP_Text EnsureSectionBlock(GameObject parent, string name, string sectionTitle, string placeholder, float fontSize)
    {
        // Container
        string blockName = name + "_Section";
        Transform sectionTrans = parent.transform.Find(blockName);
        GameObject sectionObj;

        if (sectionTrans == null)
        {
            sectionObj = new GameObject(blockName, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            Undo.RegisterCreatedObjectUndo(sectionObj, "Create " + blockName);
            sectionObj.transform.SetParent(parent.transform, false);
        }
        else
        {
            sectionObj = sectionTrans.gameObject;
        }

        var vlg = sectionObj.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = sectionObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 3f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Subtitle
        Transform titleTrans = sectionObj.transform.Find("SectionTitle");
        if (titleTrans == null)
        {
            GameObject tObj = new GameObject("SectionTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            tObj.transform.SetParent(sectionObj.transform, false);
            var titleTMP = tObj.GetComponent<TextMeshProUGUI>();
            titleTMP.text = sectionTitle;
            titleTMP.fontSize = 11;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.color = new Color(0.39f, 0.45f, 0.55f); // Subdued slate
        }

        // Content
        Transform contentTrans = sectionObj.transform.Find(name);
        GameObject contentObj;
        if (contentTrans == null)
        {
            contentObj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            contentObj.transform.SetParent(sectionObj.transform, false);
        }
        else
        {
            contentObj = contentTrans.gameObject;
        }

        var tmp = contentObj.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = contentObj.AddComponent<TextMeshProUGUI>();
        tmp.text = placeholder;
        tmp.fontSize = fontSize;
        tmp.color = new Color(0.89f, 0.91f, 0.94f);
        tmp.textWrappingMode = TextWrappingModes.Normal;

        return tmp;
    }

    private static void FixAnatomyLayersAlignmentAndScale(List<string> logs)
    {
        GameObject anatomyRoot = GameObject.Find("Human male anatomy");
        if (anatomyRoot == null)
        {
            var allGo = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in allGo)
            {
                if (go.name.ToLower().Contains("male anatomy") || (go.name.ToLower().Contains("human") && go.name.ToLower().Contains("anatomy")))
                {
                    anatomyRoot = go;
                    break;
                }
            }
        }

        if (anatomyRoot == null)
        {
            logs.Add("Could not find 'Human male anatomy' GameObject in scene.");
            return;
        }

        Undo.RecordObject(anatomyRoot.transform, "Fix Anatomy Root Transform");

        // Ground anatomy root
        Vector3 rootPos = anatomyRoot.transform.position;
        rootPos.y = 0f;
        anatomyRoot.transform.position = rootPos;

        // Ensure all child layers (Male Body, Skeletal systems, Muscular, etc.) share uniform local transform
        Transform referenceChild = null;
        for (int i = 0; i < anatomyRoot.transform.childCount; i++)
        {
            Transform child = anatomyRoot.transform.GetChild(i);
            string cname = child.name.ToLower();
            if (cname.Contains("canvas") || cname.Contains("menu"))
            {
                Undo.RecordObject(child.gameObject, "Deactivate Canvas under Anatomy");
                child.gameObject.SetActive(false);
                continue;
            }

            if (cname.Contains("body") || cname.Contains("male"))
            {
                referenceChild = child;
                break;
            }
        }

        Vector3 targetLocalPos = (referenceChild != null) ? referenceChild.localPosition : Vector3.zero;
        Quaternion targetLocalRot = (referenceChild != null) ? referenceChild.localRotation : Quaternion.identity;
        Vector3 targetLocalScale = (referenceChild != null) ? referenceChild.localScale : Vector3.one;

        int alignedCount = 0;
        for (int i = 0; i < anatomyRoot.transform.childCount; i++)
        {
            Transform child = anatomyRoot.transform.GetChild(i);
            string cname = child.name.ToLower();
            if (cname.Contains("canvas") || cname.Contains("menu")) continue;

            Undo.RecordObject(child, "Align Child Layer " + child.name);
            child.localPosition = targetLocalPos;
            child.localRotation = targetLocalRot;
            child.localScale = targetLocalScale;

            // Remove any XRGrabInteractable or Rigidbody from child layer so grabbing only grabs the whole model
            var childGrabs = child.GetComponents<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            foreach (var g in childGrabs) Undo.DestroyObjectImmediate(g);

            var childRBs = child.GetComponents<Rigidbody>();
            foreach (var rb in childRBs) Undo.DestroyObjectImmediate(rb);

            alignedCount++;
        }
        logs.Add($"Aligned {alignedCount} anatomical layers (Skeletal, Muscular, Organs, Skin) to matching transform.");

        // Calculate current height of character model
        var renderers = anatomyRoot.GetComponentsInChildren<Renderer>(false);
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int r = 1; r < renderers.Length; r++)
            {
                if (renderers[r].gameObject.activeInHierarchy)
                    b.Encapsulate(renderers[r].bounds);
            }

            float currentHeight = b.size.y;
            float targetHeight = 1.78f; // Standard adult human height in meters
            if (currentHeight > 0.1f && Mathf.Abs(currentHeight - targetHeight) > 0.15f)
            {
                float factor = targetHeight / currentHeight;
                anatomyRoot.transform.localScale *= factor;
                logs.Add($"Scaled anatomy model from {currentHeight:F2}m to life-sized adult height ({targetHeight:F2}m).");
            }
        }

        // Configure AnatomyExplodedView on anatomyRoot
        var oldExploded = anatomyRoot.GetComponentsInChildren<AnatomyExplodedView>(true);
        foreach (var ex in oldExploded)
        {
            if (ex.gameObject != anatomyRoot)
            {
                Undo.DestroyObjectImmediate(ex);
            }
        }

        // Clean up duplicate AnatomyTransformManipulators on children
        var oldManipulators = anatomyRoot.GetComponentsInChildren<AnatomyTransformManipulator>(true);
        foreach (var m in oldManipulators)
        {
            if (m.gameObject != anatomyRoot)
            {
                Undo.DestroyObjectImmediate(m);
            }
        }

        AnatomyExplodedView rootExploded = anatomyRoot.GetComponent<AnatomyExplodedView>();
        if (rootExploded == null) rootExploded = Undo.AddComponent<AnatomyExplodedView>(anatomyRoot);
        rootExploded.AutoDetectLayers();
        rootExploded.InitializeLayers();
        rootExploded.SetSeparation(0f);
        logs.Add("Re-initialized Exploded View on Anatomy Root (layers assembled/folded).");

        // Setup touch, point, highlight, and information on ALL body parts
        SetupAllBodyPartsInteraction(anatomyRoot);
        int totalParts = anatomyRoot.GetComponentsInChildren<BodyPartSelector>(true).Length;
        logs.Add($"Configured interactive selection, touch, and database info on ALL {totalParts} body parts across all 8 systems.");
    }

    public static void SetupAllBodyPartsInteraction(GameObject anatomyRoot)
    {
        if (anatomyRoot == null) return;

        AnatomyDatabase db = Resources.Load<AnatomyDatabase>("AnatomyDatabase");
        if (db == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:AnatomyDatabase");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                db = AssetDatabase.LoadAssetAtPath<AnatomyDatabase>(path);
            }
        }

        var meshFilters = anatomyRoot.GetComponentsInChildren<MeshFilter>(true);
        var skinnedMeshes = anatomyRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        HashSet<GameObject> processed = new HashSet<GameObject>();
        int configuredCount = 0;

        foreach (var mf in meshFilters)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            GameObject go = mf.gameObject;
            if (go.name.ToLower().Contains("canvas") || go.name.ToLower().Contains("menu")) continue;
            if (processed.Contains(go)) continue;
            processed.Add(go);

            ConfigureSingleBodyPart(go, mf.sharedMesh, db);
            configuredCount++;
        }

        foreach (var smr in skinnedMeshes)
        {
            if (smr == null || smr.sharedMesh == null) continue;
            GameObject go = smr.gameObject;
            if (go.name.ToLower().Contains("canvas") || go.name.ToLower().Contains("menu")) continue;
            if (processed.Contains(go)) continue;
            processed.Add(go);

            ConfigureSingleBodyPart(go, smr.sharedMesh, db);
            configuredCount++;
        }

        Debug.Log($"[AnatomySceneRepairTool] Successfully configured interactions on ALL {configuredCount} body parts!");
    }

    private static void ConfigureSingleBodyPart(GameObject go, Mesh mesh, AnatomyDatabase db)
    {
        Undo.RecordObject(go, "Configure Body Part " + go.name);

        // 1. MeshCollider
        MeshCollider mc = go.GetComponent<MeshCollider>();
        if (mc == null)
        {
            mc = Undo.AddComponent<MeshCollider>(go);
        }
        if (mc.sharedMesh == null && mesh != null)
        {
            mc.sharedMesh = mesh;
        }
        mc.convex = false;

        // 2. BodyPartSelector
        BodyPartSelector selector = go.GetComponent<BodyPartSelector>();
        if (selector == null)
        {
            selector = Undo.AddComponent<BodyPartSelector>(go);
        }

        if (string.IsNullOrEmpty(selector.displayName))
        {
            selector.displayName = AnatomyNamingUtils.FormatDisplayName(go.name);
        }
        if (string.IsNullOrEmpty(selector.systemName))
        {
            selector.systemName = selector.GetSystemName();
        }
        if (db != null && selector.anatomyDatabase == null)
        {
            selector.anatomyDatabase = db;
        }

        // 3. AnatomyHighlight
        AnatomyHighlight highlight = go.GetComponent<AnatomyHighlight>();
        if (highlight == null)
        {
            highlight = Undo.AddComponent<AnatomyHighlight>(go);
        }

        // 4. XRSimpleInteractable
        var interactable = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        if (interactable == null)
        {
            interactable = Undo.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>(go);
        }

        interactable.distanceCalculationMode = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.DistanceCalculationMode.TransformPosition;

        // Add persistent listeners
        AddSelectListener(interactable, selector);
        AddHoverListeners(interactable, selector);

        EditorUtility.SetDirty(go);
    }

    private static void AddSelectListener(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable, BodyPartSelector selector)
    {
        int count = interactable.selectEntered.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (interactable.selectEntered.GetPersistentTarget(i) == selector &&
                interactable.selectEntered.GetPersistentMethodName(i) == nameof(selector.OnSelectEntered))
                return;
        }
        UnityEditor.Events.UnityEventTools.AddPersistentListener(interactable.selectEntered, selector.OnSelectEntered);
    }

    private static void AddHoverListeners(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable, BodyPartSelector selector)
    {
        int enterCount = interactable.firstHoverEntered.GetPersistentEventCount();
        bool hasEnter = false;
        for (int i = 0; i < enterCount; i++)
        {
            if (interactable.firstHoverEntered.GetPersistentTarget(i) == selector &&
                interactable.firstHoverEntered.GetPersistentMethodName(i) == nameof(selector.OnHoverEntered))
            {
                hasEnter = true;
                break;
            }
        }
        if (!hasEnter)
        {
            UnityEditor.Events.UnityEventTools.AddPersistentListener(interactable.firstHoverEntered, selector.OnHoverEntered);
        }

        int exitCount = interactable.lastHoverExited.GetPersistentEventCount();
        bool hasExit = false;
        for (int i = 0; i < exitCount; i++)
        {
            if (interactable.lastHoverExited.GetPersistentTarget(i) == selector &&
                interactable.lastHoverExited.GetPersistentMethodName(i) == nameof(selector.OnHoverExited))
            {
                hasExit = true;
                break;
            }
        }
        if (!hasExit)
        {
            UnityEditor.Events.UnityEventTools.AddPersistentListener(interactable.lastHoverExited, selector.OnHoverExited);
        }
    }

    private static void ConfigureHandDashboard(List<string> logs)
    {
        SetupHandDashboardTool.ConfigureHandDashboard(false);
        logs.Add("Configured modern AnatomyVR Hand Dashboard anchored to Left Hand.");
    }
}
