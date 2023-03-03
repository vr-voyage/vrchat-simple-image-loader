#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using System.Security.Policy;
using UdonSharpEditor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDKBase;

namespace VRVoyage.SimpleScripts
{
    [CustomEditor(typeof(SimpleImageLoader))]
    public class SimpleImageLoaderEditor : Editor
    {
        ReorderableList urlsList;

        SerializedProperty urls;
        SerializedProperty receivingMaterial;
        SerializedProperty receivingPanel;
        SerializedProperty rescalePanel;
        SerializedProperty errorOutput;
        SerializedProperty errorTexture;
        SerializedProperty pictureRefreshTime;

        SerializedProperty synchronise;

        private void OnEnable()
        {
            urls = serializedObject.FindProperty(nameof(SimpleImageLoader.urls));
            receivingMaterial = serializedObject.FindProperty(nameof(SimpleImageLoader.receivingMaterial));
            receivingPanel = serializedObject.FindProperty(nameof(SimpleImageLoader.receivingPanel));
            rescalePanel = serializedObject.FindProperty(nameof(SimpleImageLoader.rescalePanel));
            errorOutput = serializedObject.FindProperty(nameof(SimpleImageLoader.errorOutput));
            errorTexture = serializedObject.FindProperty(nameof(SimpleImageLoader.errorTexture));
            pictureRefreshTime = serializedObject.FindProperty(nameof(SimpleImageLoader.pictureRefreshTime));

            synchronise = serializedObject.FindProperty(nameof(SimpleImageLoader.synchronise));

            /* Code shamelessly taken from USharpVideoInspector (USharpVideo : https://github.com/MerlinVR/USharpVideo) */
            urlsList = new ReorderableList(serializedObject, urls, true, true, true, true);
            urlsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                Rect fieldRect = new Rect(
                    x: rect.x,
                    y: rect.y + 2,
                    width: rect.width,
                    height: EditorGUIUtility.singleLineHeight);

                EditorGUI.PropertyField(
                    position: fieldRect,
                    property: urlsList.serializedProperty.GetArrayElementAtIndex(index),
                    label: new GUIContent());
            };
            urlsList.drawHeaderCallback = (Rect rect) => {
                EditorGUI.LabelField(
                    rect,
                    new GUIContent(
                        "Images URLS",
                        "The URLS of the pictures that should be displayed"));
            };

        }

        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawProgramSource(target)) return;

            EditorGUILayout.LabelField("Main Settings", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            synchronise.boolValue = EditorGUILayout.Toggle(
                "Synchronise",
                synchronise.boolValue);
            EditorGUI.EndChangeCheck();

            urlsList.DoLayoutList();
            EditorGUILayout.PropertyField(
                pictureRefreshTime,
                new GUIContent(
                    "Wait time in seconds",
                    "Wait time, in seconds, between the last download and the next one."));
            EditorGUILayout.PropertyField(receivingMaterial);
            EditorGUILayout.PropertyField(errorTexture);

            EditorGUILayout.LabelField("Additional settings", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            rescalePanel.boolValue = EditorGUILayout.Toggle(
                "Rescale the display panel",
                rescalePanel.boolValue);
            EditorGUI.EndChangeCheck();
            EditorGUILayout.PropertyField(receivingPanel, new GUIContent("Display panel"));
            EditorGUILayout.PropertyField(errorOutput, new GUIContent("(Optional) Show errors in"));
            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif