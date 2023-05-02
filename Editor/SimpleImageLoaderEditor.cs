#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using System.IO;
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

        SerializedProperty playButton;
        SerializedProperty stopButton;

        SerializedProperty Property(string name)
        {
            return serializedObject.FindProperty(name);
        }

        private void OnEnable()
        {
            urls = Property(nameof(SimpleImageLoader.urls));
            receivingMaterial = Property(nameof(SimpleImageLoader.receivingMaterial));
            receivingPanel = Property(nameof(SimpleImageLoader.receivingPanel));
            rescalePanel = Property(nameof(SimpleImageLoader.rescalePanel));
            errorOutput = Property(nameof(SimpleImageLoader.errorOutput));
            errorTexture = Property(nameof(SimpleImageLoader.errorTexture));
            pictureRefreshTime = Property(nameof(SimpleImageLoader.pictureRefreshTime));

            synchronise = Property(nameof(SimpleImageLoader.synchronise));

            playButton = Property(nameof(SimpleImageLoader.playButton));
            stopButton = Property(nameof(SimpleImageLoader.stopButton));

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

        string[] UrlsToString()
        {
            var currentUrls = ((SimpleImageLoader) target).urls;
            var stringUrls = new string[currentUrls.Length];
            for (int i = 0; i < currentUrls.Length; i++)
            {
                stringUrls[i] = currentUrls[i].ToString();
            }
            return stringUrls;
        }

        void LoadUrlsFrom(string filePath, bool addToExisting)
        {
            var urls = File.ReadAllLines(filePath);
            List<VRCUrl> urlList = new List<VRCUrl>();
            if (addToExisting)
            {
                urlList.AddRange(((SimpleImageLoader) target).urls);
            }
            foreach (var url in urls)
            {
                if (url.Trim() != "") urlList.Add(new VRCUrl(url));
            }
            ((SimpleImageLoader) target).urls = urlList.ToArray();

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

            if (GUILayout.Button("Load from file"))
            {
                var loadFromFilePath = EditorUtility.OpenFilePanel("Load from list", Application.dataPath, "txt");
                if (loadFromFilePath != null)
                {
                    LoadUrlsFrom(loadFromFilePath, false);
                }
            }

            if (GUILayout.Button("Append from file"))
            {
                var loadFromFilePath = EditorUtility.OpenFilePanel("Append from list", Application.dataPath, "txt");
                if (loadFromFilePath != null)
                {
                    LoadUrlsFrom(loadFromFilePath, true);
                }
            }

            urlsList.DoLayoutList();

            if (GUILayout.Button("Save List To"))
            {
                var exportFilePath = EditorUtility.SaveFilePanel(
                    "Save images list to",
                    Application.dataPath,
                    "list.txt",
                    "txt");
                
                if (exportFilePath != null)
                {
                    File.WriteAllLines(exportFilePath, UrlsToString());
                }
            }
            EditorGUILayout.PropertyField(
                pictureRefreshTime,
                new GUIContent(
                    "Wait time in seconds",
                    "Wait time, in seconds, between the last download and the next one."));
            EditorGUILayout.PropertyField(receivingMaterial);
            EditorGUILayout.PropertyField(errorTexture);

            EditorGUILayout.PropertyField(playButton);
            EditorGUILayout.PropertyField(stopButton);

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