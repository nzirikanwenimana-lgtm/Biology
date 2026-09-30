using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Attached to your anatomy root GameObject (e.g., Anato_XR) to organize 
/// imported anatomy FBX objects into system parent groups directly from the Inspector.
/// </summary>
public class AnatomyBodyPartsGroup : MonoBehaviour
{
    [Header("Target (Leave empty to organize THIS GameObject)")]
    public GameObject targetAnatomyRoot;

    // Group Names matching exact requirements
    public const string GROUP_SKELETAL = "Skeletal_System";
    public const string GROUP_MUSCULAR = "Muscular_System";
    public const string GROUP_JOINTS = "Joints";
    public const string GROUP_CARDIOVASCULAR = "Cardiovascular_System";
    public const string GROUP_LYMPHOID = "Lymphoid_Organs";
    public const string GROUP_NERVOUS = "Nervous_System";
    public const string GROUP_VISCERAL = "Visceral_Systems";
    public const string GROUP_REGIONS = "Regions_of_Human_Body";
    public const string GROUP_UNCLASSIFIED = "Unclassified";

    public enum SystemCategory
    {
        Cardiovascular_System = 0,
        Lymphoid_Organs = 1,
        Nervous_System = 2,
        Visceral_Systems = 3,
        Joints = 4,
        Muscular_System = 5,
        Skeletal_System = 6,
        Regions_of_Human_Body = 7,
        Unclassified = 8
    }

    [System.Serializable]
    public class SystemRule
    {
        public SystemCategory category;
        public string groupName;
        public string displayName;
        public List<string> keywords = new List<string>();

        public SystemRule(SystemCategory cat, string group, string display, string[] defaultKeywords)
        {
            category = cat;
            groupName = group;
            displayName = display;
            keywords = new List<string>(defaultKeywords);
        }
    }

    [HideInInspector]
    public List<SystemRule> rules = new List<SystemRule>();

