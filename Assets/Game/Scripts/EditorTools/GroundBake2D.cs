using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BorrowTomorrow.EditorTools
{
    /// <summary>
    /// Composites a hand-painted 2D ground strip into the existing backdrop
    /// images, replacing only the band below the walking line. Everything
    /// above the line stays pixel-identical.
    /// </summary>
    public static class GroundBake2D
    {
        private const string StripPath = "ArtRegen_20261029/ground2d/out/PROP_Ground_2D.png";
        private const string BackupDir = "ArtRegen_20261029/ground2d/backup";

        private sealed class Target
        {
            public readonly string Path;
            public readonly int GroundLineFromBottom;
            public readonly Color Tint;
            public Target(string path, int groundLineFromBottom, Color tint)
            {
                Path = path;
                GroundLineFromBottom = groundLineFromBottom;
                Tint = tint;
            }
        }

        private static readonly Target[] Targets =
        {
            new Target("Assets/Game/Art/Delivery/Backgrounds_v2/BG_L00_Gate_1994_v2.png", 179, Color.white),
            new Target("Assets/Game/Art/Delivery/Backgrounds_v2/BG_L00_Gate_2024_v2.png", 179, new Color(0.95f, 0.98f, 1.02f)),
            new Target("Assets/Game/Art/SampleV1/01_背景_Backgrounds/BG_L01B_CommonArchitecture_v2.png", 322, Color.white),
            new Target("Assets/Game/Art/Delivery/Backgrounds_v2/BG_L02_Yard_1994_v2.png", 143, Color.white),
            new Target("Assets/Game/Art/Delivery/Backgrounds_v2/BG_L02_Yard_2024_v2.png", 143, new Color(0.95f, 0.98f, 1.02f)),
        };

        private static readonly string[] ScenePaths =
        {
            "Assets/Game/Scenes/L00_Gate.scene",
            "Assets/Game/Scenes/L01_TreeCourtyard.scene",
            "Assets/Game/Scenes/L02_Sewing.scene",
        };

        [MenuItem("BorrowTomorrow/Visual/Bake 2D Ground Into Backgrounds")]
        public static void Bake()
        {
            if (!File.Exists(StripPath))
            {
                Debug.LogError("[GroundBake2D] Missing strip: " + StripPath);
                return;
            }

            var strip = LoadTexture(StripPath);
            if (strip == null) return;

            int lineRow = FindGroundLineRowFromTop(strip);
            Debug.Log($"[GroundBake2D] Strip {strip.width}x{strip.height}, ground line at row {lineRow} from top.");

            Directory.CreateDirectory(BackupDir);
            foreach (var target in Targets)
            {
                if (!File.Exists(target.Path))
                {
                    Debug.LogWarning("[GroundBake2D] Missing background: " + target.Path);
                    continue;
                }

                string backup = Path.Combine(BackupDir, Path.GetFileName(target.Path));
                if (!File.Exists(backup)) File.Copy(target.Path, backup);

                BakeOne(target, strip, lineRow);
            }

            Object.DestroyImmediate(strip);
            SetGroundArtVisible(false);
            AssetDatabase.SaveAssets();
            Debug.Log("[GroundBake2D] Done.");
        }

        [MenuItem("BorrowTomorrow/Visual/Revert 2D Ground Bake")]
        public static void Revert()
        {
            foreach (var target in Targets)
            {
                string backup = Path.Combine(BackupDir, Path.GetFileName(target.Path));
                if (!File.Exists(backup))
                {
                    Debug.LogWarning("[GroundBake2D] No backup for " + target.Path);
                    continue;
                }
                File.Copy(backup, target.Path, true);
                AssetDatabase.ImportAsset(target.Path, ImportAssetOptions.ForceUpdate);
            }

            SetGroundArtVisible(true);
            AssetDatabase.SaveAssets();
            Debug.Log("[GroundBake2D] Reverted.");
        }

        private static Texture2D LoadTexture(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
            {
                Debug.LogError("[GroundBake2D] Failed to read " + path);
                Object.DestroyImmediate(texture);
                return null;
            }
            return texture;
        }

        private static int FindGroundLineRowFromTop(Texture2D strip)
        {
            var pixels = strip.GetPixels32();
            int width = strip.width;
            int height = strip.height;
            int samples = (width + 3) / 4;

            for (int row = 0; row < height; row++)
            {
                int y = height - 1 - row;
                int nonMagenta = 0;
                for (int x = 0; x < width; x += 4)
                {
                    if (!IsMagenta(pixels[y * width + x])) nonMagenta++;
                }
                if (nonMagenta > samples * 0.75f) return row;
            }
            return 0;
        }

        private static bool IsMagenta(Color32 c)
        {
            return c.r > 190 && c.b > 190 && c.g < 90;
        }

        private static void BakeOne(Target target, Texture2D strip, int stripLineRowFromTop)
        {
            var background = LoadTexture(target.Path);
            if (background == null) return;

            int bgWidth = background.width;
            int bgHeight = background.height;
            float scale = bgWidth / (float)strip.width;
            int bandHeight = Mathf.Clamp(target.GroundLineFromBottom, 1, bgHeight);

            var bgPixels = background.GetPixels32();
            var stripPixels = strip.GetPixels32();

            for (int y = 0; y < bandHeight; y++)
            {
                // y counts up from the bottom of the background.
                float stripRow = stripLineRowFromTop + (bandHeight - 1 - y) / scale;
                int sourceY = Mathf.Clamp(
                    strip.height - 1 - Mathf.RoundToInt(stripRow),
                    0,
                    strip.height - 1);

                for (int x = 0; x < bgWidth; x++)
                {
                    float sourceX = (x + 0.5f) / scale - 0.5f;
                    int x0 = Mathf.Clamp(Mathf.FloorToInt(sourceX), 0, strip.width - 1);
                    int x1 = Mathf.Clamp(x0 + 1, 0, strip.width - 1);
                    float t = Mathf.Clamp01(sourceX - x0);

                    Color32 a = stripPixels[sourceY * strip.width + x0];
                    Color32 b = stripPixels[sourceY * strip.width + x1];

                    // Never paste magenta (or a magenta fringe) over the art.
                    bool magenta = IsMagenta(a) && IsMagenta(b);
                    if (magenta) continue;

                    bgPixels[y * bgWidth + x] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(a.r, b.r, t) * target.Tint.r), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(a.g, b.g, t) * target.Tint.g), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(a.b, b.b, t) * target.Tint.b), 0, 255),
                        255);
                }
            }

            background.SetPixels32(bgPixels);
            background.Apply(false, false);
            File.WriteAllBytes(target.Path, background.EncodeToPNG());
            Object.DestroyImmediate(background);

            AssetDatabase.ImportAsset(target.Path, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[GroundBake2D] Baked {Path.GetFileName(target.Path)} band={bandHeight}px.");
        }

        private static void SetGroundArtVisible(bool visible)
        {
            var active = EditorSceneManager.GetActiveScene();
            foreach (string path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool changed = false;

                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        if (renderer.sprite == null) continue;
                        if (!renderer.sprite.name.Contains("Ground")) continue;
                        if (renderer.transform.parent == null) continue;
                        if (renderer.transform.parent.name != "Ground") continue;

                        renderer.enabled = visible;
                        changed = true;
                    }
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }

            if (!string.IsNullOrEmpty(active.path))
                EditorSceneManager.OpenScene(active.path, OpenSceneMode.Single);
        }
    }
}
