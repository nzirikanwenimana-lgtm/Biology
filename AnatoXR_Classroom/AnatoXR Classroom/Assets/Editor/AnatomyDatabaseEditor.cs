using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Custom Inspector for the AnatomyDatabase ScriptableObject.
///
/// Features:
///   • Completion counter (Completed / Total)
///   • Search by name
///   • Filter by System
///   • Filter by Type
///   • "Uncompleted Only" filter (missing description, function, or location)
///   • Expand / Collapse all
///   • Per-entry delete
///   • Add blank entry
///   • CSV export / import shortcut buttons
///   • Clear All (with confirmation)
/// </summary>
[CustomEditor(typeof(AnatomyDatabase))]
public class AnatomyDatabaseEditor : Editor
{
    // -------------------------------------------------------------------------
    // Filter state
    // -------------------------------------------------------------------------
    private string  searchQuery      = "";
    private string  filterSystem     = "All";
    private string  filterType       = "All";
    private bool    uncompletedOnly  = false;
    private Vector2 scrollPosition;
    private HashSet<int> expandedIndices = new HashSet<int>();

    // Cache to avoid rebuilding dropdown lists every frame
    private string[] systemOptions;
    private string[] typeOptions;
    private int      systemIndex;
    private int      typeIndex;
    private int      lastEntryCount  = -1;

    // -------------------------------------------------------------------------
    // Inspector GUI
    // -------------------------------------------------------------------------
    public override void OnInspectorGUI()
    {
        AnatomyDatabase db = (AnatomyDatabase)target;

        int total     = (db.entries != null) ? db.entries.Count : 0;
        int completed = CountCompleted(db);

        // ── Header ──────────────────────────────────────────────────────────
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Anatomy Database Inspector", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Anatomy Database Management:\n" +
            "  • Anatomy → Database → Update Metadata From Scene  — auto-classify parts & systems\n" +
            "  • Anatomy → Database → Export Database CSV         — export database for Excel / Sheets\n" +
            "  • Anatomy → Database → Import Database CSV         — import updated medical data",
            MessageType.Info);

        EditorGUILayout.Space(4);

        // ── Completion Counter ───────────────────────────────────────────────
        float progress  = (total > 0) ? (float)completed / total : 0f;
        Rect  barRect   = GUILayoutUtility.GetRect(18f, 18f, GUILayout.ExpandWidth(true));
        EditorGUI.ProgressBar(barRect, progress, $"Completed: {completed} / {total}");
        EditorGUILayout.Space(4);

        // ── Quick-action buttons ─────────────────────────────────────────────────
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

