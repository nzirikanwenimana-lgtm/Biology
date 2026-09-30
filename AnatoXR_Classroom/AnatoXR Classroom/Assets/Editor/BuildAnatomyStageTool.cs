using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Professional Editor tool to construct and configure the movable AnatomyDisplayStage in AnatoXR Classroom.
/// </summary>
[InitializeOnLoad]
public static class BuildAnatomyStageTool
{
    private static bool s_HasRunOnceThisSession = false;

    static BuildAnatomyStageTool()
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
            List<string> logs = new List<string>();
            BuildOrUpdateStageInternal(logs);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[BuildAnatomyStageTool] Automatically configured AnatomyDisplayStage for: " + scene.name);
        }
    }

    private const string STAGE_NAME = "AnatomyDisplayStage";
    private const string DISPLAY_CIRCLE_NAME = "DisplayCircle";
    private const string STAGE_HANDLE_NAME = "StageGrabHandle";
    private const string ANATOMY_CENTER_NAME = "AnatomyCenter";
    private const string HUMAN_BODY_NAME = "HumanBody";
    private const string SYSTEM_VISUALIZATIONS_NAME = "SystemVisualizations";
    private const string ANATOMY_EFFECTS_NAME = "AnatomyEffects";

    [MenuItem("Anatomy/Stage/Build or Update Anatomy Display Stage", false, 5)]
    public static void BuildOrUpdateStageMenu()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.isLoaded) return;

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Build Anatomy Display Stage");
        int group = Undo.GetCurrentGroup();

        List<string> logs = new List<string>();
        GameObject stageGo = BuildOrUpdateStageInternal(logs);

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(activeScene);

        string report = string.Join("\n• ", logs);
        Debug.Log($"[BuildAnatomyStageTool] Complete:\n• {report}");
        EditorUtility.DisplayDialog("Anatomy Display Stage", 
            $"AnatomyDisplayStage built and configured successfully!\n\n• {report}", "OK");

        if (stageGo != null)
        {
            Selection.activeGameObject = stageGo;
        }
    }

    public static GameObject BuildOrUpdateStageInternal(List<string> logs)
    {
        // 1. Locate or create master AnatomyDisplayStage
        GameObject stageGo = GameObject.Find(STAGE_NAME);
        bool isNewStage = (stageGo == null);

        // Find existing anatomy root in the scene if stage is new
        GameObject existingModel = null;
        if (isNewStage)
        {
            existingModel = GameObject.Find("Human male anatomy");
            if (existingModel == null)
            {
                existingModel = GameObject.Find(HUMAN_BODY_NAME);
            }
            if (existingModel == null)
            {
                var controller = Object.FindFirstObjectByType<AnatomySystemController>(FindObjectsInactive.Include);
                if (controller != null)
                {
                    existingModel = controller.gameObject;
                }
            }
        }

        Vector3 initialStagePos = Vector3.forward * 1.8f;
        if (existingModel != null)
        {
            initialStagePos = existingModel.transform.position;
        }
        initialStagePos.y = 0f; // Ground to floor

        if (isNewStage)
        {
            stageGo = new GameObject(STAGE_NAME);
            Undo.RegisterCreatedObjectUndo(stageGo, "Create AnatomyDisplayStage");
            stageGo.transform.position = initialStagePos;
            stageGo.transform.rotation = Quaternion.identity;
            stageGo.transform.localScale = Vector3.one;
            logs.Add($"Created master '{STAGE_NAME}' at {initialStagePos}.");
        }
        else
        {
            Undo.RecordObject(stageGo.transform, "Update AnatomyDisplayStage Transform");
            Vector3 p = stageGo.transform.position;
            p.y = 0f;
            stageGo.transform.position = p;
            logs.Add($"Found existing '{STAGE_NAME}'. Updating hierarchy.");
        }

        // 2. Configure Rigidbody on stage
        Rigidbody stageRb = stageGo.GetComponent<Rigidbody>();
        if (stageRb == null)
        {
            stageRb = Undo.AddComponent<Rigidbody>(stageGo);
        }
        stageRb.isKinematic = true;
        stageRb.useGravity = false;

        // 3. Configure AnatomyDisplayStage component
        AnatomyDisplayStage stageComp = stageGo.GetComponent<AnatomyDisplayStage>();
        if (stageComp == null)
        {
            stageComp = Undo.AddComponent<AnatomyDisplayStage>(stageGo);
        }
        stageComp.lockToFloorHeight = true;
        stageComp.floorHeight = 0f;
        stageComp.targetHumanHeight = 1.78f;

        // 4. Configure XRGrabInteractable on stage
        XRGrabInteractable stageGrab = stageGo.GetComponent<XRGrabInteractable>();
        if (stageGrab == null)
        {
            stageGrab = Undo.AddComponent<XRGrabInteractable>(stageGo);
        }
        stageGrab.movementType = XRBaseInteractable.MovementType.Kinematic;
        stageGrab.throwOnDetach = false;
        stageGrab.trackPosition = true;
        stageGrab.trackRotation = true;
        stageGrab.colliders.Clear();

        // 5. Configure AnatomyTransformManipulator on stage
        AnatomyTransformManipulator manip = stageGo.GetComponent<AnatomyTransformManipulator>();
        if (manip == null)
        {
            manip = Undo.AddComponent<AnatomyTransformManipulator>(stageGo);
        }
        manip.targetTransform = stageGo.transform;
        manip.enableDesktopControls = true;

        // ---------------------------------------------------------------------
        // 6. Child Hierarchy
        // ---------------------------------------------------------------------

        // A. DisplayCircle (Visual dais on floor)
        Transform displayCircle = stageGo.transform.Find(DISPLAY_CIRCLE_NAME);
        if (displayCircle == null)
        {
            GameObject circleGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            circleGo.name = DISPLAY_CIRCLE_NAME;
            Undo.RegisterCreatedObjectUndo(circleGo, "Create DisplayCircle");
            circleGo.transform.SetParent(stageGo.transform, false);
            circleGo.transform.localPosition = new Vector3(0f, 0.006f, 0f);
            circleGo.transform.localRotation = Quaternion.identity;
            circleGo.transform.localScale = new Vector3(1.7f, 0.012f, 1.7f);

            // Material styling
            Renderer r = circleGo.GetComponent<Renderer>();
            if (r != null)
            {
                Material daisMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                daisMat.name = "AnatomyStageDais_Mat";
                daisMat.color = new Color(0.08f, 0.11f, 0.15f, 1.0f); // Sleek medical dark slate
                if (daisMat.HasProperty("_Smoothness")) daisMat.SetFloat("_Smoothness", 0.65f);
                r.sharedMaterial = daisMat;
            }

            // Remove cylinder collider from dais so teleportation and floor rays pass through cleanly
            Collider daisCol = circleGo.GetComponent<Collider>();
            if (daisCol != null)
            {
                Undo.DestroyObjectImmediate(daisCol);
            }

            displayCircle = circleGo.transform;
            logs.Add("Created visual presentation dais 'DisplayCircle' (1.7m diameter).");
        }

        // B. Dedicated Grab Handle on DisplayCircle
        Transform handleTrans = displayCircle.Find(STAGE_HANDLE_NAME);
        if (handleTrans == null)
        {
            GameObject handleGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handleGo.name = STAGE_HANDLE_NAME;
            Undo.RegisterCreatedObjectUndo(handleGo, "Create StageGrabHandle");
            handleGo.transform.SetParent(displayCircle, false);
            // Placed at the front edge of the stage dais
            handleGo.transform.localPosition = new Vector3(0f, 2.5f, 0.48f);
            handleGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            handleGo.transform.localScale = new Vector3(0.24f, 0.03f, 0.08f);

            Renderer hr = handleGo.GetComponent<Renderer>();
            if (hr != null)
            {
                Material hMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                hMat.name = "AnatomyStageHandle_Mat";
                hMat.color = new Color(0.15f, 0.55f, 0.95f, 0.9f); // Medical cyan-blue
                if (hMat.HasProperty("_EmissionColor"))
                {
                    hMat.SetColor("_EmissionColor", new Color(0.15f, 0.55f, 0.95f) * 0.5f);
                    hMat.EnableKeyword("_EMISSION");
                }
                hr.sharedMaterial = hMat;
            }

            // Ensure collider is configured
            Collider hCol = handleGo.GetComponent<Collider>();
            if (hCol == null)
            {
                hCol = Undo.AddComponent<CapsuleCollider>(handleGo);
            }

            AnatomyStageHandle handleScript = Undo.AddComponent<AnatomyStageHandle>(handleGo);
            handleScript.stage = stageComp;
            handleScript.handleRenderer = hr;

            handleTrans = handleGo.transform;
            logs.Add("Created dedicated 'StageGrabHandle' on DisplayCircle.");
        }

        // Add handle collider to stage grab interactable
        Collider handleCollider = handleTrans.GetComponent<Collider>();
        if (handleCollider != null)
        {
            stageGrab.colliders.Add(handleCollider);
        }

        // C. AnatomyCenter
        Transform anatomyCenter = stageGo.transform.Find(ANATOMY_CENTER_NAME);
        if (anatomyCenter == null)
        {
            GameObject centerGo = new GameObject(ANATOMY_CENTER_NAME);
            Undo.RegisterCreatedObjectUndo(centerGo, "Create AnatomyCenter");
            centerGo.transform.SetParent(stageGo.transform, false);
            centerGo.transform.localPosition = Vector3.zero;
            centerGo.transform.localRotation = Quaternion.identity;
            centerGo.transform.localScale = Vector3.one;
            anatomyCenter = centerGo.transform;
            logs.Add("Created exact center anchor 'AnatomyCenter' (0,0,0).");
        }

        // D. HumanBody
        Transform humanBody = stageGo.transform.Find(HUMAN_BODY_NAME);
        if (humanBody == null)
        {
            // If existing model was found, adopt it
            if (existingModel != null && existingModel != stageGo)
            {
                Undo.SetTransformParent(existingModel.transform, stageGo.transform, "Parent Anatomy to Stage");
                existingModel.name = HUMAN_BODY_NAME;
                humanBody = existingModel.transform;
                logs.Add($"Moved existing anatomy model into '{STAGE_NAME}/{HUMAN_BODY_NAME}'.");
            }
            else
            {
                GameObject bodyGo = new GameObject(HUMAN_BODY_NAME);
                Undo.RegisterCreatedObjectUndo(bodyGo, "Create HumanBody");
                bodyGo.transform.SetParent(stageGo.transform, false);
                humanBody = bodyGo.transform;
                logs.Add($"Created '{HUMAN_BODY_NAME}' under stage.");
            }
        }

        Undo.RecordObject(humanBody, "Align HumanBody to Center");
        humanBody.localPosition = Vector3.zero;
        humanBody.localRotation = Quaternion.identity;

        // E. SystemVisualizations
        Transform systemViz = stageGo.transform.Find(SYSTEM_VISUALIZATIONS_NAME);
        if (systemViz == null)
        {
            GameObject vizGo = new GameObject(SYSTEM_VISUALIZATIONS_NAME);
            Undo.RegisterCreatedObjectUndo(vizGo, "Create SystemVisualizations");
            vizGo.transform.SetParent(stageGo.transform, false);
            vizGo.transform.localPosition = Vector3.zero;
            vizGo.transform.localRotation = Quaternion.identity;
            vizGo.transform.localScale = Vector3.one;
            systemViz = vizGo.transform;
        }

        // F. AnatomyEffects
        Transform effects = stageGo.transform.Find(ANATOMY_EFFECTS_NAME);
        if (effects == null)
        {
            GameObject fxGo = new GameObject(ANATOMY_EFFECTS_NAME);
            Undo.RegisterCreatedObjectUndo(fxGo, "Create AnatomyEffects");
            fxGo.transform.SetParent(stageGo.transform, false);
            fxGo.transform.localPosition = Vector3.zero;
            fxGo.transform.localRotation = Quaternion.identity;
            fxGo.transform.localScale = Vector3.one;
            effects = fxGo.transform;
        }

        // Assign references in stage component
        stageComp.displayCircle = displayCircle;
        stageComp.stageHandle = handleTrans;
        stageComp.anatomyCenter = anatomyCenter;
        stageComp.humanBody = humanBody;
        stageComp.systemVisualizations = systemViz;
        stageComp.anatomyEffects = effects;

        // ---------------------------------------------------------------------
        // 7. Align All Anatomical Systems to AnatomyCenter & Uniform Scale
        // ---------------------------------------------------------------------
        int childLayerCount = 0;
        Transform referenceChild = null;

        for (int i = 0; i < humanBody.childCount; i++)
        {
            Transform child = humanBody.GetChild(i);
            string cname = child.name.ToLower();
            if (cname.Contains("canvas") || cname.Contains("camera") || cname.Contains("light")) continue;

            if (cname.Contains("body") || cname.Contains("male") || cname.Contains("skeleton"))
            {
                referenceChild = child;
                break;
            }
        }

        Vector3 targetChildScale = (referenceChild != null) ? referenceChild.localScale : Vector3.one;

        for (int i = 0; i < humanBody.childCount; i++)
        {
            Transform child = humanBody.GetChild(i);
            string cname = child.name.ToLower();
            if (cname.Contains("canvas") || cname.Contains("camera") || cname.Contains("light")) continue;

            Undo.RecordObject(child, "Center Layer " + child.name);
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            if (targetChildScale != Vector3.one && targetChildScale != Vector3.zero)
            {
                child.localScale = targetChildScale;
            }

            // Ensure child layer doesn't possess conflicting grab components
            var grabs = child.GetComponents<XRGrabInteractable>();
            foreach (var g in grabs) Undo.DestroyObjectImmediate(g);
            var rbs = child.GetComponents<Rigidbody>();
            foreach (var rb in rbs) Undo.DestroyObjectImmediate(rb);

            childLayerCount++;
        }
        logs.Add($"Aligned {childLayerCount} anatomy layers (Skeleton, Muscles, Organs, etc.) centered on AnatomyCenter.");

        // 8. Adult Life-Size Scaling
        stageComp.CalibrateAdultScale();

        // 9. Re-sync or Create AnatomySystemController on HumanBody
        AnatomySystemController sysCtrl = stageGo.GetComponentInChildren<AnatomySystemController>(true);
        if (sysCtrl == null)
        {
            sysCtrl = Object.FindFirstObjectByType<AnatomySystemController>(FindObjectsInactive.Include);
        }
        if (sysCtrl == null)
        {
            sysCtrl = Undo.AddComponent<AnatomySystemController>(humanBody.gameObject);
            logs.Add("Created AnatomySystemController on HumanBody.");
        }

        if (sysCtrl != null)
        {
            Undo.RecordObject(sysCtrl, "Update AnatomySystemController references");
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
            logs.Add("Updated AnatomySystemController system layer references to AnatomyDisplayStage children.");

            // Connect AnatomyDashboardUI directly to AnatomySystemController
            var dashUI = Object.FindFirstObjectByType<AnatomyDashboardUI>(FindObjectsInactive.Include);
            if (dashUI != null)
            {
                Undo.RecordObject(dashUI, "Connect Dashboard to AnatomySystemController");
                dashUI.systemController = sysCtrl;
                dashUI.ConnectToBodyAnatomy();
                EditorUtility.SetDirty(dashUI);
                logs.Add("Connected AnatomyDashboardUI directly to AnatomySystemController.");
            }
        }

        // 10. Update BodyPartInfoUI anatomy root
        BodyPartInfoUI infoUI = Object.FindFirstObjectByType<BodyPartInfoUI>(FindObjectsInactive.Include);
        if (infoUI != null)
        {
            Undo.RecordObject(infoUI, "Update BodyPartInfoUI stage anchor");
            infoUI.anatomyModelRoot = anatomyCenter;
            EditorUtility.SetDirty(infoUI);
            logs.Add("Linked BodyPartInfoUI to AnatomyCenter.");
        }

        EditorUtility.SetDirty(stageGo);
        EditorUtility.SetDirty(stageComp);

        return stageGo;
    }
}
