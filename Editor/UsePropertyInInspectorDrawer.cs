﻿using System.Reflection;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace LevelCaptureTool {

    [CustomPropertyDrawer(typeof(UsePropertyInInspector))]
    public class UsePropertyInInspectorDrawer : PropertyDrawer {

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            var fieldAttribute = (UsePropertyInInspector) attribute;
            var fieldName = fieldAttribute.FieldName;

            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(position, property, label);

            if (EditorGUI.EndChangeCheck()) {
                var targetObject = property.serializedObject.targetObject;
                var propertyInfo = targetObject.GetType().GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (propertyInfo == null) {
                    Debug.LogError($"Property {fieldName} does not exist in Type {targetObject.GetType().Name}");
                }
                propertyInfo?.SetValue(targetObject, propertyInfo.GetValue(targetObject));
            }
        }
    }
}
