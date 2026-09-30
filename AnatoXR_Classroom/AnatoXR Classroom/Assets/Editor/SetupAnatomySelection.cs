using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using System.Collections.Generic;

/// <summary>
/// Unity Editor tools for the AnatoXR anatomy system.
///
/// MENU COMMANDS:
///   Anatomy → Setup Selected Anatomy         — assigns UI refs + database to all BodyPartSelectors under selection
///   Anatomy → Build/Update Anatomy Database  — scans selection for MeshRenderers, creates database entries by name
///   Anatomy → Create Anatomy Database Asset  — creates Assets/Resources/AnatomyDatabase.asset if missing
/// </summary>
public class SetupAnatomySelection
{
    // =========================================================================
    // MENU: Setup Selected Anatomy
    // =========================================================================
    [MenuItem("Anatomy/Advanced/Setup Selected Anatomy", false, 31)]
    public static void SetupSelectedAnatomy()
    {
        // 1. Validate Selection
        if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "No Anatomy Objects Selected",
                "Please select one or more anatomy GameObjects (or the root Anatomy GameObject) in the Hierarchy before running this command.",
                "OK"
            );
            return;
        }

        // 2. Auto-clean any non-anatomy environment objects before setup
        CleanNonAnatomyObjectsInternal(false);

        // 3. Find the shared UI (including inactive GameObjects)
        GameObject infoPanel = FindPanelInScene();

        if (infoPanel == null)
        {
            bool createNew = EditorUtility.DisplayDialog(
                "BodyPartInfoPanel Not Found",
                "Could not find 'BodyPartInfoPanel' or any Info Panel in the active scene.\n\nWould you like to automatically create a default BodyPartInfoPanel Canvas in the scene?",
                "Yes, Create Panel",
                "Cancel"
            );

            if (createNew)
            {
                infoPanel = CreateDefaultInfoPanel();
            }
            else
            {
                Debug.LogError("[SetupAnatomySelection] Setup aborted: BodyPartInfoPanel is required.");
                return;
            }
        }

        // 4. Find Text Mesh Pro UI elements (searches inside panel first, including inactive)
        TMP_Text bodyPartName        = FindTMP(infoPanel, "BodyPartName",        "Title",    "Name");
        TMP_Text bodyPartSystem      = FindTMP(infoPanel, "BodyPartSystem",      "System");
        TMP_Text bodyPartType        = FindTMP(infoPanel, "BodyPartType",        "Type",     "Category");
        TMP_Text bodyPartDescription = FindTMP(infoPanel, "BodyPartDescription", "Description", "Desc", "Info", "Detail");
        TMP_Text bodyPartFunction    = FindTMP(infoPanel, "BodyPartFunction",    "Function", "Func");
        TMP_Text bodyPartLocation    = FindTMP(infoPanel, "BodyPartLocation",    "Location", "Loc");

        // 5. Ensure centralized BodyPartInfoUI component is configured on the panel
        BodyPartInfoUI uiManager = infoPanel.GetComponent<BodyPartInfoUI>();
        if (uiManager == null)
        {
            uiManager = Undo.AddComponent<BodyPartInfoUI>(infoPanel);
        }

        if (uiManager != null)
        {
            SerializedObject serializedUI = new SerializedObject(uiManager);
            SetObjectReference(serializedUI, "infoPanel",            infoPanel);
            SetObjectReference(serializedUI, "bodyPartName",         bodyPartName);
            SetObjectReference(serializedUI, "bodyPartSystem",       bodyPartSystem);
            SetObjectReference(serializedUI, "bodyPartType",         bodyPartType);
            SetObjectReference(serializedUI, "bodyPartDescription",  bodyPartDescription);
            SetObjectReference(serializedUI, "bodyPartFunction",     bodyPartFunction);
            SetObjectReference(serializedUI, "bodyPartLocation",     bodyPartLocation);
            serializedUI.ApplyModifiedProperties();
            EditorUtility.SetDirty(uiManager);
        }

        // 5b. Ensure AnatomyInteractionRaycaster is in the scene for desktop & VR point-and-click
        AnatomyInteractionRaycaster raycaster = UnityEngine.Object.FindFirstObjectByType<AnatomyInteractionRaycaster>(FindObjectsInactive.Include);
        if (raycaster == null)
        {
            Camera cam = Camera.main;
            GameObject host = (cam != null) ? cam.gameObject : infoPanel;
            if (host != null)
            {
                raycaster = Undo.AddComponent<AnatomyInteractionRaycaster>(host);
                Debug.Log($"[SetupAnatomySelection] Added AnatomyInteractionRaycaster to {host.name} for mouse and VR point-and-click.");
            }
        }

        // 6. Load AnatomyDatabase from Resources (or find in project)
        AnatomyDatabase anatomyDB = LoadOrFindAnatomyDatabase();
        if (anatomyDB == null)
        {
            Debug.LogWarning("[SetupAnatomySelection] No AnatomyDatabase found. Run 'Anatomy → Create Anatomy Database Asset' first. BodyPartSelectors will still work without a database.");
        }

        // 7. Setup selected anatomy GameObjects and their descendants
        int configuredCount = 0;
        HashSet<GameObject> processedObjects = new HashSet<GameObject>();

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Setup Anatomy Selection");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (GameObject obj in Selection.gameObjects)
        {
            if (obj != null)
            {
                SetupObjectHierarchy(
                    obj.transform,
                    infoPanel,
                    bodyPartName,
                    bodyPartSystem,
                    bodyPartType,
                    bodyPartDescription,
                    bodyPartFunction,
                    bodyPartLocation,
                    anatomyDB,
                    processedObjects,
                    ref configuredCount
                );
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        // Mark active scene dirty so changes are saved
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.isLoaded)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
        }

        string msg = $"Anatomy selection setup complete!\n" +
                     $"• Configured {configuredCount} interactable anatomy part(s).\n" +
                     (anatomyDB != null
                         ? $"• AnatomyDatabase '{anatomyDB.name}' assigned ({anatomyDB.entries.Count} entries)."
                         : "• No AnatomyDatabase found — run 'Anatomy → Build/Update Anatomy Database' to create one.");

        Debug.Log($"[SetupAnatomySelection] {msg}");
        EditorUtility.DisplayDialog("Anatomy Setup Complete", msg, "OK");
    }

    // =========================================================================
    // MENU: Build / Update Anatomy Database
    // =========================================================================
    [MenuItem("Anatomy/Database/Build-Update Database from Selection", false, 11)]
    public static void BuildOrUpdateAnatomyDatabase()
    {
        if (Selection.gameObjects == null || Selection.gameObjects.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "No Selection",
                "Please select one or more anatomy parent GameObjects in the Hierarchy (e.g. 'Skeletal systems') before running this command.",
                "OK"
            );
            return;
        }

        // Load or create the database asset
        AnatomyDatabase db = LoadOrFindAnatomyDatabase();
        if (db == null)
        {
            bool create = EditorUtility.DisplayDialog(
                "No Anatomy Database Found",
                "No AnatomyDatabase asset was found in Assets/Resources/.\n\nWould you like to create one now?",
                "Create Database", "Cancel"
            );
            if (create)
            {
                db = CreateAnatomyDatabaseAsset();
            }
            if (db == null)
            {
                Debug.LogError("[SetupAnatomySelection] Build aborted: no AnatomyDatabase asset.");
                return;
            }
        }

        int newEntries      = 0;
        int existingEntries = 0;

        Undo.RecordObject(db, "Build Anatomy Database");

        foreach (GameObject root in Selection.gameObjects)
        {
            if (root == null) continue;

            // Recurse all children with a MeshRenderer
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer mr in renderers)
            {
                if (mr == null) continue;
                GameObject go = mr.gameObject;

                // Skip environment / UI / teleportation objects
                if (!IsAnatomyObject(go)) continue;

                // Skip empty or whitespace names
                string partName = go.name;
                if (string.IsNullOrWhiteSpace(partName)) continue;

                // GetOrCreate — never overwrites existing data
                bool existed = (db.FindEntry(partName) != null);
                db.GetOrCreate(partName);

                if (existed) existingEntries++;
                else newEntries++;
            }
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string report = $"Anatomy Database Updated:\n" +
                        $"• {newEntries} new entries added.\n" +
                        $"• {existingEntries} existing entries kept (no data overwritten).\n" +
                        $"• Total entries: {db.entries.Count}\n\n" +
                        $"Now open the AnatomyDatabase asset to fill in Descriptions, Functions, and Locations.";

        Debug.Log($"[SetupAnatomySelection] {report}");
        EditorUtility.DisplayDialog("Anatomy Database Updated", report, "OK");

        // Ping the asset in the Project window
        EditorGUIUtility.PingObject(db);
        Selection.activeObject = db;
    }

    // =========================================================================
    // MENU: Create Anatomy Database Asset
    // =========================================================================
    [MenuItem("Anatomy/Database/Create Anatomy Database Asset", false, 10)]
    public static void CreateAnatomyDatabaseAssetMenu()
    {
        AnatomyDatabase existing = LoadOrFindAnatomyDatabase();
        if (existing != null)
        {
            EditorUtility.DisplayDialog(
                "Already Exists",
                $"An AnatomyDatabase asset already exists at:\n{AssetDatabase.GetAssetPath(existing)}\n\nOpening it now.",
                "OK"
            );
            EditorGUIUtility.PingObject(existing);
            Selection.activeObject = existing;
            return;
        }

        AnatomyDatabase db = CreateAnatomyDatabaseAsset();
        if (db != null)
        {
            EditorGUIUtility.PingObject(db);
            Selection.activeObject = db;
            EditorUtility.DisplayDialog(
                "Database Created",
                $"AnatomyDatabase asset created at:\nAssets/Resources/AnatomyDatabase.asset\n\nSelect your anatomy hierarchy, then run:\nAnatomy → Build/Update Anatomy Database",
                "OK"
            );
        }
    }

    // =========================================================================
    // Core setup helpers
    // =========================================================================

    private static void SetupObjectHierarchy(
        Transform current,
        GameObject infoPanel,
        TMP_Text bodyPartName,
        TMP_Text bodyPartSystem,
        TMP_Text bodyPartType,
        TMP_Text bodyPartDescription,
        TMP_Text bodyPartFunction,
        TMP_Text bodyPartLocation,
        AnatomyDatabase anatomyDB,
        HashSet<GameObject> processedObjects,
        ref int configuredCount)
    {
        if (current == null) return;

        // Process children first (depth-first)
        for (int i = 0; i < current.childCount; i++)
        {
            SetupObjectHierarchy(
                current.GetChild(i),
                infoPanel,
                bodyPartName,
                bodyPartSystem,
                bodyPartType,
                bodyPartDescription,
                bodyPartFunction,
                bodyPartLocation,
                anatomyDB,
                processedObjects,
                ref configuredCount
            );
        }

        GameObject obj = current.gameObject;

        // Only configure valid anatomy objects
        if (!IsAnatomyObject(obj)) return;

        // Only configure GameObjects that have renderable meshes
        MeshRenderer meshRenderer        = obj.GetComponent<MeshRenderer>();
        SkinnedMeshRenderer skinnedMesh  = obj.GetComponent<SkinnedMeshRenderer>();
        if (meshRenderer == null && skinnedMesh == null) return;

        // Avoid double-processing in this batch
        if (processedObjects.Contains(obj)) return;
        processedObjects.Add(obj);

        // 1. Ensure Collider for XR raycast
        Collider col = obj.GetComponent<Collider>();
        if (col == null)
        {
            MeshFilter mf = obj.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                MeshCollider mc  = Undo.AddComponent<MeshCollider>(obj);
                mc.sharedMesh    = mf.sharedMesh;
                mc.convex        = false;
            }
            else
            {
                Undo.AddComponent<BoxCollider>(obj);
            }
        }

        // 2. Configure BodyPartSelector
        BodyPartSelector selector = obj.GetComponent<BodyPartSelector>();
        if (selector == null)
        {
            selector = Undo.AddComponent<BodyPartSelector>(obj);
        }

        SerializedObject serializedSelector = new SerializedObject(selector);
        SetObjectReference(serializedSelector, "infoPanel",           infoPanel);
        SetObjectReference(serializedSelector, "bodyPartName",        bodyPartName);
        SetObjectReference(serializedSelector, "bodyPartSystem",      bodyPartSystem);
        SetObjectReference(serializedSelector, "bodyPartType",        bodyPartType);
        SetObjectReference(serializedSelector, "bodyPartDescription", bodyPartDescription);
        SetObjectReference(serializedSelector, "bodyPartFunction",    bodyPartFunction);
        SetObjectReference(serializedSelector, "bodyPartLocation",    bodyPartLocation);

        // Assign AnatomyDatabase reference if available
        if (anatomyDB != null)
        {
            SetObjectReference(serializedSelector, "anatomyDatabase", anatomyDB);
        }

        serializedSelector.ApplyModifiedProperties();

        // 3. Configure XRSimpleInteractable
        var interactable =
            obj.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();

        if (interactable == null)
        {
            interactable = Undo.AddComponent<
                UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>(obj);
        }

        if (interactable != null)
        {
            // Prevent Physics.ClosestPoint spam from non-convex mesh colliders
            interactable.distanceCalculationMode =
                UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable
                    .DistanceCalculationMode.TransformPosition;

            // Add OnSelectEntered listener — only once
            bool hasListener    = false;
            int  eventCount     = interactable.selectEntered.GetPersistentEventCount();
            for (int e = 0; e < eventCount; e++)
            {
                if (interactable.selectEntered.GetPersistentTarget(e) == selector &&
                    interactable.selectEntered.GetPersistentMethodName(e) ==
                        nameof(selector.OnSelectEntered))
                {
                    hasListener = true;
                    break;
                }
            }

            if (!hasListener)
            {
                UnityEditor.Events.UnityEventTools.AddPersistentListener(
                    interactable.selectEntered,
                    selector.OnSelectEntered
                );
            }

            // Add OnHoverEntered listener — VR ray hover highlighting
            bool hasHoverEnteredListener = false;
            int hoverEnterCount = interactable.firstHoverEntered.GetPersistentEventCount();
            for (int e = 0; e < hoverEnterCount; e++)
            {
                if (interactable.firstHoverEntered.GetPersistentTarget(e) == selector &&
                    interactable.firstHoverEntered.GetPersistentMethodName(e) == nameof(selector.OnHoverEntered))
                {
                    hasHoverEnteredListener = true;
                    break;
                }
            }
            if (!hasHoverEnteredListener)
            {
                UnityEditor.Events.UnityEventTools.AddPersistentListener(
                    interactable.firstHoverEntered,
                    selector.OnHoverEntered
                );
            }

            // Add OnHoverExited listener — VR ray unhighlight
            bool hasHoverExitedListener = false;
            int hoverExitCount = interactable.lastHoverExited.GetPersistentEventCount();
            for (int e = 0; e < hoverExitCount; e++)
            {
                if (interactable.lastHoverExited.GetPersistentTarget(e) == selector &&
                    interactable.lastHoverExited.GetPersistentMethodName(e) == nameof(selector.OnHoverExited))
                {
                    hasHoverExitedListener = true;
                    break;
                }
            }
            if (!hasHoverExitedListener)
            {
                UnityEditor.Events.UnityEventTools.AddPersistentListener(
                    interactable.lastHoverExited,
                    selector.OnHoverExited
                );
            }
        }

        EditorUtility.SetDirty(obj);
        configuredCount++;
    }

    // =========================================================================
    // Scene cleanup
    // =========================================================================

    private static void CleanNonAnatomyObjectsInternal(bool showDialog)
    {
        int missingScriptsRemoved          = 0;
        int conflictingInteractablesRemoved = 0;
        int selectorsRemoved               = 0;
        int distanceModeFixed              = 0;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in transforms)
                {
                    if (t == null) continue;
                    GameObject go = t.gameObject;

                    // 1. Remove missing script components
                    int missing = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                    if (missing > 0)
                    {
                        missingScriptsRemoved += missing;
                        EditorUtility.SetDirty(go);
                    }

                    // 2. Strip BodyPartSelector / duplicate XRSimpleInteractable from environment objects
                    bool isEnvironment = !IsAnatomyObject(go);

                    if (isEnvironment)
                    {
                        BodyPartSelector sel = go.GetComponent<BodyPartSelector>();
                        if (sel != null)
                        {
                            Undo.DestroyObjectImmediate(sel);
                            selectorsRemoved++;
                            EditorUtility.SetDirty(go);
                        }

                        bool isTeleport =
                            go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>() != null ||
                            go.GetComponent<FloorOnlyTeleportationArea>() != null;

                        if (isTeleport)
                        {
                            var simples = go.GetComponents<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
                            foreach (var s in simples)
                            {
                                Undo.DestroyObjectImmediate(s);
                                conflictingInteractablesRemoved++;
                                EditorUtility.SetDirty(go);
                            }
                        }
                    }

                    // 3. Fix distanceCalculationMode on all interactables to suppress PhysX warning
                    var interactable =
                        go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
                    if (interactable != null)
                    {
                        if (interactable.distanceCalculationMode !=
                            UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable
                                .DistanceCalculationMode.TransformPosition)
                        {
                            interactable.distanceCalculationMode =
                                UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable
                                    .DistanceCalculationMode.TransformPosition;
                            distanceModeFixed++;
                            EditorUtility.SetDirty(interactable);
                        }
                    }
                }
            }
        }

        Scene currentScene = SceneManager.GetActiveScene();
        if (currentScene.isLoaded)
        {
            EditorSceneManager.MarkSceneDirty(currentScene);
        }

        string report =
            $"Scene Fix & Cleanup Complete:\n" +
            $"• Fixed distance calculation mode on {distanceModeFixed} interactable(s) (resolves Physics.ClosestPoint warning)\n" +
            $"• Removed {missingScriptsRemoved} missing script component(s)\n" +
            $"• Removed {conflictingInteractablesRemoved} conflicting XRSimpleInteractable(s) on teleportation objects\n" +
            $"• Removed {selectorsRemoved} BodyPartSelector(s) from environment objects.";

        Debug.Log($"[SetupAnatomySelection] {report}");

        if (showDialog)
        {
            EditorUtility.DisplayDialog("Scene Cleanup", report, "OK");
        }
    }

    // =========================================================================
    // Anatomy / Environment classifier
    // =========================================================================

    /// <summary>
    /// Returns true if the GameObject is an anatomical mesh (not room, carpet, UI, camera, light, or teleportation object).
    /// </summary>
    public static bool IsAnatomyObject(GameObject obj)
    {
        if (obj == null) return false;

        // Teleportation objects are never anatomy parts
        if (obj.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>()   != null ||
            obj.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor>() != null ||
            obj.GetComponent<FloorOnlyTeleportationArea>() != null)
        {
            return false;
        }

        // UI Canvas and elements are not anatomy parts
        if (obj.GetComponent<Canvas>()                    != null ||
            obj.GetComponent<CanvasRenderer>()            != null ||
            obj.GetComponent<UnityEngine.UI.Graphic>()   != null)
        {
            return false;
        }

        // Check ancestor names for environment / room / UI / camera containers
        Transform curr = obj.transform;
        while (curr != null)
        {
            string name = curr.name.ToLowerInvariant();
            if (name.Contains("roomstructure") ||
                name.Contains("hotelroom")     ||
                name.Contains("environment")   ||
                name.Contains("classroom")     ||
                name.Contains("xr origin")     ||
                name.Contains("camera")        ||
                name.Contains("light")         ||
                name.Contains("carpet")        ||
                name.Contains("furniture")     ||
                name.Contains("infopanel")     ||
                name.Contains("menu")          ||
                name.Contains("canvas")        ||
                name.Contains("teleport"))
            {
                return false;
            }
            curr = curr.parent;
        }

        return true;
    }

    // =========================================================================
    // AnatomyDatabase asset helpers
    // =========================================================================

    /// <summary>
    /// Attempts to load the AnatomyDatabase from Resources first, then from a full AssetDatabase scan.
    /// </summary>
    private static AnatomyDatabase LoadOrFindAnatomyDatabase()
    {
        // 1. Resources path (preferred — works at runtime too)
        AnatomyDatabase db = Resources.Load<AnatomyDatabase>("AnatomyDatabase");
        if (db != null) return db;

        // 2. AssetDatabase scan (Editor only)
        string[] guids = AssetDatabase.FindAssets("t:AnatomyDatabase");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            db = AssetDatabase.LoadAssetAtPath<AnatomyDatabase>(path);
            if (db != null) return db;
        }

        return null;
    }

    /// <summary>
    /// Creates a new AnatomyDatabase ScriptableObject at Assets/Resources/AnatomyDatabase.asset.
    /// </summary>
    private static AnatomyDatabase CreateAnatomyDatabaseAsset()
    {
        // Ensure Resources folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        AnatomyDatabase db = ScriptableObject.CreateInstance<AnatomyDatabase>();
        AssetDatabase.CreateAsset(db, "Assets/Resources/AnatomyDatabase.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SetupAnatomySelection] Created AnatomyDatabase at Assets/Resources/AnatomyDatabase.asset");
        return db;
    }

    // =========================================================================
    // Info Panel helpers
    // =========================================================================

    private static GameObject FindPanelInScene(string preferredName = "BodyPartInfoPanel")
    {
        // 1. Check if BodyPartInfoUI is already in any scene (including inactive)
        BodyPartInfoUI[] allUIs = FindComponentsInScene<BodyPartInfoUI>();
        if (allUIs.Length > 0 && allUIs[0] != null)
        {
            return allUIs[0].infoPanel != null ? allUIs[0].infoPanel : allUIs[0].gameObject;
        }

        // 2. Exact name match (including inactive)
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.Equals(preferredName, StringComparison.OrdinalIgnoreCase))
                        return t.gameObject;
                }
            }
        }

        // 3. Fuzzy match
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    string lower = t.name.ToLowerInvariant();
                    if (lower.Contains("infopanel") || lower == "bodypartpanel")
                        return t.gameObject;
                }
            }
        }

        return null;
    }

    private static TMP_Text FindTMP(GameObject panel, params string[] searchNames)
    {
        // 1. Inside the panel (including inactive)
        if (panel != null)
        {
            TMP_Text[] panelTexts = panel.GetComponentsInChildren<TMP_Text>(true);
            foreach (string search in searchNames)
            {
                foreach (TMP_Text t in panelTexts)
                {
                    if (t.name.Equals(search, StringComparison.OrdinalIgnoreCase))
                        return t;
                }
            }
            foreach (string search in searchNames)
            {
                string sl = search.ToLowerInvariant();
                foreach (TMP_Text t in panelTexts)
                {
                    if (t.name.ToLowerInvariant().Contains(sl))
                        return t;
                }
            }
        }

        // 2. Scene-wide fallback (including inactive)
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
                foreach (string search in searchNames)
                {
                    foreach (TMP_Text t in texts)
                    {
                        if (t.name.Equals(search, StringComparison.OrdinalIgnoreCase))
                            return t;
                    }
                }
            }
        }

        return null;
    }

    private static T[] FindComponentsInScene<T>() where T : Component
    {
        List<T> list = new List<T>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                list.AddRange(root.GetComponentsInChildren<T>(true));
            }
        }
        return list.ToArray();
    }

    private static void SetObjectReference(SerializedObject so, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
        {
            prop.objectReferenceValue = value;
        }
    }

    // =========================================================================
    // Fallback panel creator
    // =========================================================================

    private static GameObject CreateDefaultInfoPanel()
    {
        GameObject panelRoot = new GameObject("BodyPartInfoPanel");
        Undo.RegisterCreatedObjectUndo(panelRoot, "Create BodyPartInfoPanel");

        RectTransform panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.sizeDelta      = new Vector2(500, 300);
        panelRect.localPosition  = new Vector3(0, 1.5f, 2f);
        panelRect.localScale     = Vector3.one * 0.002f;

        UnityEngine.UI.Image bgImage = panelRoot.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = new Color(0.1f, 0.12f, 0.15f, 0.9f);

        // BodyPartName
        GameObject nameObj   = CreateTMPChild(panelRoot, "BodyPartName",
            new Vector2(0, 0.7f), new Vector2(1, 1f),
            new Vector2(20, 10), new Vector2(-20, -10),
            "Body Part Name", 28, FontStyles.Bold, TextAlignmentOptions.Center, Color.white);

        // BodyPartDescription
        GameObject descObj   = CreateTMPChild(panelRoot, "BodyPartDescription",
            new Vector2(0, 0), new Vector2(1, 0.7f),
            new Vector2(20, 20), new Vector2(-20, -10),
            "Description will appear here.", 18, FontStyles.Normal,
            TextAlignmentOptions.TopLeft, new Color(0.9f, 0.9f, 0.9f, 1f));

        TextMeshProUGUI nameTMP = nameObj.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI descTMP = descObj.GetComponent<TextMeshProUGUI>();
        descTMP.textWrappingMode = TextWrappingModes.Normal;

        // Add BodyPartInfoUI manager
        BodyPartInfoUI ui         = panelRoot.AddComponent<BodyPartInfoUI>();
        ui.infoPanel              = panelRoot;
        ui.bodyPartName           = nameTMP;
        ui.bodyPartDescription    = descTMP;

        return panelRoot;
    }

    private static GameObject CreateTMPChild(
        GameObject parent,
        string childName,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        string defaultText,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject go = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(go, $"Create {childName}");
        go.transform.SetParent(parent.transform, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin  = anchorMin;
        rt.anchorMax  = anchorMax;
        rt.offsetMin  = offsetMin;
        rt.offsetMax  = offsetMax;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text       = defaultText;
        tmp.fontSize   = fontSize;
        tmp.fontStyle  = fontStyle;
        tmp.alignment  = alignment;
        tmp.color      = color;

        return go;
    }
}