using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace LevelCaptureTool {

    [CustomEditor(typeof(LevelCaptureTool))]
    public class LevelCaptureToolInspector : Editor {

        private const int ActionBoxPadding = 5;

        private static readonly HashSet<EventType> AcceptedEventTypes = new HashSet<EventType> {
            EventType.MouseDown,
            EventType.MouseDrag,
            EventType.MouseUp,
            EventType.KeyDown
        };

        private static readonly Color ToggleBgColor = new Color(1f, 0.85f, 0.4f);
        private static readonly Color ToggleTextColor = new Color(1f, 0.96f, 0.56f);
        private static readonly Color SelectionBgColor = new Color(0.07f, 0f, 0f, 0.4f);

        private GUIStyle _actionBoxStyle;
        private GUIStyle _toggleStyle;

        private bool _selectionMode;
        private bool _drawGizmos;
        private Vector2 _start;
        private Vector2 _end;

        private void OnSceneGUI() {
            if (!_selectionMode) {
                return;
            }
            serializedObject.Update();

            var levelCaptureTool = Selection.activeGameObject.GetComponent<LevelCaptureTool>();
            var bounds = FindBounds();
            DrawHandles(levelCaptureTool, bounds);
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(GetHashCode(), FocusType.Passive));

            var currentEvent = Event.current;
            var use = AcceptedEventTypes.Contains(currentEvent.type);
            var drawGizmosProperty = serializedObject.FindProperty("drawGizmos");
            var worldPos = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition).origin;

            switch (currentEvent.type) {
                case EventType.MouseDown:
                    _start = worldPos;
                    _end = worldPos;
                    _drawGizmos = drawGizmosProperty.boolValue;

                    ToggleGizmos(drawGizmosProperty, false);
                    break;
                case EventType.MouseDrag:
                    _end = worldPos;
                    break;
                case EventType.MouseUp:
                    UpdateCaptureRect(levelCaptureTool, bounds);
                    FinishSelection(drawGizmosProperty);
                    ToggleGizmos(drawGizmosProperty, _drawGizmos);
                    break;
                case EventType.KeyDown when currentEvent.keyCode == KeyCode.Escape:
                    FinishSelection(drawGizmosProperty);
                    ToggleGizmos(drawGizmosProperty, _drawGizmos);
                    break;
            }
            if (use) {
                serializedObject.ApplyModifiedProperties();
                currentEvent.Use();
            }
        }

        private static void ToggleGizmos(SerializedProperty drawGizmosProperty, bool enabled) {
            drawGizmosProperty.boolValue = enabled;
        }

        private void FinishSelection(SerializedProperty drawGizmosProperty) {
            drawGizmosProperty.boolValue = _drawGizmos;
            _selectionMode = false;
            _start = _end;

            GUIUtility.hotControl = 0;
        }

        private Rect FindBounds() {
            var minX = Mathf.Min(_start.x, _end.x);
            var minY = Mathf.Min(_start.y, _end.y);
            var maxX = Mathf.Max(_start.x, _end.x);
            var maxY = Mathf.Max(_start.y, _end.y);

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        private void DrawHandles(LevelCaptureTool levelCaptureTool, Rect bounds) {
            if (_start == _end) {
                return;
            }
            Handles.color = Color.green;
            Handles.DrawWireCube(bounds.center, new Vector3(bounds.size.x + levelCaptureTool.Margin, bounds.size.y + levelCaptureTool.Margin));
            Handles.color = Color.white;
            Handles.DrawSolidRectangleWithOutline(bounds, SelectionBgColor, Color.red);

            HandleUtility.Repaint();
        }

        private void UpdateCaptureRect(LevelCaptureTool levelCaptureTool, Rect bounds) {
            if (_start == _end) {
                return;
            }
            levelCaptureTool.transform.position = new Vector3(
                bounds.center.x,
                bounds.center.y,
                levelCaptureTool.transform.position.z
            );
            serializedObject.FindProperty("size").vector2Value = new Vector2(
                Mathf.Abs(bounds.size.x),
                Mathf.Abs(bounds.size.y)
            );
            levelCaptureTool.MarkDirty();
        }

        public override void OnInspectorGUI() {
            if (_actionBoxStyle == null) {
                _actionBoxStyle = new GUIStyle(GUI.skin.box) {
                    padding = new RectOffset(ActionBoxPadding, ActionBoxPadding, ActionBoxPadding, ActionBoxPadding)
                };
            }
            if (_toggleStyle == null) {
                _toggleStyle = new GUIStyle(GUI.skin.button) {
                    active = {textColor = ToggleTextColor},
                    hover = {textColor = ToggleTextColor},
                    normal = {textColor = ToggleTextColor},
                    focused = {textColor = ToggleTextColor},
                    onActive = {textColor = ToggleTextColor},
                    onHover = {textColor = ToggleTextColor},
                    onNormal = {textColor = ToggleTextColor},
                    onFocused = {textColor = ToggleTextColor}
                };
            }
            EditorGUI.BeginChangeCheck();
            serializedObject.UpdateIfRequiredOrScript();
            var iterator = serializedObject.GetIterator();
            for (var enterChildren = true; iterator.NextVisible(enterChildren); enterChildren = false) {
                if ("drawGizmos" == iterator.propertyPath || "saveLayersSeparately" == iterator.propertyPath) {
                    continue;
                }
                using (new EditorGUI.DisabledScope("m_Script" == iterator.propertyPath)) {
                    EditorGUILayout.PropertyField(iterator, true);
                }
            }
            if (EditorGUI.EndChangeCheck()) {
                serializedObject.ApplyModifiedProperties();
            }

            CreateOptionsBox();
            CreateActionsBox();
        }

        private void CreateOptionsBox() {
            GUILayout.Space(8f);
            GUILayout.BeginVertical(_actionBoxStyle);
            GUILayout.Label("Options", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("drawGizmos"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("saveLayersSeparately"));
            if (EditorGUI.EndChangeCheck()) {
                serializedObject.ApplyModifiedProperties();
            }

            GUILayout.EndVertical();
        }

        private void CreateActionsBox() {
            GUILayout.Space(4f);
            GUILayout.BeginVertical(_actionBoxStyle);
            GUILayout.Label("Actions", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            var freeformButtonContent = new GUIContent("Select", EditorGUIUtility.ObjectContent(null, typeof(BoxCollider2D)).image);

            GUI.backgroundColor = ToggleBgColor;
            _selectionMode = GUILayout.Toggle(_selectionMode, freeformButtonContent, _toggleStyle, GUILayout.Width(90f), GUILayout.Height(24f));
            GUI.backgroundColor = Color.white;

            var instance = ((LevelCaptureTool) serializedObject.targetObject);
            EditorGUI.BeginChangeCheck();
            if (GUILayout.Button("Trim Bounds", GUILayout.Height(24f))) {
                instance.TrimBounds();
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button("Update Camera", GUILayout.Height(24f))) {
                instance.UpdateCamera();
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button("Capture", GUILayout.Height(24f))) {
                instance.Capture();
                GUIUtility.ExitGUI();
            }
            if (EditorGUI.EndChangeCheck()) {
                serializedObject.ApplyModifiedProperties();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
    }
}