    public void InitializeDefaultRules()
    {
        rules = new List<SystemRule>
        {
            // Priority 1: Cardiovascular
            new SystemRule(SystemCategory.Cardiovascular_System, GROUP_CARDIOVASCULAR, "Cardiovascular System", new[]
            {
                "heart", "artery", "arteriole", "vein", "venule", "capillary", "aorta", "blood vessel",
                "arteria", "arteriae", "vena", "venae", "cardio", "vascular", "atrium", "ventricle",
                "myocardium", "endocardium", "pericardium", "carotid", "jugular", "aortic", "vascul",
                "valvula", "sinus venosus", "coronary", "cava", "subclavian"
            }),

            // Priority 2: Lymphoid
            new SystemRule(SystemCategory.Lymphoid_Organs, GROUP_LYMPHOID, "Lymphoid Organs", new[]
            {
                "lymph", "lymphatic", "thymus", "spleen", "lymph node", "tonsil", "lien", "lienalis",
                "lymphaticus", "lymphoideus", "nodus lymphaticus", "nodi lymphatici", "cisterna chyli",
                "thoracic duct", "tonsilla"
            }),

            // Priority 3: Nervous
            new SystemRule(SystemCategory.Nervous_System, GROUP_NERVOUS, "Nervous System", new[]
            {
                "brain", "nerve", "neuron", "spinal cord", "ganglion", "cerebellum", "medulla",
                "nervus", "nervi", "cerebrum", "encephalon", "cortex", "thalamus", "hypothalamus",
                "pons", "meninges", "dura mater", "arachnoid", "pia mater", "plexus", "chiasm",
                "neuro", "neural", "tractus", "olfactory", "optic", "trigeminal", "vagus", "sciatic"
            }),

            // Priority 4: Visceral
            new SystemRule(SystemCategory.Visceral_Systems, GROUP_VISCERAL, "Visceral Systems", new[]
            {
                "lung", "liver", "stomach", "intestine", "pancreas", "kidney", "bladder", "esophagus",
                "trachea", "bronchus", "organ", "hepar", "hepatic", "gaster", "gastric", "pulmo",
                "pulmonary", "ren", "renal", "vesica", "vesical", "colon", "duodenum", "jejunum",
                "ileum", "cecum", "appendix", "rectum", "gallbladder", "biliary", "thyroid",
                "parathyroid", "adrenal", "suprarenal", "ureter", "urethra", "larynx", "pharynx",
                "ovary", "uterus", "testis", "prostate", "viscera", "visceral", "digestive"
            }),

            // Priority 5: Joints
            new SystemRule(SystemCategory.Joints, GROUP_JOINTS, "Joints & Articulations", new[]
            {
                "joint", "ligament", "meniscus", "tendon", "articular", "articulatio", "articulationes",
                "ligamentum", "ligamenta", "synovia", "synovial", "labrum", "bursa", "capsule",
                "capsular", "disc", "disk", "aponeurosis", "suture", "articular cartilage"
            }),

            // Priority 6: Muscular
            new SystemRule(SystemCategory.Muscular_System, GROUP_MUSCULAR, "Muscular System", new[]
            {
                "muscle", "muscular", "musculus", "musculi", "biceps", "triceps", "pectoralis",
                "deltoid", "quadriceps", "hamstring", "gluteus", "gastrocnemius", "myo", "myology",
                "flexor", "extensor", "abductor", "adductor", "levator", "depressor", "tensor",
                "rectus", "oblique", "transversus", "intercostal", "trapezius", "latissimus",
                "serratus", "scalene", "masseter", "temporalis", "psoas", "soleus", "pronator",
                "supinator", "sartorius", "gracilis", "brachialis", "coracobrachialis", "iliacus",
                "quadratus", "rhomboid", "splenius", "pectineus", "piriformis", "obturator", "gemellus"
            }),

            // Priority 7: Skeletal
            new SystemRule(SystemCategory.Skeletal_System, GROUP_SKELETAL, "Skeletal System", new[]
            {
                "bone", "skull", "femur", "tibia", "fibula", "humerus", "radius", "ulna", "pelvis",
                "vertebra", "rib", "sternum", "clavicle", "scapula", "patella", "sacrum", "os",
                "ossa", "ossis", "costa", "costae", "cranium", "cranii", "skeleton", "skeletal",
                "osseous", "phalanges", "phalanx", "metacarpal", "metatarsal", "tarsus", "carpus",
                "mandible", "maxilla", "vertebrae", "spine", "ilium", "ischium", "pubis", "coccyx",
                "hyoid", "clavicula", "scapulae", "vomer", "ethmoid", "sphenoid", "calcaneus",
                "talus", "navicular", "cuboid", "cuneiform", "sesamoid", "cartilage"
            }),

            // Priority 8: Regions
            new SystemRule(SystemCategory.Regions_of_Human_Body, GROUP_REGIONS, "Regions of Human Body", new[]
            {
                "head", "neck", "thorax", "abdomen", "arm", "forearm", "hand", "thigh",
                "leg", "foot", "region", "regio", "regiones", "torso", "trunk", "limb",
                "extremity", "cranial", "facial", "axilla", "perineum", "dorsum", "brachium",
                "antebrachium", "crus", "suras", "pedis", "manus"
            }),

            // Fallback: Unclassified
            new SystemRule(SystemCategory.Unclassified, GROUP_UNCLASSIFIED, "Unclassified", new string[0])
        };
    }

#if UNITY_EDITOR
    private struct TransformParentRecord
    {
        public Transform transform;
        public Transform originalParent;
    }
    private List<TransformParentRecord> snapshot = new List<TransformParentRecord>();

