using System;
using BorrowTomorrow.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    public static class BoundaryAudioInstaller
    {
        private const string Root = "Assets/Game/Audio/SFX/Boundary/";

        public static void Configure(GameObject systemRoot)
        {
            var emitter = systemRoot.GetComponent<BoundaryAudioEmitter>();
            if (emitter == null) emitter = systemRoot.AddComponent<BoundaryAudioEmitter>();
            var serialized = new SerializedObject(emitter);
            string[,] bindings = {
                { "pastSlow", "boundary_drag_past_slow_loop" },
                { "pastFast", "boundary_drag_past_fast_loop" },
                { "presentSlow", "boundary_drag_present_slow_loop" },
                { "presentFast", "boundary_drag_present_fast_loop" },
                { "dragStart", "boundary_drag_start" },
                { "dragStop", "boundary_drag_stop" },
                { "switchOk", "boundary_switch_ok" },
                { "switchBlocked", "boundary_switch_blocked" }
            };
            for (int i = 0; i < bindings.GetLength(0); i++)
            {
                string path = Root + bindings[i, 1] + ".wav";
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null) throw new InvalidOperationException("Missing boundary audio: " + path);
                serialized.FindProperty(bindings[i, 0]).objectReferenceValue = clip;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(emitter);
        }

        [MenuItem("BorrowTomorrow/音频/接入时间边界音效")]
        public static void Install()
        {
            // Keep the existing scene layout, narrative and character assets intact.
            AssetDatabase.Refresh();
            foreach (string path in AssetDatabase.FindAssets("t:AudioClip", new[] { Root.TrimEnd('/') }))
            {
                string audioPath = AssetDatabase.GUIDToAssetPath(path);
                var importer = AssetImporter.GetAtPath(audioPath) as AudioImporter;
                if (importer == null) continue;
                var sample = importer.defaultSampleSettings;
                sample.loadType = AudioClipLoadType.DecompressOnLoad;
                sample.compressionFormat = AudioCompressionFormat.PCM;
                sample.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                sample.preloadAudioData = true;
                importer.defaultSampleSettings = sample;
                importer.forceToMono = false;
                importer.loadInBackground = false;
                importer.SaveAndReimport();
            }
            var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.scene");
            AudioService service = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                service = root.GetComponentInChildren<AudioService>(true);
                if (service != null) break;
            }
            if (service == null) throw new InvalidOperationException("Bootstrap AudioService not found.");
            Configure(service.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[BoundaryAudio] Installed eight clips on Bootstrap; existing level scenes preserved.");
        }

        public static void InstallAndBuild()
        {
            Install();
            JumpRecoveryInstaller.BuildCurrent();
        }
    }
}
