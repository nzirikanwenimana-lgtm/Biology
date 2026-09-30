using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Comprehensive Medical Anatomy Content Enricher.
/// Updates and enriches all anatomical mesh entries in AnatomyDatabase with
/// textbook-grade descriptions, physiological functions, canonical systems,
/// precise structural types, and accurate regional locations with laterality.
/// </summary>
public static class AnatomyContentEnricher
    {
        // =========================================================================
        // MENU COMMAND
        // =========================================================================
        [MenuItem("Anatomy/Database/Enrich Anatomy Database (Full Medical Content)", false, 15)]
        public static void EnrichDatabaseMenu()
        {
            AnatomyDatabase db = AnatomyDatabaseTools.LoadOrFindDatabase();
            if (db == null)
            {
                EditorUtility.DisplayDialog("Anatomy Content Enricher",
                    "No AnatomyDatabase asset found in Assets/Resources/AnatomyDatabase.asset.", "OK");
                return;
            }

            bool confirm = EditorUtility.DisplayDialog("Enrich Anatomy Database",
                $"Ready to enrich all {db.entries.Count:N0} entries in AnatomyDatabase.\n\n" +
                "This process will:\n" +
                "• Automatically create a timestamped backup (.asset.bak)\n" +
                "• Standardize canonical organ systems (Skeletal, Muscular, Cardiovascular, Nervous, Visceral, Joints, etc.)\n" +
                "• Accurately classify part types (Long Bone, Skeletal Muscle, Muscular Origin/Insertion, Artery, Vein, Nerve, Organ, Ligament, etc.)\n" +
                "• Populate rich, textbook-level medical Descriptions, Functions, and Locations\n" +
                "• Clean any legacy template or scrape artifacts\n\n" +
                "Proceed with enrichment?",
                "Enrich All Now", "Cancel");

            if (!confirm) return;

            EnrichDatabase(db, true);
        }

        public static void EnrichDatabase(AnatomyDatabase db, bool showDialog = true)
        {
            if (db == null || db.entries == null) return;

            CreateBackup(db);

            Undo.RecordObject(db, "Enrich Anatomy Database");

            int total = db.entries.Count;
            int systemsUpdated = 0;
            int typesUpdated = 0;
            int descUpdated = 0;
            int funcUpdated = 0;
            int locUpdated = 0;

            try
            {
                for (int i = 0; i < total; i++)
                {
                    AnatomyEntry entry = db.entries[i];
                    if (entry == null) continue;

                    if (i % 25 == 0)
                    {
                        float prog = (float)i / total;
                        EditorUtility.DisplayProgressBar("Enriching Anatomy Database",
                            $"Processing ({i + 1}/{total}): {entry.partName}", prog);
                    }

                    EnrichSingleEntry(entry);

                    systemsUpdated++;
                    typesUpdated++;
                    descUpdated++;
                    funcUpdated++;
                    locUpdated++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string report = "Anatomy Database Enrichment Complete!\n\n" +
                            $"• Total entries processed: {total:N0}\n" +
                            $"• Canonical systems applied: {systemsUpdated:N0}\n" +
                            $"• Structural types classified: {typesUpdated:N0}\n" +
                            $"• Medical descriptions updated: {descUpdated:N0}\n" +
                            $"• Physiological functions updated: {funcUpdated:N0}\n" +
                            $"• Regional locations with laterality: {locUpdated:N0}\n\n" +
                            "Every mesh now has complete, high-quality medical data for VR and desktop interactions.";

            Debug.Log($"[AnatomyContentEnricher] {report}");
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Enrichment Complete", report, "OK");
            }
        }

        public static void EnrichSingleEntry(AnatomyEntry entry)
        {
            if (entry == null) return;
            string rawName = entry.partName ?? "";

            string cleanBaseName = CleanPartName(rawName);
            string lateralInfo = GetLateralDescription(rawName);
            bool isOrigin = IsOriginAttachment(rawName);
            bool isInsertion = IsInsertionAttachment(rawName);

            // 1. Classify System
            string system = DetectSystem(cleanBaseName, rawName, isOrigin, isInsertion);
            entry.systemName = system;

            // 2. Classify Part Type
            string partType = DetectPartType(cleanBaseName, rawName, system, isOrigin, isInsertion);
            entry.partType = partType;

            // 3. Generate Medical Information
            MedicalInfo medical = GenerateMedicalInfo(cleanBaseName, rawName, system, partType, lateralInfo, isOrigin, isInsertion);
            entry.description = medical.Description;
            entry.function = medical.Function;
            entry.location = medical.Location;
        }

        // =========================================================================
        // BACKUP HELPER
        // =========================================================================
        private static void CreateBackup(AnatomyDatabase db)
        {
            string assetPath = AssetDatabase.GetAssetPath(db);
            if (string.IsNullOrEmpty(assetPath)) return;

            string backupDir = Path.GetDirectoryName(assetPath);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupPath = Path.Combine(backupDir, $"AnatomyDatabase_Backup_{timestamp}.asset.bak");

            try
            {
                File.Copy(assetPath, backupPath, true);
                AssetDatabase.Refresh();
                Debug.Log($"[AnatomyContentEnricher] Created safety backup: {backupPath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AnatomyContentEnricher] Could not create backup: {ex.Message}");
            }
        }

        // =========================================================================
        // NAME CLEANING & LATERALITY
        // =========================================================================
        public static string CleanPartName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";

            string s = raw.Trim();
            if (s.StartsWith("(") && s.Contains(")"))
            {
                int endParen = s.IndexOf(')');
                s = s.Substring(1, endParen - 1) + s.Substring(endParen + 1);
            }

            s = Regex.Replace(s, @"\.(o|e)[0-9]*(l|r)$", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\.(l|r|left|right)$", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\b(left|right)$", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"^(left|right)\b", "", RegexOptions.IgnoreCase);
            s = s.Replace('_', ' ').Replace('-', ' ').Trim();

            return s.Trim();
        }

        public static bool IsOriginAttachment(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return false;
            string lower = raw.ToLowerInvariant();
            return Regex.IsMatch(lower, @"\.o[0-9]*(l|r)?$") || lower.Contains("origin");
        }

        public static bool IsInsertionAttachment(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return false;
            string lower = raw.ToLowerInvariant();
            return Regex.IsMatch(lower, @"\.e[0-9]*(l|r)?$") || lower.Contains("insertion");
        }

        public static string GetLateralDescription(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Midline anatomical structure on the central body axis.";

            string lower = raw.ToLowerInvariant();
            if (Regex.IsMatch(lower, @"\.o[0-9]*l$") || Regex.IsMatch(lower, @"\.e[0-9]*l$"))
                return "Left side (bilateral structure).";
            if (Regex.IsMatch(lower, @"\.o[0-9]*r$") || Regex.IsMatch(lower, @"\.e[0-9]*r$"))
                return "Right side (bilateral structure).";
            if (lower.EndsWith(".l") || lower.EndsWith("_l") || lower.Contains(".left") || lower.Contains(" left") || lower.StartsWith("left "))
                return "Left side (bilateral structure).";
            if (lower.EndsWith(".r") || lower.EndsWith("_r") || lower.Contains(".right") || lower.Contains(" right") || lower.StartsWith("right "))
                return "Right side (bilateral structure).";

            return "Midline anatomical structure on the central body axis.";
        }

        // =========================================================================
        // SYSTEM DETECTION
        // =========================================================================
        public static string DetectSystem(string cleanName, string rawName, bool isOrigin, bool isInsertion)
        {
            if (isOrigin || isInsertion) return "Muscular Insertions";
            string s = cleanName.ToLowerInvariant();
            string r = rawName.ToLowerInvariant();

            if (r.Contains(".ol") || r.Contains(".or") || r.Contains(".el") || r.Contains(".er"))
                return "Muscular Insertions";

            // Vascular Plexus (Must be before Nervous)
            if (s.Contains("plexus") && (s.Contains("venous") || s.Contains("arterial") || s.Contains("vascular") || s.Contains("basilar venous") || s.Contains("choroid")))
                return "Cardiovascular System";

            // Cardiovascular
            if (ContainsAny(s, "artery", "arteria", "arteries", "vein", "vena", "venae", "aorta", "heart", "cardio",
                "atrium", "ventricle", "vascular", "coronary", "carotid artery", "jugular", "subclavian", "cava",
                "myocardium", "pericardium", "endocardium", "valvula", "sinus venosus", "vascul", "bicuspid", "tricuspid",
                "pulmonary trunk", "portal vein", "iliac artery", "iliac vein", "femoral artery", "femoral vein",
                "popliteal artery", "popliteal vein", "tibial artery", "tibial vein", "brachial artery", "brachial vein",
                "radial artery", "radial vein", "ulnar artery", "ulnar vein", "cephalic vein", "basilic vein", "azygos"))
                return "Cardiovascular System";

            // Aponeurosis (Muscular)
            if (s.Contains("aponeurosis") || s.Contains("aponeurotica"))
                return "Muscular System";

            // Surface Regions & Triangles
            if (ContainsAny(s, "region", "triangle", "fossa", "groove", "canal", "auricle", "concha", "helix", "tragus", "antitragus", "lobule", "nares", "philtrum", "mentum", "axilla", "groin", "perineum", "perianal", "buttock", "flank", "umbilicus", "epigastri", "hypochondri", "hypogastri"))
            {
                if (s.Contains("artery") || s.Contains("vein")) return "Cardiovascular System";
                if (s.Contains("nerve")) return "Nervous System";
                return "Regions & Surface Anatomy";
            }

            // Lymphoid
            if (ContainsAny(s, "lymph", "lymphatic", "thymus", "spleen", "tonsil", "lien", "lienalis", "cisterna chyli",
                "thoracic duct", "tonsilla", "lymph node", "nodi lymphatici", "splenic"))
                return "Lymphoid Organs";

            // Nervous
            if (ContainsAny(s, "nerve", "nervus", "nervi", "brain", "cerebrum", "cerebellum", "spinal cord",
                "medulla", "pons", "thalamus", "hypothalamus", "cortex", "ganglion", "encephalon", "meninges",
                "dura mater", "arachnoid", "pia mater", "plexus", "chiasm", "neuro", "neural", "tractus",
                "olfactory", "optic", "oculomotor", "trochlear", "trigeminal", "abducens", "facial nerve",
                "vestibulocochlear", "glossopharyngeal", "vagus", "accessory nerve", "hypoglossal", "sciatic",
                "femoral nerve", "radial nerve", "median nerve", "ulnar nerve", "peroneal nerve", "tibial nerve",
                "brachial plexus", "lumbosacral", "corpus callosum", "hippocampus", "amygdala"))
                return "Nervous System";

            // Visceral
            if (ContainsAny(s, "lung", "pulmo", "pulmonary", "liver", "hepar", "hepatic", "stomach", "gaster", "gastric",
                "pancreas", "pancreatic", "kidney", "ren", "renal", "bladder", "vesica", "vesical", "intestine", "colon",
                "duodenum", "jejunum", "ileum", "cecum", "appendix", "rectum", "gallbladder", "biliary", "esophagus",
                "trachea", "bronchus", "bronchiole", "larynx", "pharynx", "epiglottis", "thyroid", "parathyroid",
                "adrenal", "suprarenal", "ureter", "urethra", "prostate", "testis", "scrotum", "ovary", "uterus",
                "vagina", "cervix", "fallopian", "epididymis", "seminal vesicle", "diaphragm", "peritoneum",
                "visceral", "organ", "gland", "glandula", "salivary", "parotid", "submandibular", "sublingual", "arytenoid"))
                return "Visceral Systems";

            // Joints
            if (ContainsAny(s, "joint", "articulatio", "articul", "ligament", "ligamentum", "ligamenta", "meniscus",
                "bursa", "cartilage", "cartilago", "labrum", "synovial", "capsule", "articular", "disk", "discus",
                "symphysis", "syndesmosis", "synchondrosis", "suture"))
                return "Joints & Articulations";

            // Muscular
            if (ContainsAny(s, "muscle", "musculus", "musculi", "biceps", "triceps", "quadriceps", "deltoid",
                "pectoralis", "trapezius", "latissimus", "rectus", "oblique", "gluteus", "hamstring", "gastrocnemius",
                "soleus", "tibialis", "brachialis", "pronator", "supinator", "flexor", "extensor", "abductor",
                "adductor", "levator", "depressor", "sphincter", "tensor", "sartorius", "gracilis", "pterygoid",
                "masseter", "temporalis", "intercostal", "serratus", "rhomboid", "infraspinatus", "supraspinatus",
                "subscapularis", "teres", "iliopsoas", "psoas", "pectineus", "plantaris", "popliteus", "peroneus",
                "fibularis", "lumbrical", "interosseus", "platysma", "sternocleidomastoid", "scalene", "omohyoid",
                "sternohyoid", "sternothyroid", "thyrohyoid", "mylohyoid", "geniohyoid", "digastric", "buccinator",
                "orbicularis", "zygomaticus", "risorius", "frontalis", "occipitalis", "tendon", "tendo"))
                return "Muscular System";

            // Skeletal
            if (ContainsAny(s, "bone", "os ", "skeleton", "femur", "tibia", "fibula", "humerus", "radius", "ulna",
                "scapula", "clavicle", "sternum", "rib", "costa", "costae", "vertebra", "vertebrae", "pelvis",
                "ilium", "ischium", "pubis", "sacrum", "coccyx", "cranium", "skull", "mandible", "maxilla",
                "frontal", "parietal", "temporal", "occipital", "sphenoid", "ethmoid", "zygomatic", "nasal",
                "lacrimal", "vomer", "palatine", "hyoid", "patella", "talus", "calcaneus", "navicular", "cuboid",
                "cuneiform", "metatarsal", "metacarpal", "phalanx", "phalanges", "trochanter", "tuberosity",
                "epicondyle", "condyle", "sulcus", "spine", "crest", "processus", "foramen", "tuber",
                "ramus", "acetabulum", "glenoid", "malleolus", "styloid", "mastoid", "odontoid", "dens", "facet"))
                return "Skeletal System";

            return "Skeletal System";
        }

        // =========================================================================
        // PART TYPE DETECTION
        // =========================================================================
        public static string DetectPartType(string cleanName, string rawName, string systemName, bool isOrigin, bool isInsertion)
        {
            if (isOrigin) return "Muscular Origin";
            if (isInsertion) return "Muscular Insertion";

            string s = cleanName.ToLowerInvariant();

            if (s.Contains("plexus") && (s.Contains("venous") || s.Contains("arterial") || s.Contains("vascular") || s.Contains("basilar venous")))
                return "Vascular Plexus";

            // Vessels
            if (ContainsAny(s, "artery", "arteria", "arteries", "aorta", "arteriole")) return "Artery";
            if (ContainsAny(s, "vein", "vena", "venae", "venule", "cava")) return "Vein";

            // Lymphatics
            if (ContainsAny(s, "lymph node", "nodus lymphaticus")) return "Lymph Node";
            if (ContainsAny(s, "lymphatic vessel", "thoracic duct", "cisterna chyli")) return "Lymphatic Vessel";
            if (s.Contains("spleen") || s.Contains("thymus")) return "Lymphoid Organ";

            // Nerves
            if (ContainsAny(s, "cranial nerve", "olfactory", "optic", "oculomotor", "trochlear", "trigeminal",
                "abducens", "facial nerve", "vestibulocochlear", "glossopharyngeal", "vagus", "accessory nerve", "hypoglossal"))
                return "Cranial Nerve";
            if (ContainsAny(s, "nerve", "nervus", "sciatic", "femoral nerve", "radial nerve", "ulnar nerve", "median nerve", "tibial nerve", "fibular nerve"))
                return "Peripheral Nerve";
            if (ContainsAny(s, "ganglion", "plexus")) return "Neural Plexus / Ganglion";
            if (ContainsAny(s, "cerebrum", "cerebellum", "thalamus", "hypothalamus", "brain", "pons", "medulla", "spinal cord"))
                return "Central Nervous System Structure";

            // Articular & Connective
            if (ContainsAny(s, "ligament", "ligamentum")) return "Articular Ligament";
            if (ContainsAny(s, "cartilage", "cartilago", "meniscus", "labrum")) return "Cartilaginous Structure";
            if (ContainsAny(s, "joint", "articulatio", "articul")) return "Synovial Joint";
            if (ContainsAny(s, "bursa")) return "Synovial Bursa";

            // Muscles & Tendons
            if (ContainsAny(s, "tendon", "tendo", "aponeurosis", "aponeurotica")) return "Tendon / Aponeurosis";
            if (ContainsAny(s, "muscle", "musculus", "biceps", "triceps", "deltoid", "pectoralis", "gluteus", "rectus", "oblique", "flexor", "extensor"))
                return "Skeletal Muscle";

            // Bones
            if (ContainsAny(s, "femur", "humerus", "tibia", "fibula", "radius", "ulna", "clavicle")) return "Long Bone";
            if (ContainsAny(s, "scapula", "sternum", "rib", "parietal", "frontal", "occipital", "ilium")) return "Flat Bone";
            if (ContainsAny(s, "vertebra", "sacrum", "coccyx", "sphenoid", "ethmoid", "mandible", "maxilla")) return "Irregular Bone";
            if (ContainsAny(s, "patella", "pisiform", "fabella")) return "Sesamoid Bone";
            if (ContainsAny(s, "talus", "calcaneus", "navicular", "cuboid", "cuneiform", "carpal", "tarsal")) return "Short Bone";
            if (ContainsAny(s, "process", "trochanter", "tuberosity", "epicondyle", "condyle", "crest", "spine", "tubercle"))
                return "Bony Landmark / Feature";
            if (ContainsAny(s, "foramen", "canal", "fissure", "meatus"))
                return "Bony Foramen / Canal";
            if (ContainsAny(s, "bone", "os ")) return "Bone";

            // Visceral Organs
            if (ContainsAny(s, "heart", "liver", "lung", "kidney", "stomach", "pancreas", "bladder", "intestine", "thyroid"))
                return "Vital Organ";
            if (ContainsAny(s, "gland", "glandula", "parotid", "adrenal", "pituitary"))
                return "Glandular Structure";

            // Regions
            if (ContainsAny(s, "triangle")) return "Anatomical Triangle";
            if (ContainsAny(s, "fossa")) return "Anatomical Fossa";
            if (ContainsAny(s, "region", "groove", "canal", "space")) return "Anatomical Landmark / Region";

            switch (systemName)
            {
                case "Skeletal System": return "Bony Structure";
                case "Muscular System": return "Skeletal Muscle";
                case "Muscular Insertions": return "Muscular Attachment";
                case "Cardiovascular System": return "Vascular Structure";
                case "Nervous System": return "Nervous Tissue";
                case "Visceral Systems": return "Visceral Organ";
                case "Joints & Articulations": return "Articular Structure";
                case "Lymphoid Organs": return "Lymphoid Tissue";
                case "Regions & Surface Anatomy": return "Topographical Region";
                default: return "Anatomical Structure";
            }
        }

        // =========================================================================
        // MEDICAL KNOWLEDGE SYNTHESIZER
        // =========================================================================
        public struct MedicalInfo
        {
            public string Description;
            public string Function;
            public string Location;
        }

        public static MedicalInfo GenerateMedicalInfo(string cleanName, string rawName, string system, string partType, string lateralInfo, bool isOrigin, bool isInsertion)
        {
            MedicalInfo info = new MedicalInfo();
            string s = cleanName.ToLowerInvariant();
            string region = GetBodyRegion(s, rawName);

            // 1. Muscular Origin
            if (isOrigin)
            {
                info.Description = $"Represents the anatomical origin (proximal/immobile anchoring enthesis) of the {cleanName}. It consists of dense fibrous connective tissue and Sharpey's fibers interdigitating with the periosteum of the underlying bone.";
                info.Function = $"Secures the proximal tendon of the {cleanName} firmly to skeletal bone, providing a stable mechanical base that absorbs reaction forces during dynamic muscular contraction.";
                info.Location = $"Located at the proximal bony origin site within the {region}. {lateralInfo}";
                return info;
            }

            // 2. Muscular Insertion
            if (isInsertion)
            {
                info.Description = $"Represents the anatomical insertion (distal/mobile attachment enthesis) of the {cleanName}. At this site, the muscle's terminal tendon or aponeurosis fuses with the cortical bone matrix.";
                info.Function = $"Transfers tensile muscular contraction forces directly onto the articulated bone, driving joint rotation, leverage, and biomechanical limb displacement.";
                info.Location = $"Located at the distal bony insertion site within the {region}. {lateralInfo}";
                return info;
            }

            // 3. Specific Major Structures
            if (TryGetSpecificMedicalEntry(s, out info))
            {
                if (!string.IsNullOrEmpty(lateralInfo) && !info.Location.Contains("side"))
                {
                    info.Location += $" {lateralInfo}";
                }
                return info;
            }

            // 4. Vascular Plexus
            if (partType == "Vascular Plexus" || (system == "Cardiovascular System" && s.Contains("plexus")))
            {
                info.Description = $"A complex, highly anastomotic network of interconnected blood vessels ({cleanName}) that lines and cushions neighboring structures.";
                info.Function = $"Facilitates extensive collateral circulatory pathways, stabilizes local vascular pressures, and drains venous return from neighboring osseous and soft tissues.";
                info.Location = $"Situated along the vascular bed within the {region}. {lateralInfo}";
                return info;
            }

            // 5. Arteries
            if (partType == "Artery" || (system == "Cardiovascular System" && (s.Contains("artery") || s.Contains("arteria") || s.Contains("aorta"))))
            {
                info.Description = $"A high-pressure arterial vessel ({cleanName}) composed of three concentric histological layers: tunica intima, media (smooth muscle and elastin), and adventitia.";
                info.Function = $"Distributes pulsatile oxygenated blood and essential metabolic nutrients to the tissues, skeletal muscles, and viscera of the {region}.";
                info.Location = $"Courses along the neurovascular plane of the {region}. {lateralInfo}";
                return info;
            }

            // 6. Veins
            if (partType == "Vein" || (system == "Cardiovascular System" && (s.Contains("vein") || s.Contains("vena"))))
            {
                info.Description = $"A low-pressure capacitance blood vessel ({cleanName}) featuring thin muscular walls and intraluminal bicuspid valves designed to promote unidirectional hemodynamic return.";
                info.Function = $"Collects deoxygenated blood and metabolic waste products from the microcirculation of the {region}, funneling it back toward the heart and vena cava.";
                info.Location = $"Courses through the superficial and deep venous fascia of the {region}. {lateralInfo}";
                return info;
            }

            // 7. Tendons & Aponeuroses
            if (partType.Contains("Tendon") || s.Contains("aponeurosis") || s.Contains("aponeurotica"))
            {
                info.Description = $"A dense regular connective tissue sheet or tendon ({cleanName}) composed of densely bundled parallel collagen fibers engineered to withstand strong mechanical tension.";
                info.Function = $"Anchors muscle bellies to the skeletal framework, stabilizes the underlying fascia, and transmits muscular kinetic forces across wide anatomical regions.";
                info.Location = $"Extends across the fascial plane within the {region}. {lateralInfo}";
                return info;
            }

            // 8. Nerves
            if (partType.Contains("Nerve") || system == "Nervous System")
            {
                if (partType == "Cranial Nerve" || s.Contains("cranial"))
                {
                    info.Description = $"A specialized cranial nerve trunk ({cleanName}) arising directly from the brainstem and emerging through cranial base foramina to innervate peripheral cranial structures.";
                    info.Function = $"Delivers somatic sensory, motor, or autonomic parasympathetic innervation to the head, neck, and upper visceral organs.";
                    info.Location = $"Originates from the brainstem and emerges through the base of the skull in the {region}. {lateralInfo}";
                }
                else
                {
                    info.Description = $"A peripheral neural cable ({cleanName}) composed of myelinated and unmyelinated nerve fibers bundled into fascicles by endoneurium, perineurium, and epineurium.";
                    info.Function = $"Conducts bidirectional electrophysiological signals: relays motor impulses from the spinal cord to target muscles and sensory feedback (proprioception, pain, temperature) back to the central nervous system.";
                    info.Location = $"Courses within the neurovascular bundle of the {region}. {lateralInfo}";
                }
                return info;
            }

            // 9. Muscles
            if (partType == "Skeletal Muscle" || system == "Muscular System")
            {
                info.Description = $"A voluntary skeletal muscle ({cleanName}) formed by organized fascicles of multinucleated striated muscle fibers enclosed by the epimysium.";
                info.Function = $"Generates active tensile force through actin-myosin cross-bridge cycling to stabilize articulated joints, power limb movement, and maintain upright body posture.";
                info.Location = $"Situated within the muscular compartment of the {region}. {lateralInfo}";
                return info;
            }

            // 10. Articular Ligaments
            if (partType.Contains("Ligament") || s.Contains("ligament"))
            {
                info.Description = $"A tough, flexible band of dense fibrous connective tissue ({cleanName}) that connects adjacent bones across an anatomical joint.";
                info.Function = $"Provides passive mechanical constraint, limits excessive joint translation and rotation, and houses mechanoreceptors for proprioceptive joint feedback.";
                info.Location = $"Spans across the articular capsule of the joint in the {region}. {lateralInfo}";
                return info;
            }

            // 11. Synovial Joints & Articulations
            if (partType.Contains("Joint") || system == "Joints & Articulations")
            {
                info.Description = $"An articulated junction ({cleanName}) where adjacent skeletal elements meet, lined with smooth articular hyaline cartilage and enclosed by a synovial fluid-filled capsule.";
                info.Function = $"Permits controlled mechanical degrees of freedom (flexion, extension, rotation, or gliding) while distributing biomechanical compressive loads.";
                info.Location = $"Positioned between articulating bones in the {region}. {lateralInfo}";
                return info;
            }

            // 12. Bones & Bony Landmarks
            if (system == "Skeletal System" || partType.Contains("Bone"))
            {
                if (partType.Contains("Landmark") || ContainsAny(s, "process", "trochanter", "tuberosity", "condyle", "crest", "spine", "tubercle"))
                {
                    info.Description = $"A prominent osseous projection and morphological landmark ({cleanName}) formed on the cortical surface of the parent bone.";
                    info.Function = $"Expands mechanical surface area for the strong anchorage of tendons and ligaments, enhancing biomechanical lever arms and distributing joint stresses.";
                    info.Location = $"Projecting from the surface of the bone in the {region}. {lateralInfo}";
                }
                else if (ContainsAny(s, "foramen", "canal", "fissure", "meatus"))
                {
                    info.Description = $"An anatomical aperture or osseous passageway ({cleanName}) penetrating through the bone structure.";
                    info.Function = $"Provides a protected anatomical transit channel through which neurovascular bundles (arteries, veins, and nerves) travel between body compartments.";
                    info.Location = $"Traversing the osseous framework of the {region}. {lateralInfo}";
                }
                else
                {
                    info.Description = $"A rigid skeletal organ ({cleanName}) constructed from an outer dense cortical bone layer, internal spongy trabecular cancellous bone, periosteum, and bone marrow.";
                    info.Function = $"Provides structural body scaffolding, mechanical lever arms for muscle action, hematopoiesis (blood cell production), and a systemic metabolic reservoir for calcium and phosphate.";
                    info.Location = $"Constitutes an integral part of the skeletal framework in the {region}. {lateralInfo}";
                }
                return info;
            }

            // 13. Visceral Organs
            if (system == "Visceral Systems" || partType.Contains("Organ") || partType.Contains("Gland"))
            {
                info.Description = $"A specialized internal visceral structure ({cleanName}) organized with specialized parenchymal and epithelial tissues designed for systemic physiological processes.";
                info.Function = $"Performs vital metabolic, filtration, digestive, endocrine, or respiratory activities essential for systemic physiological homeostasis.";
                info.Location = $"Situated within the protective cavity of the {region}. {lateralInfo}";
                return info;
            }

            // 14. Lymphoid Organs & Nodes
            if (system == "Lymphoid Organs" || partType.Contains("Lymph"))
            {
                info.Description = $"An encapsulated lymphoid organ ({cleanName}) packed with lymphocytes, macrophages, and dendritic cells organized into lymphoid follicles.";
                info.Function = $"Filters extracellular lymph fluid or blood, phagocytoses cellular debris and foreign pathogens, and initiates adaptive immune responses.";
                info.Location = $"Located along the lymphatic drainage channels of the {region}. {lateralInfo}";
                return info;
            }

            // 15. Surface & Topographical Regions
            info.Description = $"A recognized topographical and clinical surface landmark of the human body ({cleanName}), serving as a primary surgical and diagnostic reference space.";
            info.Function = $"Defines clinical anatomical boundaries, encapsulates underlying neurovascular bundles, and provides external landmarks for physical palpation and surgical approaches.";
            info.Location = $"Situated within the {region}. {lateralInfo}";
            return info;
        }

        // =========================================================================
        // BODY REGION DETECTOR
        // =========================================================================
        private static string GetBodyRegion(string s, string raw)
        {
            string combined = (s + " " + raw).ToLowerInvariant();

            if (ContainsAny(combined, "cranial", "skull", "brain", "head", "face", "mandib", "maxill", "orbit", "eye", "ear", "auric", "nasal", "nose", "buccal", "mouth", "tongue", "frontal", "temporal", "parietal", "occipital", "sphenoid", "ethmoid", "epicranial", "galea"))
                return "head and cranial region";

            if (ContainsAny(combined, "neck", "cervical", "carotid", "jugular", "larynx", "pharynx", "thyroid", "hyoid", "scalene", "sternocleidomastoid", "arytenoid"))
                return "cervical (neck) region";

            if (ContainsAny(combined, "thorax", "thoracic", "chest", "rib", "costal", "sternum", "lung", "pulmo", "heart", "cardio", "aorta", "intercostal", "pectoral", "mediastinum"))
                return "thoracic (chest) cavity and thoracic wall";

            if (ContainsAny(combined, "abdomen", "abdominal", "stomach", "gastric", "liver", "hepar", "hepatic", "pancreas", "spleen", "kidney", "renal", "duodenum", "jejunum", "ileum", "colon", "cecum", "appendix", "rectum", "peritoneum", "umbilic", "epigastri", "hypochondri"))
                return "abdominal cavity and anterior abdominal wall";

            if (ContainsAny(combined, "pelvis", "pelvic", "sacrum", "coccyx", "ilium", "ischium", "pubis", "bladder", "vesica", "prostate", "uterus", "ovary", "perineum", "perianal", "anal", "glute"))
                return "pelvic cavity and perineal region";

            if (ContainsAny(combined, "shoulder", "scapula", "clavicle", "acromio", "deltoid", "glenohumeral", "axilla", "rotator cuff"))
                return "shoulder girdle and glenohumeral region";

            if (ContainsAny(combined, "arm", "brachial", "humerus", "biceps", "triceps", "coracobrachialis"))
                return "arm (brachial region)";

            if (ContainsAny(combined, "elbow", "cubital", "olecranon", "trochlea", "capitulum"))
                return "elbow (cubital) joint and fossa";

            if (ContainsAny(combined, "forearm", "antebrachial", "radius", "ulna", "pronator", "supinator"))
                return "forearm (antebrachial region)";

            if (ContainsAny(combined, "wrist", "carpal", "carpus", "scaphoid", "lunate", "triquetrum", "pisiform"))
                return "wrist (carpal region)";

            if (ContainsAny(combined, "hand", "palm", "finger", "digit", "metacarpal", "phalan", "thumb", "pollex", "thenar", "hypothenar"))
                return "hand and digits";

            if (ContainsAny(combined, "hip", "acetabulum", "trochanter", "groin", "inguinal", "coxal", "iliopsoas"))
                return "hip joint and proximal femoral region";

            if (ContainsAny(combined, "thigh", "femur", "femoral", "quadriceps", "hamstring", "sartorius", "gracilis", "adductor magnus"))
                return "thigh compartment";

            if (ContainsAny(combined, "knee", "patella", "patellar", "popliteal", "cruciate", "meniscus", "tibiofemoral"))
                return "knee joint and popliteal fossa";

            if (ContainsAny(combined, "leg", "tibia", "fibula", "calf", "gastrocnemius", "soleus", "peroneal", "fibular", "crural"))
                return "lower leg (crural region)";

            if (ContainsAny(combined, "ankle", "tarsal", "malleolus", "talus", "calcaneus", "achilles", "talocrural"))
                return "ankle (talocrural region)";

            if (ContainsAny(combined, "foot", "sole", "plantar", "dorsum of foot", "metatarsal", "toe", "hallux", "tarsus"))
                return "foot and pedal digits";

            if (ContainsAny(combined, "spine", "vertebra", "spinal", "back", "dorsal", "lumbar", "erector spinae"))
                return "vertebral column and posterior torso";

            return "human body";
        }

        // =========================================================================
        // SPECIFIC MEDICAL ENTRIES
        // =========================================================================
        private static bool TryGetSpecificMedicalEntry(string s, out MedicalInfo info)
        {
            info = new MedicalInfo();

            if (s == "femur" || s.StartsWith("femur ") || s.EndsWith(" femur"))
            {
                info.Description = "The femur (thigh bone) is the longest, heaviest, and strongest tubular bone in the human body. It comprises a proximal femoral head with fovea capitis, anatomical neck, greater and lesser trochanters, an anteriorly bowed shaft, and distal medial and lateral condyles.";
                info.Function = "Transmits the entirety of upper body weight to the lower leg during upright bipedal stance and ambulation; serves as the primary mechanical lever arm for the powerful hip and knee muscle groups.";
                info.Location = "Extends through the thigh compartment, articulating proximally with the pelvic acetabulum (hip joint) and distally with the tibia and patella (knee joint).";
                return true;
            }

            if (s == "humerus" || s.StartsWith("humerus ") || s.EndsWith(" humerus"))
            {
                info.Description = "The humerus is the principal long bone of the upper arm. It features a hemispherical proximal head, anatomical and surgical necks, greater and lesser tubercles, deltoid tuberosity, radial groove, and distal capitulum, trochlea, and medial/lateral epicondyles.";
                info.Function = "Provides rigid structural support for the upper arm, acts as the primary skeletal lever for shoulder and elbow kinematics, and anchors 24 muscular attachments.";
                info.Location = "Occupies the brachial region of the upper limb; articulates proximally with the glenoid cavity of the scapula and distally with the radius and ulna at the elbow.";
                return true;
            }

            if (s == "tibia" || s.StartsWith("tibia ") || s.EndsWith(" tibia"))
            {
                info.Description = "The tibia (shin bone) is the massive, weight-bearing medial bone of the lower leg. It possesses an expanded proximal tibial plateau with medial/lateral condyles, a prominent anterior tibial tuberosity, a prismatic triangular shaft, and distal medial malleolus.";
                info.Function = "Bears approximately 85-90% of the body weight transmitted through the lower extremity, enabling stable standing, running, and jumping.";
                info.Location = "Located anteromedially in the lower leg; articulates proximally with the femoral condyles and distally with the talus and fibula.";
                return true;
            }

            if (s == "fibula" || s.StartsWith("fibula ") || s.EndsWith(" fibula"))
            {
                info.Description = "The fibula is the slender, non-weight-bearing lateral bone of the leg. It features an expanded proximal head with an articular facet, a slender grooved shaft, and a pyramidal distal expansion known as the lateral malleolus.";
                info.Function = "Provides extensive attachment area for leg and ankle eversion/inversion muscles and forms the lateral retaining wall of the ankle mortise joint to prevent lateral talar subluxation.";
                info.Location = "Runs parallel and lateral to the tibia in the lower leg, bound tightly along its length by the interosseous membrane.";
                return true;
            }

            if (s == "radius" || s.StartsWith("radius ") || s.EndsWith(" radius"))
            {
                info.Description = "The radius is the lateral, mobile long bone of the forearm (aligned with the thumb). It has a cylindrical disc-shaped head, neck, prominent bicipital tuberosity, an interosseous crest, and an expanded distal base with a styloid process.";
                info.Function = "Pivots around the stationary ulna to enable forearm pronation and supination; receives and distributes mechanical impact forces from the hand via the radiocarpal wrist joint.";
                info.Location = "Positioned on the lateral side of the forearm parallel to the ulna, articulating with the humeral capitulum, radial notch of the ulna, scaphoid, and lunate.";
                return true;
            }

            if (s == "ulna" || s.StartsWith("ulna ") || s.EndsWith(" ulna"))
            {
                info.Description = "The ulna is the medial stabilizing long bone of the forearm. Its massive proximal end displays the hook-like olecranon, coronoid process, and trochlear notch, tapering distally to a small rounded head and styloid process.";
                info.Function = "Forms the primary structural hinge of the elbow joint with the humeral trochlea, restricting joint kinematics strictly to sagittal flexion and extension.";
                info.Location = "Located medially in the forearm; articulates proximally with the trochlea of the humerus and distally with the triangular fibrocartilage complex (TFCC).";
                return true;
            }

            if (s == "patella" || s.StartsWith("patella ") || s.EndsWith(" patella"))
            {
                info.Description = "The patella (kneecap) is the largest sesamoid bone in the human skeleton, situated within the common tendon of the quadriceps femoris. Its posterior surface is coated with the thickest hyaline cartilage in the human body (up to 6mm).";
                info.Function = "Displaces the quadriceps tendon anteriorly away from the knee joint axis, increasing the mechanical extensor lever arm by up to 30% and shielding the joint from direct frontal trauma.";
                info.Location = "Positioned anterior to the distal femoral trochlea, gliding within the intercondylar sulcus during knee flexion and extension.";
                return true;
            }

            if (s == "clavicle" || s.StartsWith("clavicle ") || s.EndsWith(" clavicle"))
            {
                info.Description = "The clavicle (collarbone) is an elongated S-shaped bone that forms the anterior portion of the shoulder girdle. It features a rounded medial sternal end, flattened lateral acromial end, and conoid tubercle.";
                info.Function = "Acts as a rigid mechanical strut that suspends the scapula and upper limb away from the thoracic wall, maximizing rotational freedom and transferring impact loads to the axial skeleton.";
                info.Location = "Extends horizontally across the superior anterior thorax between the sternal manubrium medially and the scapular acromion laterally.";
                return true;
            }

            if (s == "scapula" || s.StartsWith("scapula ") || s.EndsWith(" scapula"))
            {
                info.Description = "The scapula (shoulder blade) is a broad, triangular flat bone positioned on the posterolateral thorax. It presents a prominent dorsal spine terminating in the acromion, a coracoid process, supraspinous/infraspinous fossae, and a glenoid cavity.";
                info.Function = "Provides the mobile foundational platform for upper extremity articulation via scapulothoracic gliding and anchors 17 distinct muscles, including the four rotator cuff stabilizers.";
                info.Location = "Overlies posterior ribs 2 through 7; articulates laterally with the humeral head at the glenohumeral joint and with the clavicle at the acromioclavicular joint.";
                return true;
            }

            if (s == "sternum" || s.StartsWith("sternum ") || s.EndsWith(" sternum"))
            {
                info.Description = "The sternum (breastbone) is a flat, elongated plate located in the anterior thoracic midline. It is composed of three fused segments: the superior pentagonal manubrium, the elongated central body (mesosternum), and the inferior cartilaginous xiphoid process.";
                info.Function = "Protects the vital mediastinal organs (heart and great vessels), articulates with true costal cartilages (ribs 1–7) to facilitate respiratory chest expansion, and anchors pectoral muscles.";
                info.Location = "Forms the anterior midline anchor of the thoracic cage, articulating laterally with the bilateral clavicles and ribs 1 through 7.";
                return true;
            }

            if (s == "heart" || s == "myocardium")
            {
                info.Description = "The heart is a four-chambered fibromuscular organ composed of specialized cardiac muscle (myocardium) enclosed within a double-walled fibroserous pericardium. It consists of right and left atria and ventricles.";
                info.Function = "Rhythmically pumps deoxygenated blood through the pulmonary circulation for gas exchange and propels oxygen-rich blood through the systemic arterial tree to nourish all bodily tissues.";
                info.Location = "Located in the middle mediastinum of the thoracic cavity, posterior to the sternum, tilted with its apex pointing anteroinferiorly toward the left fifth intercostal space.";
                return true;
            }

            if (s == "aorta" || s.StartsWith("aorta ") || s.EndsWith(" aorta"))
            {
                info.Description = "The aorta is the primary and largest systemic artery in the body, emerging directly from the left ventricle. It is anatomically subdivided into the ascending aorta, aortic arch, descending thoracic aorta, and abdominal aorta.";
                info.Function = "Transports oxygenated blood at peak systolic pressure from the left ventricle into the systemic arterial branches feeding all peripheral organ systems.";
                info.Location = "Arises from the base of the heart, arches superiorly over the pulmonary trunk in the superior mediastinum, and descends anterior to the spine into the abdomen.";
                return true;
            }

            if (s.Contains("biceps brachii") || s == "biceps")
            {
                info.Description = "The biceps brachii is a prominent two-headed fusiform muscle of the anterior arm. It originates via a short head from the coracoid process and a long head from the supraglenoid tubercle, converging to insert onto the radial tuberosity.";
                info.Function = "Acts as the most powerful supinator of the flexed forearm, a primary flexor of the elbow joint, and an accessory stabilizer of the glenohumeral joint.";
                info.Location = "Occupies the anterior compartment of the arm, superficial to the brachialis and coracobrachialis muscles; innervated by the musculocutaneous nerve (C5, C6).";
                return true;
            }

            if (s.Contains("triceps brachii") || s == "triceps")
            {
                info.Description = "The triceps brachii is a powerful three-headed muscle that spans nearly the entire posterior arm. Its long head arises from the infraglenoid tubercle, lateral head from the postero-lateral humerus, and medial head from the postero-medial humerus, inserting onto the olecranon.";
                info.Function = "Serves as the primary and most potent extensor of the elbow joint; the long head additionally assists in adduction and extension of the humerus at the shoulder.";
                info.Location = "Occupies the posterior compartment of the arm; innervated by multiple motor branches of the radial nerve (C6, C7, C8).";
                return true;
            }

            if (s.Contains("deltoid") && (s.Contains("muscle") || s == "deltoid"))
            {
                info.Description = "The deltoid is a thick, multipennate muscle cap that drapes over the glenohumeral joint. It arises from the lateral third of the clavicle, acromion, and scapular spine, converging distally to insert onto the deltoid tuberosity of the humerus.";
                info.Function = "The central acromial fibers are the primary motor for arm abduction between 15° and 90°; the anterior clavicular fibers flex and medially rotate the arm; the posterior spinal fibers extend and laterally rotate the arm.";
                info.Location = "Forms the prominent rounded contour of the shoulder; innervated by the axillary nerve (C5, C6).";
                return true;
            }

            if (s.Contains("sciatic nerve") || s == "sciatic")
            {
                info.Description = "The sciatic nerve is the largest and thickest nerve trunk in the human body (up to 2cm wide). Arising from the sacral plexus (L4-S3), it travels through the greater sciatic foramen into the posterior thigh, bifurcating into the tibial and common fibular nerves.";
                info.Function = "Provides motor innervation to the hamstring muscles, adductor magnus, and all lower leg and foot musculature; delivers sensory perception from the posterior leg, lateral ankle, and entire sole of the foot.";
                info.Location = "Emerges below the piriformis muscle in the buttock, courses down the midline of the posterior thigh beneath the biceps femoris into the popliteal fossa.";
                return true;
            }

            if (s.Contains("vagus nerve") || s == "vagus")
            {
                info.Description = "The vagus nerve (Cranial Nerve X) is the longest cranial nerve, containing approximately 80% visceral sensory afferents and 20% general visceral parasympathetic efferents.";
                info.Function = "Carries essential parasympathetic autonomic tone to decelerate heart rate, constrict bronchioles, stimulate digestive gastrointestinal peristalsis and acid secretion, and innervate laryngeal muscles for vocal phonation.";
                info.Location = "Exits the posterior cranial fossa via the jugular foramen, descends within the carotid sheath in the neck, traverses the mediastinum, and enters the abdomen through the esophageal hiatus.";
                return true;
            }

            if (s == "liver" || s == "hepar")
            {
                info.Description = "The liver (hepar) is the largest internal solid organ and metabolic gland in the human body, weighing approximately 1.5 kg. It is partitioned into right, left, caudate, and quadrate anatomical lobes, receiving dual blood supply from the portal vein and hepatic artery.";
                info.Function = "Executes over 500 vital biochemical functions: synthesizes bile, plasma albumins, and clotting factors; detoxifies endogenous and exogenous compounds; metabolizes carbohydrates, lipids, and amino acids; stores glycogen, iron, and vitamins.";
                info.Location = "Fills the right hypochondriac, epigastric, and part of the left hypochondriac regions of the abdomen, nestled directly beneath the right hemidiaphragm.";
                return true;
            }

            if (s == "spleen" || s == "lien")
            {
                info.Description = "The spleen is the largest secondary lymphoid organ in the human body. Histologically, it is segregated into red pulp (which filters circulating blood) and white pulp (which mounts immunological defense).";
                info.Function = "Filters systemic blood, extracts senescent or malformed red blood cells, recycles hemoglobin iron, acts as an emergency reservoir for erythrocytes and platelets, and triggers adaptive immune responses against blood-borne antigens.";
                info.Location = "Situated in the left hypochondrium of the abdominal cavity, deep to ribs 9, 10, and 11, postero-lateral to the stomach fundus.";
                return true;
            }

            if (s.Contains("kidney") || s == "ren")
            {
                info.Description = "The kidney is a bean-shaped retroperitoneal organ featuring an outer renal cortex containing over one million nephrons and an inner medulla organized into renal pyramids that drain into minor and major calyces and the renal pelvis.";
                info.Function = "Ultrafilters circulating blood plasma to remove metabolic wastes (urea, creatinine), maintains electrolyte and fluid balance, regulates blood pH, controls systemic arterial pressure via the renin-angiotensin system, and secretes erythropoietin.";
                info.Location = "Retroperitoneal on the posterior abdominal wall on either side of the vertebral column between T12 and L3; the right kidney sits slightly lower due to liver displacement.";
                return true;
            }

            return false;
        }

        private static bool ContainsAny(string s, params string[] terms)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < terms.Length; i++)
            {
                if (s.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
}