        GUI.backgroundColor = new Color(0.2f, 0.7f, 1f);
        if (GUILayout.Button("Enrich Medical Data", EditorStyles.miniButton, GUILayout.Height(26)))
            AnatomyContentEnricher.EnrichDatabaseMenu();

        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.3f, 0.75f, 0.4f);
        if (GUILayout.Button("Update From Scene", EditorStyles.miniButtonLeft, GUILayout.Height(24)))
            AnatomyDatabaseTools.UpdateMetadataFromScene();

        GUI.backgroundColor = new Color(0.3f, 0.55f, 0.9f);
        if (GUILayout.Button("Export CSV", EditorStyles.miniButtonMid, GUILayout.Height(24)))
            AnatomyDatabaseTools.ExportDatabaseCSV();

        GUI.backgroundColor = new Color(0.9f, 0.65f, 0.2f);
        if (GUILayout.Button("Import CSV", EditorStyles.miniButtonRight, GUILayout.Height(24)))
            AnatomyDatabaseTools.ImportDatabaseCSV();

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // ── Filters ─────────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Filters", EditorStyles.boldLabel);

        // Rebuild dropdown lists when entry count changes
        if (total != lastEntryCount)
        {
            RebuildDropdownLists(db);
            lastEntryCount = total;
        }

        // Name search
        string newSearch = EditorGUILayout.TextField("Search by name:", searchQuery);
        if (newSearch != searchQuery)
        {
            searchQuery = newSearch;
            expandedIndices.Clear();
        }

        // System filter
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("System:", GUILayout.Width(60));
        int newSysIdx = EditorGUILayout.Popup(systemIndex, systemOptions ?? new[] { "All" });
        if (newSysIdx != systemIndex) { systemIndex = newSysIdx; filterSystem = systemOptions[systemIndex]; expandedIndices.Clear(); }
        EditorGUILayout.EndHorizontal();

        // Type filter
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Type:", GUILayout.Width(60));
        int newTypeIdx = EditorGUILayout.Popup(typeIndex, typeOptions ?? new[] { "All" });
        if (newTypeIdx != typeIndex) { typeIndex = newTypeIdx; filterType = typeOptions[typeIndex]; expandedIndices.Clear(); }
        EditorGUILayout.EndHorizontal();

        // Uncompleted only toggle
        bool newUncompleted = EditorGUILayout.Toggle("Uncompleted only", uncompletedOnly);
        if (newUncompleted != uncompletedOnly) { uncompletedOnly = newUncompleted; expandedIndices.Clear(); }

        EditorGUILayout.Space(4);

        // ── Expand / Collapse ────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Expand All", EditorStyles.miniButtonLeft, GUILayout.Width(90)))
        {
            expandedIndices.Clear();
            if (db.entries != null)
                for (int i = 0; i < db.entries.Count; i++)
                    expandedIndices.Add(i);
        }
        if (GUILayout.Button("Collapse All", EditorStyles.miniButtonRight, GUILayout.Width(90)))
        {
            expandedIndices.Clear();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // ── Entry List ───────────────────────────────────────────────────────
        if (total == 0)
        {
            EditorGUILayout.HelpBox(
                "No entries yet. Select an anatomy parent in the Hierarchy, then run:\n" +
                "Anatomy → Build-Update Anatomy Database",
                MessageType.Warning);
        }
        else
        {
            string queryLower = searchQuery?.ToLowerInvariant() ?? "";
            bool   filterSys  = filterSystem != "All";
            bool   filterTyp  = filterType   != "All";

            int shown = 0;

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MaxHeight(600));

            Undo.RecordObject(db, "Edit Anatomy Database");

            for (int i = 0; i < db.entries.Count; i++)
            {
                AnatomyEntry entry = db.entries[i];
                if (entry == null) continue;

                // ── Apply filters ──────────────────────────────────────────
                if (!string.IsNullOrEmpty(queryLower) &&
                    !entry.partName.ToLowerInvariant().Contains(queryLower))
                    continue;

                if (filterSys && !string.Equals(entry.systemName, filterSystem,
                        System.StringComparison.OrdinalIgnoreCase))
                    continue;

                if (filterTyp && !string.Equals(entry.partType, filterType,
                        System.StringComparison.OrdinalIgnoreCase))
                    continue;

                if (uncompletedOnly && IsComplete(entry))
                    continue;

                shown++;

                bool isComplete  = IsComplete(entry);
                bool isExpanded  = expandedIndices.Contains(i);

                // Colour-code: green = complete, yellow = partial, red = blank
                Color rowColor = isComplete
                    ? new Color(0.25f, 0.55f, 0.25f, 0.25f)
                    : (HasAnyContent(entry)
                        ? new Color(0.55f, 0.5f, 0.1f, 0.25f)
                        : new Color(0.55f, 0.2f, 0.2f, 0.15f));

                // Draw coloured background
                Rect bgRect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.Height(0));

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();

                // Status dot — ASCII only (Unity's built-in font doesn't always render Unicode glyphs)
                GUIStyle dotStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize  = 10,
                    fontStyle = isComplete ? FontStyle.Bold : FontStyle.Normal,
                    normal    = { textColor = isComplete ? new Color(0.3f, 0.85f, 0.3f) : new Color(0.75f, 0.4f, 0.4f) }
                };
                EditorGUILayout.LabelField(isComplete ? "[+]" : "[ ]", dotStyle, GUILayout.Width(28));

                // Foldout label
                bool newExpanded = EditorGUILayout.Foldout(isExpanded,
                    $"{entry.partName}  [{entry.systemName ?? ""}  |  {entry.partType ?? ""}]",
                    true, EditorStyles.foldoutHeader);
                if (newExpanded != isExpanded)
                {
                    if (newExpanded) expandedIndices.Add(i);
                    else expandedIndices.Remove(i);
                }

                // Delete button
                GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f, 1f);
                if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22)))
                {
                    if (EditorUtility.DisplayDialog("Delete Entry",
                        $"Delete entry '{entry.partName}'?", "Delete", "Cancel"))
                    {
                        db.entries.RemoveAt(i);
                        expandedIndices.Remove(i);
                        EditorUtility.SetDirty(db);
                        AssetDatabase.SaveAssets();
                        GUIUtility.ExitGUI();
                        return;
                    }
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();

                // ── Expanded detail fields ─────────────────────────────────
                if (expandedIndices.Contains(i))
                {
                    EditorGUI.indentLevel++;

                    entry.partName = EditorGUILayout.TextField(
                        new GUIContent("Part Name", "Must match the exact GameObject name in the scene (case-insensitive)."),
                        entry.partName);

                    entry.systemName = EditorGUILayout.TextField(
                        new GUIContent("System", "e.g. Skeletal System, Muscular System, Circulatory System"),
                        entry.systemName);

                    entry.partType = EditorGUILayout.TextField(
                        new GUIContent("Type", "e.g. Bone, Muscle, Organ, Nerve, Vessel"),
                        entry.partType);

                    EditorGUILayout.LabelField(new GUIContent("Description",
                        "Medical description. Must come from reliable anatomy information."));
                    entry.description = EditorGUILayout.TextArea(entry.description, GUILayout.MinHeight(54));

                    EditorGUILayout.LabelField(new GUIContent("Function",
                        "Primary physiological function."));
                    entry.function = EditorGUILayout.TextArea(entry.function, GUILayout.MinHeight(40));

                    entry.location = EditorGUILayout.TextField(
                        new GUIContent("Location", "Anatomical location in the body."),
                        entry.location);

                    // ── FMA Data ───────────────────────────────────────────
                    bool hasFma = !string.IsNullOrWhiteSpace(entry.fmaId) ||
                                  !string.IsNullOrWhiteSpace(entry.fmaPreferredName);

                    EditorGUILayout.Space(4);
                    GUIStyle fmaLabelStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        fontStyle = FontStyle.Italic,
                        normal    = { textColor = hasFma
                            ? new Color(0.35f, 0.75f, 0.95f)
                            : new Color(0.55f, 0.55f, 0.55f) }
                    };
                    EditorGUILayout.LabelField(hasFma ? "FMA Data  [matched]" : "FMA Data  [not matched]",
                        fmaLabelStyle);

                    EditorGUI.BeginDisabledGroup(false); // editable, but visually grouped
                    entry.fmaId = EditorGUILayout.TextField(
                        new GUIContent("FMA ID", "FMA ontology identifier, e.g. FMA:7088. " +
                            "Set automatically by 'Anatomy → Import FMA CSV'."),
                        entry.fmaId);

                    entry.fmaPreferredName = EditorGUILayout.TextField(
                        new GUIContent("FMA Preferred Name", "The FMA preferred English anatomical term. " +
                            "Set automatically by 'Anatomy → Import FMA CSV'."),
                        entry.fmaPreferredName);
                    EditorGUI.EndDisabledGroup();

                    EditorGUI.indentLevel--;
                    EditorGUILayout.Space(2);
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox(
                $"Showing {shown} of {total} entries.",
                MessageType.None);
        }

        EditorGUILayout.Space(8);

        // ── Manual Add ───────────────────────────────────────────────────────
        EditorGUILayout.LabelField("Manual Entry", EditorStyles.boldLabel);
        if (GUILayout.Button("+ Add Blank Entry"))
        {
            Undo.RecordObject(db, "Add Anatomy Entry");
            db.entries.Add(new AnatomyEntry { partName = "New Part" });
            expandedIndices.Add(db.entries.Count - 1);
            lastEntryCount = -1; // force dropdown rebuild
            EditorUtility.SetDirty(db);
        }

        EditorGUILayout.Space(4);

        // ── Danger Zone ───────────────────────────────────────────────────────
        GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f, 1f);
        if (GUILayout.Button("Clear ALL Entries"))
        {
            if (EditorUtility.DisplayDialog("Clear All Entries",
                $"Permanently delete ALL {total} entries? This cannot be undone.", "Yes, Clear All", "Cancel"))
            {
                Undo.RecordObject(db, "Clear Anatomy Database");
                db.entries.Clear();
                expandedIndices.Clear();
                lastEntryCount = -1;
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssets();
            }
        }
        GUI.backgroundColor = Color.white;

        if (GUI.changed)
        {
            EditorUtility.SetDirty(db);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static bool IsComplete(AnatomyEntry e) =>
        !string.IsNullOrWhiteSpace(e.description) &&
        !string.IsNullOrWhiteSpace(e.function) &&
        !string.IsNullOrWhiteSpace(e.location);

    private static bool HasAnyContent(AnatomyEntry e) =>
        !string.IsNullOrWhiteSpace(e.description) ||
        !string.IsNullOrWhiteSpace(e.function) ||
        !string.IsNullOrWhiteSpace(e.location);

    private static int CountCompleted(AnatomyDatabase db)
    {
        if (db.entries == null) return 0;
        int count = 0;
        foreach (AnatomyEntry e in db.entries)
            if (e != null && IsComplete(e)) count++;
        return count;
    }

    private void RebuildDropdownLists(AnatomyDatabase db)
    {
        var systems = new SortedSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var types   = new SortedSet<string>(System.StringComparer.OrdinalIgnoreCase);

        if (db.entries != null)
        {
            foreach (AnatomyEntry e in db.entries)
            {
                if (e == null) continue;
                if (!string.IsNullOrWhiteSpace(e.systemName)) systems.Add(e.systemName.Trim());
                if (!string.IsNullOrWhiteSpace(e.partType))   types.Add(e.partType.Trim());
            }
        }

        var sysList = new List<string> { "All" };
        sysList.AddRange(systems);
        systemOptions = sysList.ToArray();

        var typeList = new List<string> { "All" };
        typeList.AddRange(types);
        typeOptions = typeList.ToArray();

        // Restore selected indices after rebuild
        systemIndex = Mathf.Max(0, System.Array.IndexOf(systemOptions, filterSystem));
        typeIndex   = Mathf.Max(0, System.Array.IndexOf(typeOptions,   filterType));

        filterSystem = systemOptions[systemIndex];
        filterType   = typeOptions[typeIndex];
    }
}
