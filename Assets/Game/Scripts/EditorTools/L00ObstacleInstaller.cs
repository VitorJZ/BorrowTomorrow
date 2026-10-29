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
    /// <summary>2024 年巷道堆满旧木箱，1994 年这里还是空巷；不把正面门当作横向路障。</summary>
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
            obstacle.transform.position = new Vector3(12f, 0f, 0f);
            var definition = AssetDatabase.LoadAssetAtPath<TemporalObjectDefinition>("Assets/Game/Data/TemporalObjects/L00_fence_gate.asset");
            definition.anchorX = 12f;
            definition.safetyPolicy = SafetyPolicy.FullCheck;
            // 2024（现在）堆着木箱，1994（过去）巷道已经清空。
            definition.pastState.stateId = "cleared_lane";
            definition.presentState.stateId = "stored_crates";
            definition.pastState.blocksMovement = definition.pastState.canStand = false;
            definition.presentState.blocksMovement = true;
            definition.presentState.canStand = false;
            EditorUtility.SetDirty(definition);
            obstacle.EditorConfigure(definition);
            Transform past = obstacle.transform.Find("Past"), present = obstacle.transform.Find("Present");
            foreach (Transform root in new[] { past, present })
            {
                foreach (Transform child in root.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
                root.localPosition = Vector3.zero;
            }
            var crates = ArtDeliveryInstaller.PutByBounds(present, "StoredCrates",
                AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath), new Vector2(12f, 0f), 3.6f, "EraPlatform", 2);
            // The solid visible stack is too tall to jump over; no invisible lintel or steps.
            var body = new GameObject("CrateStackCollider");
            body.transform.SetParent(present, false);
            body.layer = LayerMask.NameToLayer("Temporal");
            var bounds = crates.bounds;
            body.transform.position = bounds.center;
            var collider = body.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(bounds.size.x - .10f, bounds.size.y - .06f);
            collider.offset = Vector2.zero;
            obstacle.ApplyEra(Era.Present, true);
            // 通知位于木箱之前的 2024 砖墙上，读完才会引出回屋独白。
            var notice = room.GetComponentInChildren<PresentOnlyInvestigation>(true);
            if (notice != null)
            {
                notice.transform.position = new Vector3(7.6f, .9f, 0f);
                notice.anchorX = 7.6f;
                EditorUtility.SetDirty(notice);
            }
            // 木箱和狗窝之间留出通行与检查点空间，避免美术互相压叠。
            foreach (Transform child in room.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Spawn_cp_L00_courtyard")
                    child.position = new Vector3(14.6f, child.position.y, 0f);
                else if (child.name == "Checkpoint_cp_L00_courtyard")
                    child.position = new Vector3(14.6f, child.position.y, 0f);
            }
            // 新游戏从 2024 年（边界最左）开始，必须由玩家把边界向右拖过木箱才能进入 1994。
            var roomData = new SerializedObject(room);
            var initialBoundary = roomData.FindProperty("initialBoundary");
            if (initialBoundary != null) initialBoundary.floatValue = 0f;
            roomData.ApplyModifiedPropertiesWithoutUndo();
            var levelDefinition = AssetDatabase.LoadAssetAtPath<LevelDefinition>("Assets/Game/Data/Levels/L00_L00_Gate.asset");
            if (levelDefinition != null)
            {
                levelDefinition.initialBoundary = 0f;
                // 目标在读完通知后才出现，开场只保留操作提示。
                levelDefinition.initialObjective = string.Empty;
                EditorUtility.SetDirty(levelDefinition);
            }
            EditorUtility.SetDirty(room);
        }
    }
}
