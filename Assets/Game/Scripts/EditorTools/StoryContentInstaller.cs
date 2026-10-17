using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BorrowTomorrow.Core;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    /// <summary>只更新对白数据与 Bootstrap 叙事接线，不重新生成关卡。</summary>
    public static class StoryContentInstaller
    {
        [Serializable] private sealed class Catalog { public Line[] entries; }
        [Serializable] private sealed class Line
        {
            public string id, speaker, text, next, voice, image;
            public string[] flags;
            public bool holdForConfirm, blackBackground;
        }
        public static Dictionary<string, string> CreateDialogueAssets()
        {
            var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText("Assets/Game/Scripts/EditorTools/StoryContent.json"));
            var map = new Dictionary<string, string>();
            var ids = new HashSet<string>(catalog.entries.Select(e => e.id));
            if (ids.Count != catalog.entries.Length) throw new InvalidOperationException("Duplicate dialogue ids");
            foreach (var line in catalog.entries)
            {
                if (!string.IsNullOrEmpty(line.next) && !ids.Contains(line.next))
                    throw new InvalidOperationException("Broken dialogue chain: " + line.id);
                string path = "Assets/Game/Data/Dialogue/D_" + line.id + ".asset";
                var entry = AssetDatabase.LoadAssetAtPath<DialogueEntry>(path);
                if (entry == null)
                {
                    entry = ScriptableObject.CreateInstance<DialogueEntry>();
                    AssetDatabase.CreateAsset(entry, path);
                }
                entry.entryId = line.id; entry.speakerLabel = line.speaker; entry.subtitle = line.text;
                entry.nextId = line.next; entry.setFlags = line.flags ?? Array.Empty<string>();
                entry.requiredFlags = Array.Empty<string>();
                entry.holdForConfirm = line.holdForConfirm;
                entry.blackBackground = line.blackBackground;
                entry.minDurationSeconds = string.IsNullOrEmpty(line.text) ? 2.5f : 1.2f;
                entry.voiceClip = string.IsNullOrEmpty(line.voice) ? null : Required<AudioClip>("Assets/Game/Audio/Voice/" + line.voice + ".mp3");
                entry.illustration = null;
                if (!string.IsNullOrEmpty(line.image))
                {
                    string imagePath = AssetDatabase.FindAssets(line.image, new[] { "Assets/Game/Art" })
                        .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == line.image);
                    if (imagePath == null) throw new InvalidOperationException("Missing illustration: " + line.image);
                    entry.illustration = Required<Texture2D>(imagePath);
                }
                EditorUtility.SetDirty(entry);
                map.Add(line.id, path);
            }
            return map;
        }
        private static T Required<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing " + typeof(T).Name + ": " + path);
            return asset;
        }

        [MenuItem("BorrowTomorrow/接入主线内容（保留关卡布局）")]
        public static void Install()
        {
            // Both editor menu entries use the same production narrative pipeline after the merge.
            NarrativeInstaller.Install();
        }

        private static void InstallLegacy()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var paths = CreateDialogueAssets();
                var entries = paths.Values.Select(Required<DialogueEntry>).ToArray();
                var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene", OpenSceneMode.Single);
                var library = UnityEngine.Object.FindObjectOfType<DialogueLibrary>();
                if (library == null) throw new InvalidOperationException("Bootstrap has no DialogueLibrary");
                SetArray(library, "entries", entries);
                var root = library.transform.root.gameObject;
                var presentation = root.GetComponent<StoryPresentation>() ?? root.AddComponent<StoryPresentation>();
                SetArray(presentation, "entries", entries);
                Set(presentation, "font", Required<Font>("Assets/Game/Art/UI/Fonts/PlaceholderChinese.ttf"));
                var audio = root.GetComponent<BorrowTomorrow.Audio.AudioService>();
                Set(audio, "musicPast", Required<AudioClip>("Assets/Game/Audio/Music/BGM_Past_1994.wav"));
                Set(audio, "musicPresent", Required<AudioClip>("Assets/Game/Audio/Music/BGM_Present_2024.wav"));
                Set(audio, "musicEpilogue", Required<AudioClip>("Assets/Game/Audio/Music/BGM_Epilogue.wav"));
                var subtitle = root.GetComponentInChildren<SubtitlePanel>(true);
                if (subtitle != null)
                {
                    var so = new SerializedObject(subtitle);
                    var panel = (GameObject)so.FindProperty("root").objectReferenceValue;
                    var background = panel.GetComponent<UnityEngine.UI.Image>() ?? panel.AddComponent<UnityEngine.UI.Image>();
                    background.color = new Color(0.04f, 0.055f, 0.045f, 0.94f);
                    background.raycastTarget = false;
                    foreach (var text in panel.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                        text.color = new Color(0.96f, 0.92f, 0.82f);
                }
                EditorSceneManager.SaveScene(scene);
                var epilogue = Required<LevelDefinition>("Assets/Game/Data/Levels/Epilogue_Epilogue.asset");
                epilogue.exitRequiredFlags = new[] { StoryFlags.EpilogueSeen };
                epilogue.exitLockedHint = "先打开铁盒，读完留给晓晓的信。";
                epilogue.initialObjective = "打开铁盒，陪外婆坐一会儿。";
                EditorUtility.SetDirty(epilogue);
                AssetDatabase.SaveAssets();
                Debug.Log("[StoryContentInstaller] Installed " + entries.Length + " dialogue entries, illustrations, records and music.");
            }
            finally
            {
                if (!Application.isBatchMode && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void SetArray(UnityEngine.Object target, string field, DialogueEntry[] entries)
        {
            var so = new SerializedObject(target); var prop = so.FindProperty(field);
            prop.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = entries[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
