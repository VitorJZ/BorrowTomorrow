using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    // Produces isolated review assets. Never edits the gameplay prefab or scenes.
    public static class WalkCandidateInstaller
    {
        [Serializable] private class Manifest { public float fps; public Frame[] frames; }
        [Serializable] private class Frame { public float[] rect; public float[] pivot; public float ppu; }
        private const string Root = "Assets/Game/Art/Characters/WalkCandidate_v3";

        [MenuItem("BorrowTomorrow/制作独立走路候选资产（不接入游戏）")]
        public static void Create()
        {
            AssetDatabase.Refresh();
            string texturePath = Root + "/Hero_Walk_Candidate_v3.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "/walk_candidate.json"));
            Directory.CreateDirectory(Root + "/Frames");
            AssetDatabase.Refresh();
            var frames = new Sprite[manifest.frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                var f = manifest.frames[i];
                string path = Root + "/Frames/Walk_" + i.ToString("00") + ".asset";
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (frames[i] != null) continue;
                frames[i] = Sprite.Create(texture,new Rect(f.rect[0],f.rect[1],f.rect[2],f.rect[3]),new Vector2(f.pivot[0],f.pivot[1]),f.ppu,0,SpriteMeshType.FullRect);
                frames[i].name = "Walk_" + i.ToString("00");
                AssetDatabase.CreateAsset(frames[i],path);
            }
            string clipPath = Root + "/Hero_Walk.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip,clipPath); }
            clip.frameRate = manifest.fps;
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / manifest.fps, value = frames[i] };
            keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / manifest.fps, value = frames[0] };
            AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" }, keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true; settings.loopBlend = false;
            AnimationUtility.SetAnimationClipSettings(clip,settings);
            EditorUtility.SetDirty(clip);
            string controllerPath = Root + "/Hero_Walk.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var state = controller.layers[0].stateMachine.AddState("Walk");
                state.motion = clip; controller.layers[0].stateMachine.defaultState = state;
            }
            var go = new GameObject("Hero_Walk_ReviewOnly");
            try
            {
                go.AddComponent<SpriteRenderer>().sprite = frames[0];
                var animator = go.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                PrefabUtility.SaveAsPrefabAsset(go,Root + "/Hero_Walk_ReviewOnly.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string output = Environment.GetEnvironmentVariable("WALK_CANDIDATE_OUTPUT");
            if (!string.IsNullOrEmpty(output))
            {
                Directory.CreateDirectory(output);
                AssetDatabase.ExportPackage(Root,Path.Combine(output,"Hero_Walk_Candidate_v3.unitypackage"),ExportPackageOptions.Recurse);
            }
            Debug.Log("[WalkCandidate] Created isolated Sprite assets, loop AnimationClip, AnimatorController and review prefab. Gameplay unchanged.");
        }
    }
}
