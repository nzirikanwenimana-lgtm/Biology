using UnityEngine;

/// <summary>
/// Represents one anatomy body part entry in the AnatomyDatabase.
/// All fields are left empty by default — fill them in the AnatomyDatabase Inspector.
/// The partName must exactly match the GameObject name in the scene (case-insensitive).
/// </summary>
[System.Serializable]
public class AnatomyEntry
{
    [Tooltip("Must match the exact GameObject name in the scene (case-insensitive). Set automatically by the Build tool.")]
    public string partName = "";

    [Tooltip("The anatomical system this part belongs to. E.g. Skeletal System, Muscular System, Circulatory System.")]
    public string systemName = "";

    [Tooltip("Classification or category. E.g. Bone, Muscle, Organ, Nerve, Vessel.")]
    public string partType = "";

    [Tooltip("Anatomical description of this structure.")]
    [TextArea(3, 6)]
    public string description = "";

    [Tooltip("The primary physiological function of this structure.")]
    [TextArea(2, 5)]
    public string function = "";

    [Tooltip("Where in the body this structure is located.")]
    public string location = "";

    // ── FMA (Foundational Model of Anatomy) ──────────────────────────────────
    // Populated automatically by 'Anatomy → Import FMA CSV' or
    // 'Anatomy → Match Database With FMA'.  Never set by hand unless you have
    // a reliable FMA source.

    [Tooltip("FMA ontology identifier for this structure (e.g. 'FMA:7088'). " +
             "Populated automatically by 'Anatomy → Import FMA CSV'.")]
    public string fmaId = "";

    [Tooltip("The FMA preferred English anatomical term for this structure. " +
             "Populated automatically by 'Anatomy → Import FMA CSV'.")]
    public string fmaPreferredName = "";
}
