using System;
using System.IO;
using System.Linq;
using BorrowTomorrow.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    public static class ClimbFrameV5Installer
    {
        const string Root = "Assets/Game/Art/Characters/ClimbFrames_v5";
        [Serializable] class Manifest
        {
            public float fps;
            public int frame_count;
            public float[] pivot;
            public float[] cell;
            public float pixels_per_unit;
            public Frame[] frames;
            public ReviewLadder review_ladder;
        }
        [Serializable] class Frame
        {
            public string file;
            public float uniform_scale, waist_y, crown_y;
            public float[] feet_y;
        }
        [Serializable] class ReviewLadder { public float cycle_rise_px; }

        // Shared by the targeted installer and the full art import, so rebuilding
        // a room/art pass cannot silently restore the rejected eight-frame climb.
        public static CharacterArtAnimator.Clip CreateGameplayClip(Sprite referenceIdle)
        {
            if (referenceIdle == null) throw new InvalidOperationException("Idle reference is required for climb alignment.");
            var m = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "/manifest.json"));
            var sprites = m.frames.Select(f => AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/" + f.file)).ToArray();
            var animation = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/Climb_Rear_15f_v5.anim");
            if (animation == null || sprites.Any(s => s == null))
            {
                Create();
                sprites = m.frames.Select(f => AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/" + f.file)).ToArray();
                animation = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/Climb_Rear_15f_v5.anim");
            }
            if (sprites.Length != 15 || sprites.Any(s => s == null) || animation == null)
                throw new InvalidOperationException("Approved climb assets are incomplete.");
            var first = m.frames[0];
            float rootY = m.cell[1] * (1f - m.pivot[1]);
            float crownY = rootY - (first.waist_y - first.crown_y) * first.uniform_scale;
            float soleY = first.feet_y.Max();
            float ppu = sprites[0].pixelsPerUnit;
            float drawnHeight = (soleY - crownY) / ppu;
            if (drawnHeight <= 0f) throw new InvalidOperationException("Invalid climb registration.");
            float scale = referenceIdle.bounds.size.y / drawnHeight;
            return new CharacterArtAnimator.Clip
            {
                name = "Climb", frames = sprites, animation = animation,
                fps = m.fps, loop = true, visualScale = scale,
                visualOffset = new Vector2(0f, referenceIdle.bounds.min.y + (soleY-rootY)/ppu*scale),
                distancePerCycle = m.review_ladder.cycle_rise_px / ppu * scale
            };
        }

        [MenuItem("BorrowTomorrow/应用已确认的爬梯动画 v5")]
        public static void Integrate()
        {
            Create();
            const string path = "Assets/Game/Prefabs/Player_Graybox.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var art = root.GetComponent<CharacterArtAnimator>();
                if (art == null || art.visual == null) throw new InvalidOperationException("Player art is not configured.");
                var idle = art.clips.First(c => c.name == "Idle");
                var climb = CreateGameplayClip(idle.frames[0]);
                int index = Array.FindIndex(art.clips, c => c.name == "Climb");
                if (index < 0) art.clips = art.clips.Concat(new[] { climb }).ToArray();
                else art.clips[index] = climb;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[ClimbV5Gameplay] INSTALLED: {climb.frames.Length} frames, scale={climb.visualScale:F4}, offsetY={climb.visualOffset.y:F4}, cycleDistance={climb.distancePerCycle:F4}, duration={climb.animation.length:F2}s.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        public static void IntegrateAndBuild()
        {
            Integrate();
            string output = Environment.GetEnvironmentVariable("BORROWTOMORROW_CLIMB_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = Path.GetFullPath("Builds/Climb_v5/BorrowTomorrow.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                target = BuildTarget.StandaloneWindows64, locationPathName = output,
                options = BuildOptions.Development
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Climb gameplay build failed: " + report.summary.result);
            Debug.Log("[ClimbV5Gameplay] BUILD SUCCEEDED: " + output);
        }

        static T Save<T>(T value, string path) where T : UnityEngine.Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing);
            UnityEngine.Object.DestroyImmediate(value);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        [MenuItem("Borrow Tomorrow/Art/Create Climb v5 Review Assets")]
        public static void Create()
        {
            AssetDatabase.Refresh();
            var m = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "/manifest.json"));
            if (m.frame_count != 15 || m.frames.Length != 15)
                throw new InvalidOperationException("Climb v5 requires 15 different frames.");
            var sprites = m.frames.Select(f =>
            {
                var path = Root + "/" + f.file;
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = m.pixels_per_unit;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(m.pivot[0], m.pivot[1]);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }).ToArray();
            if (sprites.Any(s => s == null) || sprites.Distinct().Count() != 15)
                throw new InvalidOperationException("Frame import was incomplete or duplicated.");

            var clip = new AnimationClip { name = "Climb_Rear_15f_v5", frameRate = m.fps };
            // PPtr curves retain their last sample for one frame. An explicit
            // first-frame key at 1.5s would extend this clip to 1.6s.
            var keys = sprites.Select((s,i) => new ObjectReferenceKeyframe { time = i / m.fps, value = s }).ToArray();
            AnimationUtility.SetObjectReferenceCurve(clip,
                new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" }, keys);
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            clip = Save(clip, Root + "/Climb_Rear_15f_v5.anim");

            var controllerPath = Root + "/Climb_Rear_15f_v5.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var state = controller.layers[0].stateMachine.AddState("Climb");
                state.motion = clip;
                controller.layers[0].stateMachine.defaultState = state;
            }
            var actor = new GameObject("Climb_v5_ReviewOnly");
            try
            {
                var renderer = actor.AddComponent<SpriteRenderer>();
                renderer.sprite = sprites[0];
                renderer.sortingOrder = 10;
                actor.AddComponent<Animator>().runtimeAnimatorController = controller;
                PrefabUtility.SaveAsPrefabAsset(actor, Root + "/Climb_v5_ReviewOnly.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (Mathf.Abs(clip.length - 1.5f) > .001f)
                throw new InvalidOperationException("Unexpected Climb v5 duration: " + clip.length);
            var output = Environment.GetEnvironmentVariable("CLIMB_V5_OUTPUT");
            if (!string.IsNullOrEmpty(output))
            {
                Directory.CreateDirectory(output);
                // Keep the art package portable: the gameplay installer depends
                // on this project's player module and belongs in source control.
                AssetDatabase.ExportPackage(Root, Path.Combine(output, "ClimbFrames_v5.unitypackage"), ExportPackageOptions.Recurse);
            }
            Debug.Log("[ClimbV5] PASS: 15 distinct sprites, 10 fps, 1.5 second loop, review prefab and package. Gameplay unchanged.");
        }
    }
}
