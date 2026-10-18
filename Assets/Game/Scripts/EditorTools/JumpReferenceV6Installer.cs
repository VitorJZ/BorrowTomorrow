using System;
using System.IO;
using System.Linq;
using BorrowTomorrow.Player;
using UnityEditor;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    public static class JumpReferenceV6Installer
    {
        public const string Root = "Assets/Game/Art/Characters/JumpReference_v6";
        public const float PreparationSeconds = .18f;
        [Serializable] private class Manifest
        {
            public int frameCount;
            public float fps, pixelsPerUnit, standingHeightPx;
            public float[] origin, canvas;
            public Frame[] frames;
        }
        [Serializable] private class Frame { public string texture; public float durationMs; }
        private static Manifest Read() => JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "/manifest.json"));
        private static T Save<T>(T asset, string path) where T : UnityEngine.Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(asset, path); return asset; }
            EditorUtility.CopySerialized(asset, existing);
            UnityEngine.Object.DestroyImmediate(asset);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        private static AnimationClip MakeClip(string name, Manifest m, Sprite[] sprites, int start, int count, float total = 0)
        {
            float sum = m.frames.Skip(start).Take(count).Sum(f => f.durationMs) / 1000f;
            float factor = total > 0 ? total / sum : 1f;
            var clip = new AnimationClip { name = name, frameRate = m.fps };
            var keys = new ObjectReferenceKeyframe[count + 1];
            float time = 0;
            for (int i = 0; i < count; i++)
            {
                keys[i] = new ObjectReferenceKeyframe { time = time, value = sprites[start + i] };
                time += m.frames[start + i].durationMs / 1000f * factor;
            }
            keys[count] = new ObjectReferenceKeyframe { time = time, value = sprites[start + count - 1] };
            AnimationUtility.SetObjectReferenceCurve(clip,
                new EditorCurveBinding { path = "Visual", type = typeof(SpriteRenderer), propertyName = "m_Sprite" }, keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return Save(clip, Root + "/" + name + ".anim");
        }

        [MenuItem("BorrowTomorrow/制作已确认24帧跳跃资产")]
        public static void Create()
        {
            AssetDatabase.Refresh();
            var m = Read();
            if (m.frameCount != 24 || m.frames == null || m.frames.Length != 24)
                throw new InvalidOperationException("Expected24 complete-person jump frames.");
            Directory.CreateDirectory(Root + "/Sprites");
            AssetDatabase.Refresh();
            var sprites = new Sprite[24];
            for (int i = 0; i < sprites.Length; i++)
            {
                string texturePath = Root + "/" + m.frames[i].texture;
                var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing jump PNG: " + texturePath);
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                var pivot = new Vector2(m.origin[0] / texture.width, (texture.height - m.origin[1]) / texture.height);
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot,
                    m.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprite.name = "Jump_" + (i + 1).ToString("00");
                sprites[i] = Save(sprite, Root + "/Sprites/" + sprite.name + ".asset");
            }
            MakeClip("Jump_Full_24", m, sprites, 0, 24);
            // Stand frame01 belongs to idle. Frames02-07 prepare the physical launch.
            MakeClip("Jump_Prepare", m, sprites, 1, 6, PreparationSeconds);
            MakeClip("Jump_Rise", m, sprites, 7, 3);
            MakeClip("Jump_Apex", m, sprites, 10, 1);
            MakeClip("Jump_Fall", m, sprites, 11, 2);
            MakeClip("Jump_Recover", m, sprites, 13, 11);
            AssetDatabase.SaveAssets();
        }

        public static CharacterArtAnimator.Clip[] CreateGameplayClips(Sprite referenceIdle)
        {
            Create();
            var m = Read();
            var sprites = Enumerable.Range(1, 24).Select(i =>
                AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Sprites/Jump_" + i.ToString("00") + ".asset")).ToArray();
            if (referenceIdle == null || sprites.Any(s => s == null))
                throw new InvalidOperationException("Jump24 or reference idle is missing.");
            // Padding is constant; fit the drawn standing height, not the padded rectangle.
            float scale = referenceIdle.bounds.size.y / (m.standingHeightPx / m.pixelsPerUnit);
            var offset = new Vector2(0, referenceIdle.bounds.min.y);
            CharacterArtAnimator.Clip Phase(string name, string asset, int start, int count, float total = 0)
            {
                float[] holds = m.frames.Skip(start).Take(count).Select(f => f.durationMs / 1000f).ToArray();
                if (total > 0) { float factor = total / holds.Sum(); for (int i = 0; i < holds.Length; i++) holds[i] *= factor; }
                return new CharacterArtAnimator.Clip
                {
                    name = name, frames = sprites.Skip(start).Take(count).ToArray(), fps = m.fps, loop = false,
                    animation = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "/" + asset + ".anim"),
                    frameDurations = holds, visualOffset = offset, visualScale = scale
                };
            }
            return new[]
            {
                Phase("JumpPrepare", "Jump_Prepare", 1, 6, PreparationSeconds),
                Phase("JumpRise", "Jump_Rise", 7, 3), Phase("JumpApex", "Jump_Apex", 10, 1),
                Phase("JumpFall", "Jump_Fall", 11, 2), Phase("JumpRecover", "Jump_Recover", 13, 11)
            };
        }

        [MenuItem("BorrowTomorrow/接入已确认24帧跳跃")]
        public static void Integrate()
        {
            const string path = "Assets/Game/Prefabs/Player_Graybox.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var art = root.GetComponent<CharacterArtAnimator>();
                if (art == null || art.visual == null) throw new InvalidOperationException("Player art is missing.");
                var idle = art.clips.First(c => c.name == "Idle").frames[0];
                var jump = CreateGameplayClips(idle);
                art.clips = art.clips.Where(c => !c.name.StartsWith("Jump", StringComparison.Ordinal)).Concat(jump).ToArray();
                var motor = new SerializedObject(root.GetComponent<PlayerMotor2D>());
                motor.FindProperty("jumpAnticipation").floatValue = PreparationSeconds;
                motor.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(art);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[JumpReferenceV6] INSTALLED Jump24: Prepare6/Rise3/Apex1/Fall2/Recover11 plus source stand; Walk=" +
                    art.clips.First(c => c.name == "Walk").frames.Length + "; Climb=" + art.clips.First(c => c.name == "Climb").frames.Length);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            string output = Environment.GetEnvironmentVariable("JUMP_V6_PACKAGE_OUTPUT");
            if (!string.IsNullOrEmpty(output))
            {
                Directory.CreateDirectory(output);
                AssetDatabase.ExportPackage(new[] { Root, path }, Path.Combine(output, "JumpReference_v6.unitypackage"), ExportPackageOptions.Recurse);
            }
        }
        public static void IntegrateAndBuild()
        {
            Integrate();
            JumpRecoveryInstaller.BuildCurrent();
        }
    }
}
