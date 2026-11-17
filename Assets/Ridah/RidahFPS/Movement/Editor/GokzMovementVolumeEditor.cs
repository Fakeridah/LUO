#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace RidahFPS.Controllers
{
    [CustomEditor(typeof(GokzMovementVolume))]
    public sealed class GokzMovementVolumeEditor : Editor
    {
        private SerializedProperty _movementType;
        private SerializedProperty _sourceFriction;
        private SerializedProperty _sourceMaxSpeedFactor;
        private SerializedProperty _sourceJumpFactor;
        private SerializedProperty _sourceClimbable;
        private SerializedProperty _useTransformHeight;
        private SerializedProperty _waterType;
        private SerializedProperty _surfaceHeight;
        private SerializedProperty _gravityScale;
        private SerializedProperty _priority;
        private SerializedProperty _localPushDirection;
        private SerializedProperty _pushSpeedHU;
        private SerializedProperty _pushOnce;

        private void OnEnable()
        {
            _movementType = serializedObject.FindProperty("movementType");
            _sourceFriction = serializedObject.FindProperty("sourceFriction");
            _sourceMaxSpeedFactor = serializedObject.FindProperty("sourceMaxSpeedFactor");
            _sourceJumpFactor = serializedObject.FindProperty("sourceJumpFactor");
            _sourceClimbable = serializedObject.FindProperty("sourceClimbable");
            _useTransformHeight = serializedObject.FindProperty("useTransformHeight");
            _waterType = serializedObject.FindProperty("waterType");
            _surfaceHeight = serializedObject.FindProperty("surfaceHeight");
            _gravityScale = serializedObject.FindProperty("gravityScale");
            _priority = serializedObject.FindProperty("priority");
            _localPushDirection = serializedObject.FindProperty("localPushDirection");
            _pushSpeedHU = serializedObject.FindProperty("pushSpeedHU");
            _pushOnce = serializedObject.FindProperty("pushOnce");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_movementType, new GUIContent("Type"));
            GokzMovementVolumeType movementType =
                (GokzMovementVolumeType)_movementType.enumValueIndex;

            switch (movementType)
            {
                case GokzMovementVolumeType.Ice:
                    EditorGUILayout.HelpBox(
                        "Source ice surface: low friction, solid collider.",
                        MessageType.Info);
                    break;

                case GokzMovementVolumeType.Ladder:
                    EditorGUILayout.HelpBox(
                        "Source ladder surface: solid collider detected by LadderMove.",
                        MessageType.Info);
                    break;

                case GokzMovementVolumeType.CustomSurface:
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField("Surface Properties", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(_sourceFriction, new GUIContent("Friction"));
                    EditorGUILayout.PropertyField(_sourceMaxSpeedFactor, new GUIContent("Max Speed Factor"));
                    EditorGUILayout.PropertyField(_sourceJumpFactor, new GUIContent("Jump Factor"));
                    EditorGUILayout.PropertyField(_sourceClimbable, new GUIContent("Climbable"));
                    break;

                case GokzMovementVolumeType.Water:
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField("Water", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(_useTransformHeight, new GUIContent("Use Transform Height"));
                    EditorGUILayout.PropertyField(_waterType, new GUIContent("Water Type"));
                    if (!_useTransformHeight.boolValue)
                        EditorGUILayout.PropertyField(_surfaceHeight, new GUIContent("Surface Height"));
                    break;

                case GokzMovementVolumeType.Gravity:
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField("Gravity", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(_gravityScale, new GUIContent("Gravity Scale"));
                    EditorGUILayout.PropertyField(_priority, new GUIContent("Priority"));
                    break;

                case GokzMovementVolumeType.Push:
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField("Push", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(_localPushDirection, new GUIContent("Local Direction"));
                    EditorGUILayout.PropertyField(_pushSpeedHU, new GUIContent("Push Speed (HU/s)"));
                    EditorGUILayout.PropertyField(_pushOnce, new GUIContent("Push Once"));
                    if (!_pushOnce.boolValue &&
                        Mathf.Abs(_localPushDirection.vector3Value.y) > 0.0001f)
                    {
                        EditorGUILayout.HelpBox(
                            "普通 Source trigger_push 持续写入 BaseVelocity，不是一次性跳台冲量。向上跳台请启用 Push Once；当前模式只有在产生足够的向上速度时才会脱离地面。",
                            MessageType.Info);
                    }
                    break;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Slope behavior is calculated automatically from the collision plane; it has no separate field.",
                MessageType.None);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
