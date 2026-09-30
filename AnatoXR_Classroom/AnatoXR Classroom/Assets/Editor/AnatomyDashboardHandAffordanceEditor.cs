using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AnatomyDashboardHandAffordance))]
public class AnatomyDashboardHandAffordanceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        AnatomyDashboardHandAffordance affordance = (AnatomyDashboardHandAffordance)target;

        serializedObject.Update();

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "Anatomy VR Hand Positioning & Affordance\n" +
            "• Adjust offsets below or drag the Gizmo directly in Scene View.\n" +
            "• Use 1-click presets to match the reference UI or wrist watch layout.",
            MessageType.Info
        );

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("⚡ Quick Positioning Presets", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.35f, 0.85f, 1f);
        if (GUILayout.Button("✨ Reference (Palm Float)", GUILayout.Height(30)))
        {
            Undo.RecordObject(affordance, "Apply Reference Palm Float Preset");
            affordance.ApplyPresetReferencePalm();
            EditorUtility.SetDirty(affordance);
        }
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("⌚ Wrist Tablet", GUILayout.Height(30)))
        {
            Undo.RecordObject(affordance, "Apply Wrist Tablet Preset");
            affordance.ApplyPresetWristTablet();
            EditorUtility.SetDirty(affordance);
        }

        if (GUILayout.Button("🎯 Comfort Reach", GUILayout.Height(30)))
        {
            Undo.RecordObject(affordance, "Apply Comfort Reach Preset");
            affordance.ApplyPresetComfortPoke();
            EditorUtility.SetDirty(affordance);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🔗 Hand Anchor Binding", EditorStyles.boldLabel);

        Transform currentAnchor = affordance.handAnchor;
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto-Find Left Hand (Tracked Root)"))
        {
            Undo.RecordObject(affordance, "Auto Find Hand Anchor");
            affordance.handAnchor = null;
            affordance.AutoLocateLeftHandAnchor();
            affordance.ApplyTransform();
            EditorUtility.SetDirty(affordance);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("📐 Position & Rotation Fine-Tuning", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        Vector3 pos = affordance.localPositionOffset;
        Vector3 rot = affordance.localRotationOffset;
        float scale = affordance.canvasWorldScale;

        pos.x = EditorGUILayout.Slider("Side Offset X (Left/Right)", pos.x, -0.30f, 0.30f);
        pos.y = EditorGUILayout.Slider("Height Offset Y (Above Palm)", pos.y, -0.30f, 0.30f);
        pos.z = EditorGUILayout.Slider("Distance Offset Z (Along Fingers)", pos.z, -0.30f, 0.30f);

        EditorGUILayout.Space(4);
        rot.x = EditorGUILayout.Slider("Tilt Pitch X (Towards Eyes)", rot.x, -180f, 180f);
        rot.y = EditorGUILayout.Slider("Yaw Y (Turn)", rot.y, -180f, 180f);
        rot.z = EditorGUILayout.Slider("Roll Z (Tilt)", rot.z, -180f, 180f);

        EditorGUILayout.Space(4);
        scale = EditorGUILayout.Slider("Canvas World Scale", scale, 0.00015f, 0.00080f);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(affordance, "Modify Dashboard Hand Transform");
            affordance.localPositionOffset = pos;
            affordance.localRotationOffset = rot;
            affordance.canvasWorldScale = scale;
            affordance.ApplyTransform();
            EditorUtility.SetDirty(affordance);
        }

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("⚙ Default Affordance Settings", EditorStyles.boldLabel);

        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        AnatomyDashboardHandAffordance affordance = (AnatomyDashboardHandAffordance)target;
        if (affordance == null || affordance.handAnchor == null) return;

        Canvas canvas = affordance.GetComponentInParent<Canvas>();
        Transform canvasTransform = (canvas != null) ? canvas.transform : affordance.transform;

        EditorGUI.BeginChangeCheck();

        Vector3 newWorldPos = Handles.PositionHandle(canvasTransform.position, canvasTransform.rotation);
        Quaternion newWorldRot = Handles.RotationHandle(canvasTransform.rotation, canvasTransform.position);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(affordance, "Transform Dashboard in Scene");
            Undo.RecordObject(canvasTransform, "Transform Dashboard Canvas");

            canvasTransform.position = newWorldPos;
            canvasTransform.rotation = newWorldRot;

            // Compute local offsets relative to handAnchor
            affordance.localPositionOffset = affordance.handAnchor.InverseTransformPoint(newWorldPos);
            affordance.localRotationOffset = (Quaternion.Inverse(affordance.handAnchor.rotation) * newWorldRot).eulerAngles;

            EditorUtility.SetDirty(affordance);
            EditorUtility.SetDirty(canvasTransform);
        }
    }
}
