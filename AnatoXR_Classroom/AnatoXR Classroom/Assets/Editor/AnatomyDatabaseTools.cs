using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// Editor tools for maintaining the AnatomyDatabase.
///
/// MENU COMMANDS:
///   Anatomy → Update Metadata From Scene  — PRIMARY: scans all MeshRenderers in the scene, matches to db entries
///   Anatomy → Update Database Metadata   — alias for Update Metadata From Scene
///   Anatomy → Export Database CSV        — exports all entries to a CSV you can edit in Excel / Google Sheets
///   Anatomy → Import Database CSV        — merges a CSV back in, matching by PartName, never duplicating
/// </summary>
public static class AnatomyDatabaseTools
{
    // =========================================================================
    // System keyword map  (order matters — evaluated top to bottom)
    // =========================================================================
    private static readonly (string keyword, string systemName)[] SystemKeywords =
    {
        // Must come before "muscle" keywords so circulatory arteries aren't misclassified
        ("circulat",   "Circulatory System"),
        ("cardio",     "Circulatory System"),
        ("arteri",     "Circulatory System"),   // artery / arteries / arterial
        ("vascular",   "Circulatory System"),
        ("aorta",      "Circulatory System"),

        ("lymph",      "Lymphoid Organs"),

        ("nerv",       "Nervous System"),
        ("brain",      "Nervous System"),
        ("neural",     "Nervous System"),
        ("spinal cord","Nervous System"),

        ("viscer",     "Visceral System"),
        ("organ",      "Visceral System"),      // catch-all for organs

        ("joint",      "Joints"),
        ("articul",    "Joints"),
        ("ligament",   "Joints"),

        ("insert",     "Muscular Insertions"),  // "muscular insertions" parent

        ("muscul",     "Muscular System"),
        ("muscle",     "Muscular System"),

        ("skelet",     "Skeletal System"),
        ("bone",       "Skeletal System"),
    };

    // =========================================================================
    // Part-type keyword map  (name-based, evaluated in order — most specific first)
    // =========================================================================
    private static readonly (string keyword, string partType)[] TypeKeywords =
    {
        ("artery",       "Artery"),
        ("arteria",      "Artery"),
        ("vein",         "Vein"),
        ("vena",         "Vein"),
        ("ligament",     "Ligament"),
        ("ligamentum",   "Ligament"),
        ("cartilage",    "Cartilage"),
        ("meniscus",     "Cartilage"),
        ("joint",        "Joint"),
        ("articulatio",  "Joint"),
        ("nerve",        "Nerve"),
        ("nervus",       "Nerve"),
        ("muscle",       "Muscle"),
        ("musculus",     "Muscle"),
        ("tendon",       "Tendon"),
        ("bursa",        "Bursa"),
        ("bone",         "Bone"),
        ("os ",          "Bone"),   // Latin "os" with trailing space to avoid false matches
    };

    // =========================================================================
    // MENU: Update Metadata From Scene
    // =========================================================================
    [MenuItem("Anatomy/Database/Update Metadata From Scene", false, 12)]
    public static void UpdateMetadataFromScene()
    {
        RunMetadataUpdateFromScene();
    }

    public static void UpdateDatabaseMetadata()
    {
        RunMetadataUpdateFromScene();
    }