    [ContextMenu("Organize Anatomy")]
    public void OrganizeAnatomy()
    {
        GameObject root = (targetAnatomyRoot != null) ? targetAnatomyRoot : gameObject;

        if (rules == null || rules.Count == 0)
        {
            InitializeDefaultRules();
        }

        // Unpack prefab instance if locked
        if (PrefabUtility.IsPartOfPrefabInstance(root))
        {
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }

        Undo.RegisterFullObjectHierarchyUndo(root, "Organize Anatomy");

        // 1. Get or create the 9 system parents
        Dictionary<SystemCategory, Transform> systemParents = new Dictionary<SystemCategory, Transform>();
        foreach (var rule in rules)
        {
            Transform existing = root.transform.Find(rule.groupName);
            if (existing == null)
            {
                GameObject go = new GameObject(rule.groupName);
                Undo.RegisterCreatedObjectUndo(go, "Create System Group");
                Undo.SetTransformParent(go.transform, root.transform, "Set Group Parent");
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                systemParents[rule.category] = go.transform;
            }
            else
            {
                systemParents[rule.category] = existing;
            }
        }

        HashSet<Transform> systemParentSet = new HashSet<Transform>(systemParents.Values);

        // 2. Gather all non-system descendant GameObjects
        Transform[] allDescendants = root.GetComponentsInChildren<Transform>(true);
        List<GameObject> toOrganize = new List<GameObject>();

        foreach (Transform t in allDescendants)
        {
            if (t == null || t == root.transform) continue;
            if (systemParentSet.Contains(t)) continue;
            toOrganize.Add(t.gameObject);
        }

        // Snapshot for Undo
        snapshot.Clear();
        foreach (var go in toOrganize)
        {
            if (go != null)
            {
                snapshot.Add(new TransformParentRecord
                {
                    transform = go.transform,
                    originalParent = go.transform.parent
                });
            }
        }

        // 3. Classify and reparent
        Dictionary<SystemCategory, int> counts = new Dictionary<SystemCategory, int>();
        foreach (SystemCategory cat in Enum.GetValues(typeof(SystemCategory))) counts[cat] = 0;

        for (int i = 0; i < toOrganize.Count; i++)
        {
            GameObject go = toOrganize[i];
            if (go == null) continue;

            SystemCategory cat = ClassifyName(go.name);
            Transform targetParent = systemParents[cat];

            if (go.transform.parent != targetParent)
            {
                Undo.SetTransformParent(go.transform, targetParent, "Organize Anatomy");
                go.transform.SetParent(targetParent, true);
            }

            counts[cat]++;
        }

        // 4. Print Report
        string report = "=== Z-Anatomy Organization Report ===\n";
        foreach (var rule in rules)
        {
            int c = counts.ContainsKey(rule.category) ? counts[rule.category] : 0;
            report += $"{rule.displayName}: {c} objects\n";
        }
        report += $"Total Processed: {toOrganize.Count} objects\n";
        report += "====================================";
        Debug.Log(report);

        EditorUtility.SetDirty(root);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
    }

    [ContextMenu("Undo Organization")]
    public void UndoOrganization()
    {
        GameObject root = (targetAnatomyRoot != null) ? targetAnatomyRoot : gameObject;

        if (snapshot.Count > 0)
        {
            Undo.RegisterFullObjectHierarchyUndo(root, "Undo Anatomy Organization");
            foreach (var rec in snapshot)
            {
                if (rec.transform != null)
                {
                    Undo.SetTransformParent(rec.transform, rec.originalParent, "Revert Parent");
                    rec.transform.SetParent(rec.originalParent, true);
                }
            }
            snapshot.Clear();
            Debug.Log("[Z-Anatomy] Successfully reverted to original hierarchy.");
        }
        else
        {
            Undo.PerformUndo();
        }
    }

    private SystemCategory ClassifyName(string objName)
    {
        if (string.IsNullOrEmpty(objName)) return SystemCategory.Unclassified;
        string lower = objName.ToLowerInvariant();

        for (int i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (rule.category == SystemCategory.Unclassified) continue;

            foreach (string kw in rule.keywords)
            {
                if (string.IsNullOrWhiteSpace(kw)) continue;
                string cleanKw = kw.Trim().ToLowerInvariant();

                if (lower.Contains(cleanKw))
                {
                    if (cleanKw.Length <= 2)
                    {
                        string pattern = $@"(^|[^a-z0-9]){Regex.Escape(cleanKw)}([^a-z0-9]|$)";
                        if (Regex.IsMatch(lower, pattern)) return rule.category;
                    }
                    else
                    {
                        return rule.category;
                    }
                }
            }
        }

        return SystemCategory.Unclassified;
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(AnatomyBodyPartsGroup))]
public class AnatomyBodyPartsGroupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AnatomyBodyPartsGroup script = (AnatomyBodyPartsGroup)target;

        EditorGUILayout.Space(12);
        EditorGUILayout.HelpBox("Click below to separate all anatomy objects into their 9 system parent groups.", MessageType.Info);

        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f, 1f);
        if (GUILayout.Button("Organize Anatomy", GUILayout.Height(38)))
        {
            script.OrganizeAnatomy();
        }

        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("Undo Organization", GUILayout.Height(26)))
        {
            script.UndoOrganization();
        }
    }
}
#endif
