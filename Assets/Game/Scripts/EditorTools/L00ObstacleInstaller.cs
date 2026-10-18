using System;
using System.Linq;
using BorrowTomorrow.Core.Data;
using BorrowTomorrow.Level;
using BorrowTomorrow.Narrative;
using BorrowTomorrow.TimeBoundary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BorrowTomorrow.EditorTools
{
    /// <summary>过去巷道堆满木箱，现在箱子已搬走；不把正面门当作横向路障。</summary>
    public static class L00ObstacleInstaller
    {
        public const string SpritePath = "Assets/Game/Art/Props/Temporal/PROP_L00_StoredCrates_1994.png";

        [MenuItem("BorrowTomorrow/替换 L00 挡路木箱（保留剧情）")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit play mode first.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save current scene edits first.");
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            if (importer == null) throw new InvalidOperationException("Import the crate sprite first.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/L00_Gate.scene");
            ConfigureRoom(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LevelRoot>(true)).Single());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        internal static void ConfigureRoom(LevelRoot room)
        {
            // Stable ID retained for old room snapshots; the actual obstacle is now stored crates.
            var obstacle = room.GetComponentsInChildren<TemporalObject>(true).Single(t => t.ObjectId == "L00_fence_gate");
            obstacle.transform.position = new Vector3(10f, 0f, 0f);
            var definition = AssetDatabase.LoadAssetAtPath<TemporalObjectDefinition>("Assets/Game/Data/TemporalObjects/L00_fence_gate.asset");
            definition.anchorX = 10f;
            definition.safetyPolicy = SafetyPolicy.FullCheck;
            definition.pastState.stateId = "stored_crates";
            definition.presentState.stateId = "cleared_lane";
            definition.pastState.blocksMovement = true;
            definition.pastState.canStand = true;
            definition.presentState.blocksMovement = definition.presentState.canStand = false;
            EditorUtility.SetDirty(definition);
            obstacle.EditorConfigure(definition);
            Transform past = obstacle.transform.Find("Past"), present = obstacle.transform.Find("Present");
            foreach (Transform root in new[] { past, present })
            {
                foreach (Transform child in root.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                root.localPosition = Vector3.zero;
            }
            var crates = ArtDeliveryInstaller.PutByBounds(past, "StoredCrates",
                AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath), new Vector2(10f, 0f), 3.6f, "EraPlatform", 2);
            // The solid visible stack is too tall to jump over; no invisible lintel or steps.
            var body = new GameObject("CrateStackCollider");
            body.transform.SetParent(past, false);
            body.layer = LayerMask.NameToLayer("Temporal");
            var bounds = crates.bounds;
            body.transform.position = bounds.center;
            var collider = body.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(bounds.size.x - .10f, bounds.size.y - .06f);
            collider.offset = Vector2.zero;
            obstacle.ApplyEra(Era.Past, true);
            // Reading the notice now lies on the route between clearing the crates and meeting A-Huang.
            var notice = room.GetComponentInChildren<PresentOnlyInvestigation>(true);
            if (notice != null)
            {
                notice.transform.position = new Vector3(12.5f, .9f, 0f);
                notice.anchorX = 12.5f;
                EditorUtility.SetDirty(notice);
            }
            EditorUtility.SetDirty(room);
        }
    }
}