    // =========================================================================
    // Core scene-based metadata update
    // =========================================================================
    private static void RunMetadataUpdateFromScene()
    {
        AnatomyDatabase db = LoadOrFindDatabase();
        if (db == null)
        {
            EditorUtility.DisplayDialog("No Database Found",
                "Could not find an AnatomyDatabase asset.\nRun 'Anatomy → Create Anatomy Database Asset' first.", "OK");
            return;
        }

        if (db.entries == null || db.entries.Count == 0)
        {
            EditorUtility.DisplayDialog("Database is Empty",
                "The AnatomyDatabase has no entries. Run 'Anatomy → Build-Update Anatomy Database' first.", "OK");
            return;
        }

        // -------------------------------------------------------------------
        // STEP 1 — Find every anatomy mesh in the active scene.
        //
        // WHY THIS APPROACH:
        //   • EditorSceneManager.GetActiveScene().GetRootGameObjects() is the
        //     correct Edit-Mode API — it always returns the loaded scene roots.
        //   • We search all roots for "Human male anatomy" (or any root that
        //     has anatomy-system children) and call GetComponentsInChildren
        //     with includeInactive=true on it.
        //   • Renderer base class catches BOTH MeshRenderer and
        //     SkinnedMeshRenderer so the tool works regardless of mesh type.
        // -------------------------------------------------------------------
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (!activeScene.isLoaded)
        {
            EditorUtility.DisplayDialog("No Active Scene",
                "No scene is currently loaded in the Editor.", "OK");
            return;
        }

        GameObject[] sceneRoots = activeScene.GetRootGameObjects();
        Debug.Log($"[AnatomyDatabaseTools] Active scene: '{activeScene.name}'.  Root objects: {sceneRoots.Length}");

        // Locate the anatomy root GameObject (exact match first, then contains)
        Transform anatomyRoot = null;
        foreach (GameObject root in sceneRoots)
        {
            string rn = root.name.ToLowerInvariant();
            if (rn == "human male anatomy" || rn.Contains("human male") || rn.Contains("anatomy"))
            {
                anatomyRoot = root.transform;
                Debug.Log($"[AnatomyDatabaseTools] Found anatomy root: '{root.name}'");
                break;
            }
        }

        // Fallback: search one level into each root for anatomy-system children
        if (anatomyRoot == null)
        {
            foreach (GameObject root in sceneRoots)
            {
                foreach (Transform child in root.transform)
                {
                    string cn = child.name.ToLowerInvariant();
                    if (cn == "human male anatomy" || cn.Contains("human male") || cn.Contains("anatomy"))
                    {
                        anatomyRoot = child;
                        Debug.Log($"[AnatomyDatabaseTools] Found anatomy root (nested): '{child.name}'");
                        break;
                    }
                }
                if (anatomyRoot != null) break;
            }
        }

        // Final fallback: collect Renderers from ALL scene roots
        Renderer[] allRenderers;
        if (anatomyRoot != null)
        {
            allRenderers = anatomyRoot.GetComponentsInChildren<Renderer>(true);
            Debug.Log($"[AnatomyDatabaseTools] Renderers under '{anatomyRoot.name}': {allRenderers.Length}");
        }
        else
        {
            // Anatomy root not found by name — scan entire scene
            Debug.LogWarning("[AnatomyDatabaseTools] Could not find 'Human male anatomy' root. Scanning ALL scene renderers.");
            var all = new List<Renderer>();
            foreach (GameObject root in sceneRoots)
                all.AddRange(root.GetComponentsInChildren<Renderer>(true));
            allRenderers = all.ToArray();
            Debug.Log($"[AnatomyDatabaseTools] Total scene renderers (fallback scan): {allRenderers.Length}");
        }

        if (allRenderers.Length == 0)
        {
            EditorUtility.DisplayDialog("No Renderers Found",
                "No Renderer components were found in the active scene.\n\n" +
                "Make sure the anatomy GameObjects are in the currently active scene and contain MeshRenderer or SkinnedMeshRenderer components.",
                "OK");
            return;
        }

        // -------------------------------------------------------------------
        // STEP 2 — Build name → system map from each renderer's parent chain.
        // -------------------------------------------------------------------
        // Case-insensitive: sceneName → systemName
        var nameToSystem = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int noSystemCount = 0;

        foreach (Renderer r in allRenderers)
        {
            if (r == null) continue;
            string goName = r.gameObject.name;
            if (string.IsNullOrWhiteSpace(goName)) continue;

            string system = DetectSystemFromHierarchy(r.transform);
            if (!string.IsNullOrEmpty(system))
            {
                if (!nameToSystem.ContainsKey(goName))
                    nameToSystem[goName] = system;
            }
            else
            {
                noSystemCount++;
            }
        }

        Debug.Log($"[AnatomyDatabaseTools] Names with system detected: {nameToSystem.Count}  |  " +
                  $"Names with no system parent: {noSystemCount}");

        // Log a sample of detected mappings to help diagnose any remaining issues
        int sampleCount = 0;
        foreach (var kvp in nameToSystem)
        {
            if (sampleCount++ >= 10) break;
            Debug.Log($"[AnatomyDatabaseTools] Sample mapping: '{kvp.Key}' → '{kvp.Value}'");
        }

        if (nameToSystem.Count == 0)
        {
            EditorUtility.DisplayDialog("No Systems Detected",
                "Renderers were found but none had an anatomy-system parent (Skeletal systems, Muscular system, etc.).\n\n" +
                "Check the Console for the sample mappings above and verify your hierarchy parent names match the expected names.",
                "OK");
            return;
        }

        // -------------------------------------------------------------------
        // STEP 3 — Match scene names to DB entries using the DB's own fuzzy
        //           FindEntry() — which handles case, underscores, and partial
        //           name matches. This is more robust than an exact dict lookup.
        // -------------------------------------------------------------------
        int systemsUpdated  = 0;
        int typesUpdated    = 0;
        int entriesMatched  = 0;
        int notInDatabase   = 0;

        // Track which db entries were touched so we can count unmatched ones
        HashSet<AnatomyEntry> touchedEntries = new HashSet<AnatomyEntry>();

        Undo.RecordObject(db, "Update Database Metadata From Scene");

        foreach (var kvp in nameToSystem)
        {
            string sceneName = kvp.Key;
            string system    = kvp.Value;

            // Use the DB's fuzzy FindEntry (handles normalisation, partial matches)
            AnatomyEntry entry = db.FindEntry(sceneName);
            if (entry == null)
            {
                notInDatabase++;
                continue;
            }

            touchedEntries.Add(entry);
            bool changed = false;

            // Fill systemName only if blank
            if (string.IsNullOrWhiteSpace(entry.systemName))
            {
                entry.systemName = system;
                systemsUpdated++;
                changed = true;
            }

            // Fill partType only if blank and reliably inferred from the GO name
            if (string.IsNullOrWhiteSpace(entry.partType))
            {
                string inferred = InferTypeFromName(sceneName);
                if (!string.IsNullOrWhiteSpace(inferred))
                {
                    entry.partType = inferred;
                    typesUpdated++;
                    changed = true;
                }
            }

            if (changed) entriesMatched++;
        }

        // Count DB entries that had no scene match at all
        int notInScene = 0;
        foreach (AnatomyEntry e in db.entries)
        {
            if (e != null && !touchedEntries.Contains(e))
                notInScene++;
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        string report =
            $"Metadata Updated From Scene:\n" +
            $"• Scene renderers found: {allRenderers.Length}\n" +
            $"• Names mapped to anatomy systems: {nameToSystem.Count}\n" +
            $"• Matched database entries: {touchedEntries.Count}\n" +
            $"• System names filled: {systemsUpdated}\n" +
            $"• Part types filled: {typesUpdated}\n" +
            $"• Scene objects not in database: {notInDatabase}\n" +
            $"• Database entries not found in scene: {notInScene}\n" +
            $"• Total database entries: {db.entries.Count}";

        Debug.Log($"[AnatomyDatabaseTools] {report}");
        EditorUtility.DisplayDialog("Metadata Updated", report, "OK");
        EditorGUIUtility.PingObject(db);
    }

    // =========================================================================
    // MENU: Export Database CSV
    // =========================================================================
    [MenuItem("Anatomy/Database/Export Database CSV", false, 13)]
    public static void ExportDatabaseCSV()
    {
        AnatomyDatabase db = LoadOrFindDatabase();
        if (db == null)
        {
            EditorUtility.DisplayDialog("No Database Found",
                "Could not find an AnatomyDatabase asset.", "OK");
            return;
        }

        string defaultName = "AnatomyDatabase_Export.csv";
        string path = EditorUtility.SaveFilePanel(
            "Export Anatomy Database as CSV",
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            defaultName,
            "csv");

        if (string.IsNullOrEmpty(path)) return;

        StringBuilder sb = new StringBuilder();

        // Header row
        sb.AppendLine("PartName,SystemName,PartType,Description,Function,Location");

        foreach (AnatomyEntry entry in db.entries)
        {
            if (entry == null) continue;
            sb.AppendLine(
                $"{EscapeCSV(entry.partName)}," +
                $"{EscapeCSV(entry.systemName)}," +
                $"{EscapeCSV(entry.partType)}," +
                $"{EscapeCSV(entry.description)}," +
                $"{EscapeCSV(entry.function)}," +
                $"{EscapeCSV(entry.location)}"
            );
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

        string report = $"Exported {db.entries.Count} entries to:\n{path}";
        Debug.Log($"[AnatomyDatabaseTools] {report}");
        EditorUtility.DisplayDialog("Export Complete", report, "OK");
    }

    // =========================================================================
    // MENU: Import Database CSV
    // =========================================================================
    [MenuItem("Anatomy/Database/Import Database CSV", false, 14)]
    public static void ImportDatabaseCSV()
    {
        AnatomyDatabase db = LoadOrFindDatabase();
        if (db == null)
        {
            EditorUtility.DisplayDialog("No Database Found",
                "Could not find an AnatomyDatabase asset. Run 'Anatomy → Create Anatomy Database Asset' first.", "OK");
            return;
        }

        string path = EditorUtility.OpenFilePanel("Import Anatomy CSV", "", "csv");
        if (string.IsNullOrEmpty(path)) return;

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Import Error", $"Failed to read file:\n{ex.Message}", "OK");
            return;
        }

        if (lines.Length < 2)
        {
            EditorUtility.DisplayDialog("Import Error",
                "The CSV file must have a header row and at least one data row.", "OK");
            return;
        }

        // Validate header
        string header = lines[0].Trim();
        if (!header.StartsWith("PartName", StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("Import Error",
                "The CSV must start with the header row:\nPartName,SystemName,PartType,Description,Function,Location\n\nPlease use the exported CSV as a template.", "OK");
            return;
        }

        Undo.RecordObject(db, "Import Anatomy CSV");

        int matched  = 0;
        int skipped  = 0;
        int notFound = 0;
        List<string> notFoundNames = new List<string>();

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] cols = ParseCSVLine(line);
            if (cols.Length < 1) continue;

            string partName = cols[0].Trim();
            if (string.IsNullOrWhiteSpace(partName)) continue;

            AnatomyEntry entry = db.FindEntry(partName);
            if (entry == null)
            {
                notFound++;
                if (notFoundNames.Count < 20) notFoundNames.Add(partName);
                continue;
            }

            bool changed = false;

            // systemName (col 1)
            if (cols.Length > 1)
            {
                string val = cols[1].Trim();
                if (!string.IsNullOrWhiteSpace(val) && string.IsNullOrWhiteSpace(entry.systemName))
                {
                    entry.systemName = val;
                    changed = true;
                }
                else if (!string.IsNullOrWhiteSpace(val) && !string.IsNullOrWhiteSpace(entry.systemName))
                {
                    // Overwrite only if different — CSV always wins for system / type
                    if (!string.Equals(entry.systemName, val, StringComparison.OrdinalIgnoreCase))
                    {
                        entry.systemName = val;
                        changed = true;
                    }
                }
            }

            // partType (col 2)
            if (cols.Length > 2)
            {
                string val = cols[2].Trim();
                if (!string.IsNullOrWhiteSpace(val) && string.IsNullOrWhiteSpace(entry.partType))
                {
                    entry.partType = val;
                    changed = true;
                }
                else if (!string.IsNullOrWhiteSpace(val) && !string.IsNullOrWhiteSpace(entry.partType))
                {
                    if (!string.Equals(entry.partType, val, StringComparison.OrdinalIgnoreCase))
                    {
                        entry.partType = val;
                        changed = true;
                    }
                }
            }

            // description (col 3) — never overwrite existing
            if (cols.Length > 3)
            {
                string val = cols[3].Trim();
                if (!string.IsNullOrWhiteSpace(val) && string.IsNullOrWhiteSpace(entry.description))
                {
                    entry.description = val;
                    changed = true;
                }
            }

            // function (col 4) — never overwrite existing
            if (cols.Length > 4)
            {
                string val = cols[4].Trim();
                if (!string.IsNullOrWhiteSpace(val) && string.IsNullOrWhiteSpace(entry.function))
                {
                    entry.function = val;
                    changed = true;
                }
            }

            // location (col 5) — never overwrite existing
            if (cols.Length > 5)
            {
                string val = cols[5].Trim();
                if (!string.IsNullOrWhiteSpace(val) && string.IsNullOrWhiteSpace(entry.location))
                {
                    entry.location = val;
                    changed = true;
                }
            }

            if (changed) matched++;
            else skipped++;
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        // Build report
        StringBuilder report = new StringBuilder();
        report.AppendLine($"Import Complete:");
        report.AppendLine($"• {matched} entries updated.");
        report.AppendLine($"• {skipped} entries already had data (skipped).");
        report.AppendLine($"• {notFound} names in CSV not found in database.");
        if (notFoundNames.Count > 0)
        {
            report.AppendLine($"\nFirst unmatched names:");
            foreach (string n in notFoundNames)
                report.AppendLine($"  - {n}");
            if (notFound > notFoundNames.Count)
                report.AppendLine($"  ... and {notFound - notFoundNames.Count} more.");
        }

        string reportStr = report.ToString();
        Debug.Log($"[AnatomyDatabaseTools] {reportStr}");
        EditorUtility.DisplayDialog("Import Summary", reportStr, "OK");
    }

    // =========================================================================
    // Internal: infer system from name keywords
    // =========================================================================
    public static string InferSystemFromName(string partName)
    {
        if (string.IsNullOrWhiteSpace(partName)) return null;
        string lower = partName.ToLowerInvariant();

        foreach (var (kw, sys) in SystemKeywords)
        {
            if (lower.Contains(kw)) return sys;
        }
        return null;
    }

    // =========================================================================
    // Internal: infer part type from name keywords
    // =========================================================================
    public static string InferTypeFromName(string partName)
    {
        if (string.IsNullOrWhiteSpace(partName)) return null;
        string lower = partName.ToLowerInvariant();

        foreach (var (kw, type) in TypeKeywords)
        {
            if (lower.Contains(kw)) return type;
        }
        return null;
    }

    /// <summary>
    /// Walks up the parent chain of a Transform and returns the canonical anatomy
    /// system name based on parent GameObject names.
    ///
    /// Match order:
    ///   1. Exact / near-exact match against the user's known system parent names
    ///   2. Keyword fallback for renamed or custom hierarchy nodes
    /// Never overwrites — the first matching ancestor wins.
    /// </summary>
    private static string DetectSystemFromHierarchy(Transform t)
    {
        Transform curr = t.parent;
        while (curr != null)
        {
            string name  = curr.name.Trim();
            string lower = name.ToLowerInvariant();

            // ----------------------------------------------------------------
            // Pass 1: Exact / near-exact match for the user's actual hierarchy.
            // These must come BEFORE generic keyword checks because, for example,
            // "Muscular insertions" contains "muscul" — exact match catches it first.
            // ----------------------------------------------------------------

            // Nervous system & sense organs  (check before generic "nerv")
            if (lower.Contains("nervous") && lower.Contains("sense"))
                return "Nervous System & Sense Organs";

            // Muscular insertions  (check before generic "muscul")
            if (lower.Contains("insert") &&
                (lower.Contains("muscul") || lower.Contains("muscle") || lower == "muscular insertions"))
                return "Muscular Insertions";

            // "Muscular insertions" plain (in case it doesn't contain "muscul" literally)
            if (lower == "muscular insertions") return "Muscular Insertions";

            // Skeletal
            if (lower == "skeletal systems"   || lower == "skeletal system")  return "Skeletal System";
            // Muscular
            if (lower == "muscular system")                                    return "Muscular System";
            // Circulatory
            if (lower == "circulatory system")                                 return "Circulatory System";
            // Visceral
            if (lower == "visceral systems"   || lower == "visceral system")  return "Visceral System";
            // Nervous (without sense organs)
            if (lower == "nervous system")                                     return "Nervous System";
            // Joints
            if (lower == "joints")                                             return "Joints";
            // Male body
            if (lower == "male body" || lower == "male_body")                  return "Male Body";

            // ----------------------------------------------------------------
            // Pass 2: Keyword fallback (handles custom / renamed hierarchy nodes)
            // ----------------------------------------------------------------

            // Insertions must come before muscul check
            if (lower.Contains("insert"))                                        return "Muscular Insertions";
            if (lower.Contains("muscul") || lower.Contains("muscle"))           return "Muscular System";
            if (lower.Contains("skelet") || lower.Contains("bone"))             return "Skeletal System";
            if (lower.Contains("circulat") || lower.Contains("cardio")
                || lower.Contains("arteri") || lower.Contains("vascular"))      return "Circulatory System";
            if (lower.Contains("lymph"))                                        return "Lymphoid Organs";
            if (lower.Contains("nerv") || lower.Contains("brain"))              return "Nervous System";
            if (lower.Contains("viscer") || lower.Contains("organ"))            return "Visceral System";
            if (lower.Contains("joint") || lower.Contains("articul"))           return "Joints";

            curr = curr.parent;
        }
        return null;
    }

    // =========================================================================
    // Shared: load or find the AnatomyDatabase asset
    // =========================================================================
    public static AnatomyDatabase LoadOrFindDatabase()
    {
        AnatomyDatabase db = Resources.Load<AnatomyDatabase>("AnatomyDatabase");
        if (db != null) return db;

        string[] guids = AssetDatabase.FindAssets("t:AnatomyDatabase");
        foreach (string guid in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            db = AssetDatabase.LoadAssetAtPath<AnatomyDatabase>(p);
            if (db != null) return db;
        }
        return null;
    }

    // =========================================================================
    // CSV helpers
    // =========================================================================

    /// <summary>
    /// Wraps a field in double-quotes and escapes inner quotes per RFC 4180.
    /// </summary>
    private static string EscapeCSV(string value)
    {
        if (value == null) value = string.Empty;
        // Always quote: handles commas, newlines, and quotes inside values
        value = value.Replace("\"", "\"\"");
        return $"\"{value}\"";
    }

    /// <summary>
    /// Parses one CSV line respecting quoted fields (handles embedded commas and newlines in quotes).
    /// </summary>
    private static string[] ParseCSVLine(string line)
    {
        var result  = new List<string>();
        bool inQuote = false;
        var current = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuote)
            {
                if (c == '"')
                {
                    // Peek ahead for escaped quote
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // skip second quote
                    }
                    else
                    {
                        inQuote = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuote = true;
                }
                else if (c == ',')
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        result.Add(current.ToString());
        return result.ToArray();
    }
}
