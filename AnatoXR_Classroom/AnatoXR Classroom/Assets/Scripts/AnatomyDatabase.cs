using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central database of anatomy information keyed by body part name.
/// Create one instance at Assets/Resources/AnatomyDatabase.asset using:
///   Anatomy → Create Anatomy Database Asset
/// Populate entries automatically using:
///   Anatomy → Build/Update Anatomy Database
/// Then fill in descriptions, functions, and locations in the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "AnatomyDatabase", menuName = "AnatoXR/Anatomy Database", order = 1)]
public class AnatomyDatabase : ScriptableObject
{
    [Tooltip("All registered anatomy entries. Use 'Anatomy → Build/Update Anatomy Database' to auto-populate.")]
    public List<AnatomyEntry> entries = new List<AnatomyEntry>();

    // -----------------------------------------------------------------------
    // Lookup API (used at runtime by BodyPartSelector)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Finds a database entry matching the given part name.
    /// Search order:
    ///   1. Exact match (case-insensitive)
    ///   2. Underscore/dot/dash normalised match
    ///   3. Contains match (partial name)
    /// Returns null if nothing matches.
    /// </summary>
    public AnatomyEntry FindEntry(string partName)
    {
        if (string.IsNullOrWhiteSpace(partName) || entries == null)
            return null;

        string normalised = NormaliseName(partName);

        // Pass 1: Exact case-insensitive match
        foreach (AnatomyEntry e in entries)
        {
            if (string.IsNullOrEmpty(e.partName)) continue;
            if (string.Equals(e.partName, partName, System.StringComparison.OrdinalIgnoreCase))
                return e;
        }

        // Pass 2: Normalised match (underscores / dots / dashes → spaces, trimmed)
        foreach (AnatomyEntry e in entries)
        {
            if (string.IsNullOrEmpty(e.partName)) continue;
            if (string.Equals(NormaliseName(e.partName), normalised, System.StringComparison.OrdinalIgnoreCase))
                return e;
        }

        // Pass 3: Contains match (partial / abbreviated names)
        foreach (AnatomyEntry e in entries)
        {
            if (string.IsNullOrEmpty(e.partName)) continue;
            string eNorm = NormaliseName(e.partName);
            if (eNorm.IndexOf(normalised, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalised.IndexOf(eNorm, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return e;
        }

        return null;
    }

    // -----------------------------------------------------------------------
    // Editor API (used by Build/Update tool — Editor only)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the existing entry for partName, or creates and appends a new
    /// blank entry if none exists. Never overwrites existing data.
    /// Should only be called from Editor code (uses EditorUtility.SetDirty indirectly).
    /// </summary>
    public AnatomyEntry GetOrCreate(string partName)
    {
        if (string.IsNullOrWhiteSpace(partName))
            return null;

        AnatomyEntry existing = FindEntry(partName);
        if (existing != null)
            return existing;

        AnatomyEntry newEntry = new AnatomyEntry
        {
            partName = partName
        };
        entries.Add(newEntry);
        return newEntry;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string NormaliseName(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        return raw.Replace('_', ' ').Replace('.', ' ').Replace('-', ' ').Trim();
    }
}
